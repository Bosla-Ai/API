using System.Net;
using System.Text.Json;
using Domain.Contracts;
using Domain.Entities;
using Microsoft.Extensions.Caching.Distributed;
using Moq;
using Moq.Protected;
using Service.Implementations;
using Shared.DTOs.RoadmapDTOs;
using Microsoft.Extensions.Options;
using Shared.Options;

namespace BoslaAPI.Tests;

public class ApplicationLayerTests
{
    private readonly Mock<IDistributedCache> _mockCache;
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly IOptions<AiOptions> _options;
    private readonly Mock<IGenericRepository<Course, int>> _mockCourseRepo;
    private readonly RoadmapService _roadmapService;

    public ApplicationLayerTests()
    {
        _mockCache = new Mock<IDistributedCache>();
        _mockHttpClientFactory = new Mock<IHttpClientFactory>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockCourseRepo = new Mock<IGenericRepository<Course, int>>();

        var aiOptions = new AiOptions { PipelineApi = new PipelineApiOptions { BaseUrl = "http://test-api/generate-roadmap" } };
        _options = Options.Create(aiOptions);

        // Setup the repository mock
        _mockUnitOfWork.Setup(u => u.GetRepo<Course, int>()).Returns(_mockCourseRepo.Object);
        _mockCourseRepo.Setup(r => r.GetAllAsync(It.IsAny<Specifications<Course>>()))
            .ReturnsAsync(new List<Course>());

        _roadmapService = new RoadmapService(
            _mockCache.Object,
            _mockHttpClientFactory.Object,
            _mockUnitOfWork.Object,
            _options
        );
    }

    [Fact]
    public async Task GenerateRoadmapAsync_ReturnsCachedData_IfPresent()
    {
        // Arrange
        var tags = new[] { "csharp", "dotnet" };
        var language = "en";
        var preferPaid = false;

        var cachedDto = new RoadmapGenerationDTO { Status = "Cached" };
        var cachedJson = JsonSerializer.Serialize(cachedDto);

        // Mock GetAsync which is called by GetStringAsync extension method
        _mockCache.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(System.Text.Encoding.UTF8.GetBytes(cachedJson));

        // Act
        var result = await _roadmapService.GenerateRoadmapAsync(tags, language, preferPaid);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.NotNull(result.Data);
        Assert.Equal("Cached", result.Data.Status);
        _mockHttpClientFactory.Verify(x => x.CreateClient(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GenerateRoadmapAsync_CallsApi_WhenCacheMiss()
    {
        // Arrange
        var tags = new[] { "python" };

        _mockCache.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[])null!);

        var expectedResponse = new RoadmapGenerationDTO { Status = "Fresh" };
        var httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(expectedResponse))
            });

        var httpClient = new HttpClient(httpMessageHandlerMock.Object);
        _mockHttpClientFactory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

        // Act
        var result = await _roadmapService.GenerateRoadmapAsync(tags, "en", true);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.NotNull(result.Data);
        Assert.Equal("Fresh", result.Data.Status);
        _mockCache.Verify(c => c.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
