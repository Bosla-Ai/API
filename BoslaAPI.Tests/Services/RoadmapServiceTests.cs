using System.Net;
using System.Text.Json;
using Domain.Contracts;
using Domain.Exceptions;
using Domain.Entities;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Service.Implementations;
using Shared.DTOs.RoadmapDTOs;
using Shared.Options;

namespace BoslaAPI.Tests.Services;

public class RoadmapServiceTests
{
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly IOptions<AiOptions> _options;
    private readonly Mock<IGenericRepository<Course, int>> _mockCourseRepo;
    private readonly Mock<IGenericRepository<Roadmap, int>> _mockRoadmapRepo;

    private readonly RoadmapService _service;

    public RoadmapServiceTests()
    {
        _mockHttpClientFactory = new Mock<IHttpClientFactory>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockCourseRepo = new Mock<IGenericRepository<Course, int>>();
        _mockRoadmapRepo = new Mock<IGenericRepository<Roadmap, int>>();

        var aiOptions = new AiOptions { PipelineApi = new PipelineApiOptions { BaseUrl = "http://python-api/generate" } };
        _options = Options.Create(aiOptions);

        _mockUnitOfWork.Setup(u => u.GetRepo<Course, int>()).Returns(_mockCourseRepo.Object);
        _mockUnitOfWork.Setup(u => u.GetRepo<Roadmap, int>()).Returns(_mockRoadmapRepo.Object);

        _service = new RoadmapService(
            _mockHttpClientFactory.Object,
            _mockUnitOfWork.Object,
            _options
        );
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
    public async Task GenerateRoadmapAsync_WithEmptyTags_ReturnsSuccess()
    {
        // Arrange
        var tags = Array.Empty<string>();

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

        // Act - Pass invalid language
        var result = await _service.GenerateRoadmapAsync(tags, "invalid_lang", false);

        // Assert - Should not throw and return cached data
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.Equal("Fresh", result.Data.Status);
    }


}
