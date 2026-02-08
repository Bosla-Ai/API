using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Moq;
using Persistence.Data.Contexts;
using Persistence.Repositories;
using Service.Implementations;
using Shared.DTOs.RoadmapDTOs;
using Shared.Enums;
using Shared.Options;
using Microsoft.Extensions.Options;

namespace BoslaAPI.Tests.Integration;

public class RoadmapServiceIntegrationTests : IDisposable
{
    private readonly ApplicationDbContext _dbContext;
    private readonly UnitOfWork _unitOfWork;
    private readonly RoadmapService _service;

    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
    private readonly IOptions<AiOptions> _options;

    public RoadmapServiceIntegrationTests()
    {
        // Use a unique database name for each test instance to ensure isolation
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new ApplicationDbContext(options);
        _unitOfWork = new UnitOfWork(_dbContext);

        _mockHttpClientFactory = new Mock<IHttpClientFactory>();

        var aiOptions = new AiOptions { PipelineApi = new PipelineApiOptions { BaseUrl = "http://test-api" } };
        _options = Options.Create(aiOptions);

        _service = new RoadmapService(
            _mockHttpClientFactory.Object,
            _unitOfWork,
            _options
        );
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    [Fact]
    public async Task SaveRoadmapAsync_PersistsNewCoursesAndRoadmap_Successfully()
    {
        // Arrange
        var customerId = "cust-1";
        var roadmapReq = new RoadmapDTO
        {
            Title = "My Learning Path",
            Description = "A master plan",
            RoadmapData = new RoadmapGenerationDTO
            {
                Data = new RoadmapSourcesDTO
                {
                    Udemy = new Dictionary<string, RoadmapItemDTO>
                    {
                        ["key1"] = new RoadmapItemDTO
                        {
                            Title = "C# Masterclass",
                            Url = "https://udemy.com/csharp",
                            Platform = "Udemy",
                            Price = "10.0",
                            Score = 4.8
                        }
                    },
                    Coursera = new Dictionary<string, RoadmapItemDTO>()
                }
            }
        };

        // Act
        var response = await _service.SaveRoadmapAsync(customerId, roadmapReq);

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);

        // Verify Roadmap saved
        var savedRoadmap = await _dbContext.Set<Roadmap>()
            .Include(r => r.RoadmapCourses)
            .ThenInclude(rc => rc.Course)
            .FirstOrDefaultAsync();

        Assert.NotNull(savedRoadmap);
        Assert.Equal("My Learning Path", savedRoadmap.Title);
        Assert.Equal("cust-1", savedRoadmap.CustomerId);
        Assert.Single(savedRoadmap.RoadmapCourses);

        var rc = savedRoadmap.RoadmapCourses.First();
        Assert.Equal("Udemy", rc.SectionName);
        Assert.NotNull(rc.Course);
        Assert.Equal("C# Masterclass", rc.Course.Title);
        Assert.Equal("https://udemy.com/csharp", rc.Course.Url);
    }

    [Fact]
    public async Task SaveRoadmapAsync_ReusesExistingCourse_IfUrlMatches()
    {
        // Arrange - Seed an existing course
        var existingCourse = new Course
        {
            Title = "Existing C# Course",
            Url = "https://udemy.com/csharp", // Same URL
            Platform = Platforms.Udemy,
            Description = "Old desc",
            RetrievedAt = DateTime.UtcNow
        };
        _dbContext.Courses.Add(existingCourse);
        await _dbContext.SaveChangesAsync();

        var customerId = "cust-2";
        var roadmapReq = new RoadmapDTO
        {
            Title = "Advanced Path",
            RoadmapData = new RoadmapGenerationDTO
            {
                Data = new RoadmapSourcesDTO
                {
                    Udemy = new Dictionary<string, RoadmapItemDTO>
                    {
                        ["key1"] = new RoadmapItemDTO
                        {
                            Title = "C# New Title", // Should still link to existing URL
                            Url = "https://udemy.com/csharp",
                            Platform = "Udemy",
                            Price = "12.0",
                            Score = 4.9
                        }
                    }
                }
            }
        };

        // Act
        await _service.SaveRoadmapAsync(customerId, roadmapReq);

        // Assert
        var coursesCount = await _dbContext.Courses.CountAsync();
        Assert.Equal(1, coursesCount); // Should NOT have created a second course

        var savedRoadmap = await _dbContext.Set<Roadmap>()
            .Include(r => r.RoadmapCourses)
            .ThenInclude(rc => rc.Course) // Ensure we load the relation
            .FirstOrDefaultAsync(r => r.CustomerId == customerId);

        Assert.NotNull(savedRoadmap);
        Assert.Single(savedRoadmap.RoadmapCourses);
        Assert.Equal(existingCourse.Id, savedRoadmap.RoadmapCourses.First().Course.Id);
    }
}
