using System.Net;
using System.Text.Json;
using Domain.Contracts;
using Domain.Entities;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Moq;
using Moq.Protected;
using Service.Implementations;
using Shared.DTOs.RoadmapDTOs;
using Xunit;

namespace BoslaAPI.Tests;

public class ApplicationLayerTests
{
    private readonly Mock<IDistributedCache> _mockCache;
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly RoadmapService _roadmapService;

    public ApplicationLayerTests()
    {
        _mockCache = new Mock<IDistributedCache>();
        _mockHttpClientFactory = new Mock<IHttpClientFactory>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockConfiguration = new Mock<IConfiguration>();

        _mockConfiguration.Setup(c => c["PipelineApi:BaseUrl"]).Returns("http://test-api/generate-roadmap");

        _roadmapService = new RoadmapService(
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
        var tags = new[] { "csharp", "dotnet" };
        var level = "beginner";
        var language = "en";
        var preferPaid = false;
        var cacheKey = "roadmap-csharp-dotnet-beginner-en-False";

        var cachedDto = new RoadmapGenerationDTO { Status = "Cached" };
        var cachedJson = JsonSerializer.Serialize(cachedDto);

        _mockCache.Setup(c => c.GetAsync(cacheKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(System.Text.Encoding.UTF8.GetBytes(cachedJson));

        // Act
        var result = await _roadmapService.GenerateRoadmapAsync(tags, level, language, preferPaid);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Cached", result.Status);
        _mockHttpClientFactory.Verify(x => x.CreateClient(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GenerateRoadmapAsync_CallsApi_WhenCacheMiss()
    {
        // Arrange
        var tags = new[] { "python" };
        var level = "advanced";

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
        var result = await _roadmapService.GenerateRoadmapAsync(tags, level, "en", true);

        // Assert
        Assert.Equal("Fresh", result.Status);
        _mockCache.Verify(c => c.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
