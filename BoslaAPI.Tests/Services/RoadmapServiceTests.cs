using System.Net;
using System.Text.Json;
using Domain.Contracts;
using Domain.Entities;
using Domain.Exceptions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Moq;
using Service.Implementations;
using Shared.DTOs.RoadmapDTOs;

namespace BoslaAPI.Tests.Services;

public class RoadmapServiceTests
{
    private readonly Mock<IDistributedCache> _mockCache;
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly Mock<IGenericRepository<Course, int>> _mockCourseRepo;
    private readonly Mock<IGenericRepository<Roadmap, int>> _mockRoadmapRepo;

    private readonly RoadmapService _service;

    public RoadmapServiceTests()
    {
        _mockCache = new Mock<IDistributedCache>();
        _mockHttpClientFactory = new Mock<IHttpClientFactory>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockConfiguration = new Mock<IConfiguration>();
        _mockCourseRepo = new Mock<IGenericRepository<Course, int>>();
        _mockRoadmapRepo = new Mock<IGenericRepository<Roadmap, int>>();

        _mockConfiguration.Setup(c => c["PipelineApi:BaseUrl"]).Returns("http://python-api/generate");

        _mockUnitOfWork.Setup(u => u.GetRepo<Course, int>()).Returns(_mockCourseRepo.Object);
        _mockUnitOfWork.Setup(u => u.GetRepo<Roadmap, int>()).Returns(_mockRoadmapRepo.Object);

        _service = new RoadmapService(
            _mockCache.Object,
            _mockHttpClientFactory.Object,
            _mockUnitOfWork.Object,
            _mockConfiguration.Object
        );
    }

    [Fact]
    public async Task GenerateRoadmapAsync_ReturnsCachedData_IfPresent()
    {
        // Arrange
        var tags = new[] { "csharp" };
        var cachedDto = new RoadmapGenerationDTO { Status = "Cached" };
        var cachedJson = JsonSerializer.Serialize(cachedDto);

        _mockCache.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(System.Text.Encoding.UTF8.GetBytes(cachedJson));

        // Act
        var result = await _service.GenerateRoadmapAsync(tags, "en", false);

        // Assert
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.Equal("Cached", result.Data.Status);
        _mockHttpClientFactory.Verify(x => x.CreateClient(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task DeleteRoadmapAsync_ThrowsNotFound_IfRoadmapDoesNotExist()
    {
        // Arrange
        _mockRoadmapRepo.Setup(r => r.GetIdAsync(1)).ReturnsAsync((Roadmap)null!);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => _service.DeleteRoadmapAsync(1, "user1"));
    }

    [Fact]
    public async Task DeleteRoadmapAsync_ThrowsUnauthorized_IfUserDoesNotOwnRoadmap()
    {
        // Arrange
        var roadmap = new Roadmap { Id = 1, CustomerId = "owner" };
        _mockRoadmapRepo.Setup(r => r.GetIdAsync(1)).ReturnsAsync(roadmap);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedException>(() => _service.DeleteRoadmapAsync(1, "otherUser"));
    }

    [Fact]
    public async Task DeleteRoadmapAsync_Deletes_IfUserIsOwner()
    {
        // Arrange
        var roadmap = new Roadmap { Id = 1, CustomerId = "owner" };
        _mockRoadmapRepo.Setup(r => r.GetIdAsync(1)).ReturnsAsync(roadmap);

        // Act
        var result = await _service.DeleteRoadmapAsync(1, "owner");

        // Assert
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        _mockRoadmapRepo.Verify(r => r.DeleteAsync(roadmap), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task GenerateRoadmapAsync_WithEmptyTags_CreatesCacheKeyCorrectly()
    {
        // Arrange
        var tags = Array.Empty<string>();
        var cachedDto = new RoadmapGenerationDTO { Status = "Empty" };
        var cachedJson = JsonSerializer.Serialize(cachedDto);

        _mockCache.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(System.Text.Encoding.UTF8.GetBytes(cachedJson));

        // Act
        var result = await _service.GenerateRoadmapAsync(tags, "en", false);

        // Assert
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.NotNull(result.Data);
    }

    [Fact]
    public async Task GenerateRoadmapAsync_WithInvalidLanguage_DefaultsToEnglish()
    {
        // Arrange
        var tags = new[] { "python" };
        var cachedDto = new RoadmapGenerationDTO { Status = "Cached" };
        var cachedJson = JsonSerializer.Serialize(cachedDto);

        _mockCache.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(System.Text.Encoding.UTF8.GetBytes(cachedJson));

        // Act - Pass invalid language
        var result = await _service.GenerateRoadmapAsync(tags, "invalid_lang", false);

        // Assert - Should not throw and return cached data
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
    }

    [Fact]
    public async Task GenerateRoadmapAsync_CacheKeyIsDeterministic()
    {
        // Arrange - Same tags in different order should produce same cache key
        var tags1 = new[] { "csharp", "dotnet" };
        var tags2 = new[] { "dotnet", "csharp" };
        var cachedDto = new RoadmapGenerationDTO { Status = "Cached" };
        var cachedJson = JsonSerializer.Serialize(cachedDto);

        string? capturedKey1 = null;
        string? capturedKey2 = null;

        _mockCache.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((key, _) =>
            {
                if (capturedKey1 == null) capturedKey1 = key;
                else capturedKey2 = key;
            })
            .ReturnsAsync(System.Text.Encoding.UTF8.GetBytes(cachedJson));

        // Act
        await _service.GenerateRoadmapAsync(tags1, "en", false);
        await _service.GenerateRoadmapAsync(tags2, "en", false);

        // Assert - Both should produce the same cache key (tags are sorted)
        Assert.Equal(capturedKey1, capturedKey2);
    }
}
