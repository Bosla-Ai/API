using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Service.Abstraction;
using Service.Implementations;
using Shared.DTOs;

namespace BoslaAPI.Tests.Services;

public class ChatHistoryServiceTests
{
    private readonly Mock<IChatRepository> _chatRepoMock;
    private readonly ChatHistoryService _sut;

    public ChatHistoryServiceTests()
    {
        _chatRepoMock = new Mock<IChatRepository>();
        var loggerMock = new Mock<ILogger<ChatHistoryService>>();
        _sut = new ChatHistoryService(_chatRepoMock.Object, loggerMock.Object);
    }

    [Fact]
    public async Task GetUserChatSessionsAsync_ReturnsSortedSessionsByRecency()
    {
        // Arrange
        var messages = new List<ChatMessageEntity>
        {
            new() { UserId = "u1", SessionId = "s1", Message = "Hello", Role = "user", CreatedAt = DateTime.UtcNow.AddHours(-2) },
            new() { UserId = "u1", SessionId = "s1", Message = "Hi there!", Role = "assistant", CreatedAt = DateTime.UtcNow.AddHours(-1) },
            new() { UserId = "u1", SessionId = "s2", Message = "Recent chat", Role = "user", CreatedAt = DateTime.UtcNow },
        };

        _chatRepoMock
            .Setup(r => r.GetAllUserMessagesAsync("u1"))
            .ReturnsAsync(messages);

        // Act
        var response = await _sut.GetUserChatSessionsAsync("u1");
        var result = response.Data!;

        // Assert
        result.Should().HaveCount(2);
        result[0].SessionId.Should().Be("s2");
        result[1].SessionId.Should().Be("s1");
        result[0].MessageCount.Should().Be(1);
        result[1].MessageCount.Should().Be(2);
    }

    [Fact]
    public async Task GetUserChatSessionsAsync_ReturnsEmptyWhenNoMessages()
    {
        // Arrange
        _chatRepoMock
            .Setup(r => r.GetAllUserMessagesAsync("u1"))
            .ReturnsAsync(new List<ChatMessageEntity>());

        // Act
        var response = await _sut.GetUserChatSessionsAsync("u1");

        // Assert
        response.Data.Should().BeEmpty();
    }

    [Fact]
    public async Task GetSessionMessagesAsync_ReturnsMessagesForSession()
    {
        // Arrange
        var messages = new List<ChatMessageEntity>
        {
            new() { UserId = "u1", SessionId = "s1", Message = "Hello", Role = "user", CreatedAt = DateTime.UtcNow.AddMinutes(-2) },
            new() { UserId = "u1", SessionId = "s1", Message = "Hi!", Role = "assistant", CreatedAt = DateTime.UtcNow.AddMinutes(-1) },
        };

        _chatRepoMock
            .Setup(r => r.GetMessagesAsync("u1", "s1", 100))
            .ReturnsAsync(messages);

        // Act
        var response = await _sut.GetSessionMessagesAsync("u1", "s1");
        var result = response.Data!;

        // Assert
        result.SessionId.Should().Be("s1");
        result.Messages.Should().HaveCount(2);
        result.Messages[0].Role.Should().Be("user");
        result.Messages[1].Role.Should().Be("assistant");
    }

    [Fact]
    public async Task GetSessionMessagesAsync_ReturnsEmptyForNonExistentSession()
    {
        // Arrange
        _chatRepoMock
            .Setup(r => r.GetMessagesAsync("u1", "none", 100))
            .ReturnsAsync(new List<ChatMessageEntity>());

        // Act
        var response = await _sut.GetSessionMessagesAsync("u1", "none");
        var result = response.Data!;

        // Assert
        result.SessionId.Should().Be("none");
        result.Messages.Should().BeEmpty();
    }

    [Fact]
    public async Task CleanInactiveChatsAsync_PassesDualCutoffsToRepository()
    {
        // Arrange
        _chatRepoMock
            .Setup(r => r.DeleteInactiveMessagesAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(42);

        // Act
        var result = await _sut.CleanInactiveChatsAsync(7, 3);

        // Assert
        result.Should().Be(42);
        _chatRepoMock.Verify(r => r.DeleteInactiveMessagesAsync(
            It.Is<DateTime>(d => d < DateTime.UtcNow.AddDays(-6) && d > DateTime.UtcNow.AddDays(-8)),
            It.Is<DateTime>(d => d < DateTime.UtcNow.AddDays(-2) && d > DateTime.UtcNow.AddDays(-4))),
            Times.Once);
    }

    [Fact]
    public async Task GetUserChatSessionsAsync_TruncatesLongMessagePreviews()
    {
        // Arrange
        var longMessage = new string('x', 200);
        var messages = new List<ChatMessageEntity>
        {
            new() { UserId = "u1", SessionId = "s1", Message = longMessage, Role = "user", CreatedAt = DateTime.UtcNow },
        };

        _chatRepoMock
            .Setup(r => r.GetAllUserMessagesAsync("u1"))
            .ReturnsAsync(messages);

        // Act
        var response = await _sut.GetUserChatSessionsAsync("u1");
        var result = response.Data!;

        // Assert
        result[0].LastMessagePreview.Length.Should().BeLessThanOrEqualTo(83); // 80 chars + "..."
        result[0].LastMessagePreview.Should().EndWith("...");
    }
}
