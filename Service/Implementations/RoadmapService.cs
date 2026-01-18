using System.Net.Http.Json;
using System.Text.Json;
using Domain.Contracts;
using Domain.Entities;
using Domain.Exceptions;
using Domain.ModelsSpecifications;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Service.Abstraction;
using Shared.DTOs.RoadmapDTOs;
using Shared.Enums;

namespace Service.Implementations;

public class RoadmapService : IRoadmapService
{
    private readonly IDistributedCache _cache;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IUnitOfWork _unitOfWork;
    private readonly string _pythonApiUrl;

    public RoadmapService(
        IDistributedCache cache,
        IHttpClientFactory httpClientFactory,
        IUnitOfWork unitOfWork,
        IConfiguration configuration)
    {
        _cache = cache;
        _httpClientFactory = httpClientFactory;
        _unitOfWork = unitOfWork;
        _pythonApiUrl = configuration["PipelineApi:BaseUrl"] ?? "http://localhost:8000/generate-roadmap";
    }

    public async Task<RoadmapGenerationDTO> GenerateRoadmapAsync(string[] tags, string language, bool preferPaid)
    {
        var sortedTags = tags.OrderBy(t => t).ToArray();
        string cacheKey = $"roadmap-{string.Join("-", sortedTags)}-{language}-{preferPaid}";

        string? cachedJson = await _cache.GetStringAsync(cacheKey);
        if (!string.IsNullOrEmpty(cachedJson))
        {
            return JsonSerializer.Deserialize<RoadmapGenerationDTO>(cachedJson)!;
        }

        var courses = await _unitOfWork.GetRepo<Course, int>()
                    .GetAllAsync(new CoursesByTagsAndLanguageSpecification(tags, language));


        var missingTagsSet = new HashSet<string>(tags);

        foreach (var course in courses)
        {
            if (missingTagsSet.Count == 0)
                break;

            foreach (var ct in course.CourseTags)
            {
                if (ct.Tag?.Name != null)
                {
                    missingTagsSet.Remove(ct.Tag.Name);
                }
            }
        }

        var missingTags = missingTagsSet.ToArray();

        RoadmapGenerationDTO roadmapData;

        if (missingTags.Any())
        {
            var requestPayload = new
            {
                tags = missingTags,
                language = language,
                prefer_paid = preferPaid
            };

            var httpClient = _httpClientFactory.CreateClient();

            // Call the Python Microservice
            var response = await httpClient.PostAsJsonAsync(_pythonApiUrl, requestPayload);

            if (!response.IsSuccessStatusCode)
                throw new InternalServerErrorException($"Python Scraper failed: {response.ReasonPhrase}");

            roadmapData = await response.Content.ReadFromJsonAsync<RoadmapGenerationDTO>();

            if (roadmapData == null)
                throw new InternalServerErrorException("Received empty data from Python Microservice.");
        }
        else
        {
            roadmapData = new RoadmapGenerationDTO
            {
                Status = "success",
                Data = new RoadmapSourcesDTO()
            };
        }

        var cacheOptions = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24)
        };

        string jsonToCache = JsonSerializer.Serialize(roadmapData);
        await _cache.SetStringAsync(cacheKey, jsonToCache, cacheOptions);

        return roadmapData;
    }

    public async Task<bool> SaveRoadmapAsync(string customerId, RoadmapDTO request)
    {
        var roadmap = new Roadmap
        {
            CustomerId = customerId,
            Title = request.Title,
            Description = request.Description,
            SourceType = request.SourceType,
            TargetJobRole = request.TargetJobRole,
            CreatedAt = DateTime.UtcNow,
            IsArchived = false,
            RoadmapCourses = new List<RoadmapCourse>()
        };

        await _unitOfWork.GetRepo<Roadmap, int>().CreateAsync(roadmap);

        var allItems = new List<(RoadmapItemDTO Item, string Platform)>();

        if (request.RoadmapData.Data.Udemy != null)
            allItems.AddRange(request.RoadmapData.Data.Udemy.Select(x => (x, "Udemy")));

        if (request.RoadmapData.Data.Coursera != null)
            allItems.AddRange(request.RoadmapData.Data.Coursera.Values
                .Where(x => x != null)
                .Select(x => (x!, "Coursera")));

        if (request.RoadmapData.Data.Youtube != null)
            allItems.AddRange(request.RoadmapData.Data.Youtube.Values
                .Where(x => x != null)
                .Select(x => (x!, "Youtube")));

        int orderCounter = 1;

        foreach (var (dto, platformName) in allItems)
        {
            var spec = new CourseByUrlSpecification(dto.Url);
            var existingCourse = await _unitOfWork.GetRepo<Course, int>().GetAsync(spec);
            Course courseToLink;
            if (existingCourse != null)
            {
                courseToLink = existingCourse;
            }
            else
            {
                Enum.TryParse<Platforms>(platformName, true, out var platformEnum);

                courseToLink = new Course
                {
                    Title = dto.Title,
                    Url = dto.Url,
                    Description = dto.Description,
                    ImageUrl = dto.ImageUrl,
                    Duration = dto.Duration,
                    Rating = dto.Score > 5 ? 5.0 : dto.Score,
                    Platform = platformEnum,
                    Language = "en", // Default
                    RetrievedAt = DateTime.UtcNow
                };

                await _unitOfWork.GetRepo<Course, int>().CreateAsync(courseToLink);
            }

            var roadmapCourse = new RoadmapCourse
            {
                Course = courseToLink,
                Order = orderCounter++,
                SectionName = platformName, // e.g. "Coursera"
                IsCompleted = false
            };

            roadmap.RoadmapCourses.Add(roadmapCourse);
        }

        var result = await _unitOfWork.SaveChangesAsync();

        return result > 0;
    }
}