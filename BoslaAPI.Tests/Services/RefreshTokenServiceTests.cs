using Domain.Contracts;
using Domain.Entities;
using Domain.ModelsSpecifications;
using FluentAssertions;
using Moq;
using Service.Implementations;
using Shared.Parameters;

namespace BoslaAPI.Tests.Services;

public class RefreshTokenServiceTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IGenericRepository<RefreshToken, Guid>> _refreshTokenRepoMock;
    private readonly RefreshTokenService _refreshTokenService;

    public RefreshTokenServiceTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _refreshTokenRepoMock = new Mock<IGenericRepository<RefreshToken, Guid>>();

        _unitOfWorkMock
            .Setup(u => u.GetRepo<RefreshToken, Guid>())
            .Returns(_refreshTokenRepoMock.Object);

        _refreshTokenService = new RefreshTokenService(_unitOfWorkMock.Object);
    }

    #region GetAllForUserDeviceNotRevokedAsync Tests

    [Fact]
    public async Task GetAllForUserDeviceNotRevokedAsync_ReturnsTokens()
    {
        // Arrange
        var parameters = new RefreshTokenParameters { UserId = "user-123" };
        var tokens = new List<RefreshToken>
        {
            new RefreshToken { Id = 1, UserId = "user-123" },
            new RefreshToken { Id = 2, UserId = "user-123" }
        };

        _refreshTokenRepoMock
            .Setup(r => r.GetAllAsync(It.IsAny<RefreshTokenSpecification>()))
            .ReturnsAsync(tokens);

        // Act
        var result = await _refreshTokenService.GetAllForUserDeviceNotRevokedAsync(parameters);

        // Assert
        result.Should().HaveCount(2);
    }

    #endregion

    #region CreateAsync Tests

    [Fact]
    public async Task CreateAsync_CallsRepositoryCreate()
    {
        // Arrange
        var refreshToken = new RefreshToken
        {
            Id = 1,
            UserId = "user-123",
            TokenHash = "hash",
            TokenSalt = "salt"
        };

        // Act
        await _refreshTokenService.CreateAsync(refreshToken);

        // Assert
        _refreshTokenRepoMock.Verify(r => r.CreateAsync(refreshToken), Times.Once);
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task UpdateAsync_CallsRepositoryUpdate()
    {
        // Arrange
        var refreshToken = new RefreshToken
        {
            Id = 1,
            IsRevoked = true,
            RevokedReason = "User logged out"
        };

        // Act
        await _refreshTokenService.UpdateAsync(refreshToken);

        // Assert
        _refreshTokenRepoMock.Verify(r => r.UpdateAsync(refreshToken), Times.Once);
    }

    #endregion

    #region DeleteAsync Tests

    [Fact]
    public async Task DeleteAsync_CallsRepositoryDelete()
    {
        // Arrange
        var refreshToken = new RefreshToken { Id = 1 };

        // Act
        await _refreshTokenService.DeleteAsync(refreshToken);

        // Assert
        _refreshTokenRepoMock.Verify(r => r.DeleteAsync(refreshToken), Times.Once);
    }

    #endregion

    #region GetWithDeviceIdNotRevokedAsync Tests

    [Fact]
    public async Task GetWithDeviceIdNotRevokedAsync_ReturnsToken()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var parameters = new RefreshTokenParameters { DeviceId = deviceId };
        var token = new RefreshToken { Id = 1, DeviceId = deviceId };

        _refreshTokenRepoMock
            .Setup(r => r.GetAsync(It.IsAny<RefreshTokenSpecification>()))
            .ReturnsAsync(token);

        // Act
        var result = await _refreshTokenService.GetWithDeviceIdNotRevokedAsync(parameters);

        // Assert
        result.Should().NotBeNull();
        result.DeviceId.Should().Be(deviceId);
    }

    #endregion
}
