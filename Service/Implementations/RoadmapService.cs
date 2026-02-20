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

    public async Task<APIResponse<int>> SaveRoadmapAsync(string customerId, RoadmapDTO request)
    {
        if (request == null || request.RoadmapData == null)
            throw new BadRequestException("Invalid Roadmap Generation Payload.");

        var customerRepo = _unitOfWork.GetRepo<Customer, string>();
        var existingCustomer = await customerRepo.GetIdAsync(customerId);
        if (existingCustomer == null)
        {
            // Auto-create customer profile if it doesn't exist for this ApplicationUser
            existingCustomer = new Customer
            {
                ApplicationUserId = customerId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await customerRepo.CreateAsync(existingCustomer);
            await _unitOfWork.SaveChangesAsync(); // Ensure FK is available for Roadmap
        }

        var roadmap = new Roadmap
        {
            CustomerId = customerId,
            Title = string.IsNullOrWhiteSpace(request.Title) ? "Roadmap" : (request.Title.Length > 200 ? request.Title[..200] : request.Title),
            Description = request.Description?.Length > 1000 ? request.Description[..1000] : request.Description,
            SourceType = request.SourceType,
            TargetJobRole = request.TargetJobRole?.Length > 200 ? request.TargetJobRole[..200] : request.TargetJobRole,
            CreatedAt = DateTime.UtcNow,
            IsArchived = false,
            RoadmapCourses = []
        };

        await _unitOfWork.GetRepo<Roadmap, int>().CreateAsync(roadmap);

        var allItems = CollectAllItems(request.RoadmapData);
        var orderedItems = OrderByLearningPath(allItems, request.RoadmapData.LearningPath);

        var addedCourseUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var processedCourses = new Dictionary<string, Course>(StringComparer.OrdinalIgnoreCase);
        if (orderedItems.Count != 0)
        {
            var urls = orderedItems.Select(x => x.Item.Url?.Trim()).Where(u => !string.IsNullOrEmpty(u)).ToList();
            if (urls.Count > 0)
            {
                var spec = new CourseByUrlSpecification(urls);
                var coursesInDb = await _unitOfWork.GetRepo<Course, int>().GetAllAsync(spec);
                processedCourses = coursesInDb.ToDictionary(c => c.Url, c => c, StringComparer.OrdinalIgnoreCase);
            }
        }

        int index = 1;
        foreach (var (dto, sName) in orderedItems)
        {
            var url = dto.Url?.Trim();
            if (string.IsNullOrWhiteSpace(url) || !addedCourseUrls.Add(url))
            {
                continue; // Skip duplicate or invalid courses to prevent tracking exception
            }
            if (url.Length > 1000) url = url[..1000];

            if (!processedCourses.TryGetValue(url, out var courseToLink))
            {
                var sectionName = sName?.Trim();
                Enum.TryParse<Platforms>(dto.Platform ?? sectionName, true, out var platformEnum);

                courseToLink = new Course
                {
                    Title = string.IsNullOrWhiteSpace(dto.Title) ? "Unknown" : (dto.Title.Length > 500 ? dto.Title[..500] : dto.Title),
                    Description = dto.Description?.Length > 1000 ? dto.Description[..1000] : dto.Description,
                    Url = url,
                    Instructor = null,
                    ImageUrl = dto.ImageUrl?.Length > 1000 ? dto.ImageUrl[..1000] : dto.ImageUrl,
                    Duration = dto.Duration?.Length > 50 ? dto.Duration[..50] : dto.Duration,
                    Rating = dto.Score > 5 ? 5.0 : dto.Score,
                    ReviewCount = 0,
                    Platform = platformEnum,
                    Language = ResourceLanguage.en,
                    CourseBudget = string.Equals(dto.Price, "Paid", StringComparison.OrdinalIgnoreCase)
                        ? BudgetPreference.Paid
                        : BudgetPreference.Free,
                    RetrievedAt = DateTime.UtcNow
                };

                await _unitOfWork.GetRepo<Course, int>().CreateAsync(courseToLink);
                processedCourses[url] = courseToLink; // Add specifically to track freshly added courses
                await _unitOfWork.SaveChangesAsync(); // save immediately per course
            }

            var safeSectionName = sName?.Length > 200 ? sName[..200] : sName;
            var roadmapCourse = new RoadmapCourse
            {
                RoadmapId = roadmap.Id,
                CourseId = courseToLink.Id,
                Order = index++,
                SectionName = safeSectionName,
                IsCompleted = false
            };

            roadmap.RoadmapCourses.Add(roadmapCourse);
        }

        await _unitOfWork.SaveChangesAsync();

        return new APIResponse<int>
        {
            StatusCode = HttpStatusCode.Created,
            Data = roadmap.Id
        };
    }

    public async Task<APIResponse> DeleteRoadmapAsync(int roadmapId, string userId)
    {
        var repo = _unitOfWork.GetRepo<Roadmap, int>();
        var roadmap = await repo.GetIdAsync(roadmapId) ?? throw new NotFoundException("Roadmap not found.");
        if (roadmap.CustomerId != userId)
            throw new UnauthorizedException("User is not authorized to delete this roadmap.");

        roadmap.IsArchived = true;
        await repo.UpdateAsync(roadmap);
        await _unitOfWork.SaveChangesAsync();
        return new APIResponse
        {
            StatusCode = HttpStatusCode.OK,
        };
    }

    public async Task<APIResponse<IEnumerable<RoadmapListResponseDTO>>> GetAllUserRoadmapsAsync(string userId)
    {
        var spec = new RoadmapsByCustomerSpecification(userId);
        var roadmaps = await _unitOfWork.GetRepo<Roadmap, int>().GetAllAsync(spec);

        var result = roadmaps.OrderByDescending(r => r.CreatedAt).Select(r => new RoadmapListResponseDTO
        {
            Id = r.Id,
            Title = r.Title,
            Description = r.Description,
            SourceType = r.SourceType,
            TargetJobRole = r.TargetJobRole,
            CreatedAt = r.CreatedAt
        });

        return new APIResponse<IEnumerable<RoadmapListResponseDTO>>
        {
            StatusCode = HttpStatusCode.OK,
            Data = result
        };
    }

    public async Task<APIResponse<RoadmapDetailsResponseDTO>> GetRoadmapDetailsAsync(int roadmapId, string userId)
    {
        var spec = new RoadmapsByCustomerSpecification(roadmapId, userId);
        var roadmap = await _unitOfWork.GetRepo<Roadmap, int>().GetAsync(spec) ?? throw new NotFoundException("Roadmap not found.");
        var details = new RoadmapDetailsResponseDTO
        {
            Id = roadmap.Id,
            Title = roadmap.Title,
            Description = roadmap.Description,
            SourceType = roadmap.SourceType,
            TargetJobRole = roadmap.TargetJobRole,
            CreatedAt = roadmap.CreatedAt,
            Courses = roadmap.RoadmapCourses?.OrderBy(rc => rc.Order).Select(rc => new RoadmapCourseResponseDTO
            {
                CourseId = rc.CourseId,
                Title = rc.Course?.Title ?? "Unknown",
                Description = rc.Course?.Description,
                Url = rc.Course?.Url ?? string.Empty,
                Instructor = rc.Course?.Instructor,
                Platform = rc.Course?.Platform ?? Platforms.Youtube,
                ImageUrl = rc.Course?.ImageUrl,
                Duration = rc.Course?.Duration,
                Rating = rc.Course?.Rating ?? 0,
                Language = rc.Course?.Language ?? ResourceLanguage.en,
                CourseBudget = rc.Course?.CourseBudget,
                Order = rc.Order,
                SectionName = rc.SectionName,
                IsCompleted = rc.IsCompleted,
                CompletedAt = rc.CompletedAt
            }).ToList() ?? []
        };

        return new APIResponse<RoadmapDetailsResponseDTO>
        {
            StatusCode = HttpStatusCode.OK,
            Data = details
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
