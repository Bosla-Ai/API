using System.Net;
using System.Text.Json;
using Domain.Contracts;
using Domain.Entities;
using Moq;
using Moq.Protected;
using Service.Implementations;
using Shared.DTOs.RoadmapDTOs;
using Microsoft.Extensions.Options;
using Shared.Options;

namespace BoslaAPI.Tests;

public class ApplicationLayerTests
{
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly IOptions<AiOptions> _options;
    private readonly Mock<IGenericRepository<Course, int>> _mockCourseRepo;
    private readonly RoadmapService _roadmapService;

    public ApplicationLayerTests()
    {
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
            _mockHttpClientFactory.Object,
            _mockUnitOfWork.Object,
            _options
        );
    }



    [Fact]
    public async Task GenerateRoadmapAsync_CallsApi()
    {
        // Arrange
        var tags = new[] { "python" };

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
    }
}
