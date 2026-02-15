using System.Net;
using System.Net.Http.Json;
using Domain.Contracts;
using Domain.Entities;
using Domain.Exceptions;
using Domain.ModelsSpecifications;
using Domain.Responses;
using Microsoft.Extensions.Options;
using Service.Abstraction;
using Shared.DTOs.RoadmapDTOs;
using Shared.Enums;
using Shared.Options;

namespace Service.Implementations;

public class RoadmapService(
    IHttpClientFactory httpClientFactory,
    IUnitOfWork unitOfWork,
    IOptions<AiOptions> options) : IRoadmapService
{
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly string _pythonApiUrl = options.Value.PipelineApi.BaseUrl;

    public async Task<APIResponse<RoadmapGenerationDTO>> GenerateRoadmapAsync(string[] tags, string language, bool preferPaid)
    {
        if (!Enum.TryParse<ResourceLanguage>(language, true, out var languageEnum))
        {
            languageEnum = ResourceLanguage.en; // Default fallback
        }

        var courses = await _unitOfWork.GetRepo<Course, int>()
                    .GetAllAsync(new CoursesByTagsAndLanguageSpecification(tags, languageEnum));


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
            var jobId = Guid.NewGuid().ToString("N")[..12];
            var requestPayload = new
            {
                tags = missingTags,
                language,
                prefer_paid = preferPaid,
                job_id = jobId
            };

            var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromMinutes(10);

            try
            {
                // Call the Python Microservice
                var response = await httpClient.PostAsJsonAsync(_pythonApiUrl, requestPayload);

                if (!response.IsSuccessStatusCode)
                    throw new InternalServerErrorException($"Python Scraper failed with status {response.StatusCode}: {response.ReasonPhrase}");

                var roadmapResponse = await response.Content.ReadFromJsonAsync<RoadmapGenerationDTO>() ?? throw new InternalServerErrorException("Received empty data from Python Microservice.");
                roadmapData = roadmapResponse;
            }
            catch (HttpRequestException ex)
            {
                throw new InternalServerErrorException($"Failed to connect to Python Microservice at {_pythonApiUrl}. Is it running? Error: {ex.Message}");
            }
            catch (Exception ex)
            {
                throw new InternalServerErrorException($"An unexpected error occurred while calling the Python Microservice: {ex.Message}");
            }


        }
        else
        {
            roadmapData = new RoadmapGenerationDTO
            {
                Status = "success",
                Data = new RoadmapSourcesDTO()
            };
        }

        // Merge DB found courses into roadmapData
        foreach (var course in courses)
        {
            var dto = new RoadmapItemDTO
            {
                Title = course.Title,
                Url = course.Url,
                Description = course.Description,
                ImageUrl = course.ImageUrl,
                Price = course.CourseBudget.ToString(), // Assuming simple string conversion
                Duration = course.Duration,
                Score = course.Rating,
                Platform = course.Platform.ToString()
            };

            // Generate a unique key for the dictionary
            string key = $"db_{course.Id}";

            switch (course.Platform)
            {
                case Platforms.Udemy:
                    if (!roadmapData.Data.Udemy.ContainsKey(key))
                    {
                        roadmapData.Data.Udemy[key] = dto;
                    }
                    break;
                case Platforms.Coursera:
                    if (!roadmapData.Data.Coursera.ContainsKey(key))
                        roadmapData.Data.Coursera[course.Url] = dto;
                    break;
                case Platforms.Youtube:
                    roadmapData.Data.Youtube[course.Url] = dto;
                    break;
                default:
                    // Handle other platforms or ignore
                    break;
            }
        }

        return new APIResponse<RoadmapGenerationDTO>
        {
            StatusCode = HttpStatusCode.OK,
            Data = roadmapData
        };
    }

    public async Task<APIResponse> SaveRoadmapAsync(string customerId, RoadmapDTO request)
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
            RoadmapCourses = []
        };

        await _unitOfWork.GetRepo<Roadmap, int>().CreateAsync(roadmap);

        var allItems = CollectAllItems(request.RoadmapData);
        var orderedItems = OrderByLearningPath(allItems, request.RoadmapData.LearningPath);

        int orderCounter = 1;
        var processedCourses = new Dictionary<string, Course>();

        foreach (var (dto, sectionName) in orderedItems)
        {
            if (!processedCourses.TryGetValue(dto.Url, out var courseToLink))
            {
                var spec = new CourseByUrlSpecification(dto.Url);
                var existingCourse = await _unitOfWork.GetRepo<Course, int>().GetAsync(spec);

                if (existingCourse != null)
                {
                    courseToLink = existingCourse;
                }
                else
                {
                    Enum.TryParse<Platforms>(dto.Platform ?? sectionName, true, out var platformEnum);

                    courseToLink = new Course
                    {
                        Title = dto.Title,
                        Url = dto.Url,
                        Description = dto.Description,
                        ImageUrl = dto.ImageUrl,
                        Duration = dto.Duration,
                        Rating = dto.Score > 5 ? 5.0 : dto.Score,
                        Platform = platformEnum,
                        Language = ResourceLanguage.en,
                        RetrievedAt = DateTime.UtcNow
                    };

                    await _unitOfWork.GetRepo<Course, int>().CreateAsync(courseToLink);
                }

                processedCourses[dto.Url] = courseToLink;
            }

            var roadmapCourse = new RoadmapCourse
            {
                Course = courseToLink,
                Order = orderCounter++,
                SectionName = sectionName,
                IsCompleted = false
            };

            roadmap.RoadmapCourses.Add(roadmapCourse);
        }

        await _unitOfWork.SaveChangesAsync();

        return new APIResponse
        {
            StatusCode = HttpStatusCode.OK,
        };
    }

    public async Task<APIResponse> DeleteRoadmapAsync(int roadmapId, string userId)
    {
        var repo = _unitOfWork.GetRepo<Roadmap, int>();
        var roadmap = await repo.GetIdAsync(roadmapId) ?? throw new NotFoundException("Roadmap not found.");
        if (roadmap.CustomerId != userId)
            throw new UnauthorizedException("User is not authorized to delete this roadmap.");

        await repo.DeleteAsync(roadmap);
        await _unitOfWork.SaveChangesAsync();
        return new APIResponse
        {
            StatusCode = HttpStatusCode.OK,
        };
    }

    private static List<(RoadmapItemDTO Item, string Platform, string TagKey)> CollectAllItems(RoadmapGenerationDTO roadmapData)
    {
        var items = new List<(RoadmapItemDTO, string, string)>();

        AddPlatformItems(items, roadmapData.Data.Udemy, "Udemy");
        AddPlatformItems(items, roadmapData.Data.Coursera, "Coursera");
        AddPlatformItems(items, roadmapData.Data.Youtube, "Youtube");

        return items;
    }

    private static void AddPlatformItems(
        List<(RoadmapItemDTO, string, string)> target,
        Dictionary<string, RoadmapItemDTO?>? source,
        string platformName)
    {
        if (source == null) return;

        foreach (var (tagKey, item) in source)
        {
            if (item != null)
                target.Add((item, platformName, tagKey));
        }
    }

    private static List<(RoadmapItemDTO Item, string SectionName)> OrderByLearningPath(
        List<(RoadmapItemDTO Item, string Platform, string TagKey)> allItems,
        LearningPathDTO? learningPath)
    {
        if (learningPath?.Phases == null || learningPath.Phases.Count == 0)
            return [.. allItems.Select(x => (x.Item, x.Platform))];

        // Build tag→position from the learning path's sorted phases
        var tagOrder = new Dictionary<string, (int Order, string PhaseName)>(StringComparer.OrdinalIgnoreCase);
        int position = 0;
        foreach (var phase in learningPath.Phases)
        {
            foreach (var tagInfo in phase.Tags)
            {
                if (!tagOrder.ContainsKey(tagInfo.Tag))
                    tagOrder[tagInfo.Tag] = (position++, phase.Name);
            }
        }

        int fallbackOrder = position;

        return [.. allItems
            .Select(x =>
            {
                string sectionName = x.Platform;
                int order = fallbackOrder++;

                // Match by exact dictionary key (tag name from Pipeline)
                if (tagOrder.TryGetValue(x.TagKey, out var info))
                {
                    order = info.Order;
                    sectionName = info.PhaseName;
                }

                return (x.Item, SectionName: sectionName, Order: order);
            })
            .OrderBy(x => x.Order)
            .Select(x => (x.Item, x.SectionName))];
    }
}