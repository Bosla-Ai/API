using Domain.Contracts;
using Domain.Entities;
using FluentAssertions;
using Moq;
using Service.Implementations;

namespace BoslaAPI.Tests.Services;

public class ApplicationUserServiceTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IGenericRepository<ApplicationUser, string>> _userRepoMock;
    private readonly ApplicationUserService _applicationUserService;

    public ApplicationUserServiceTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _userRepoMock = new Mock<IGenericRepository<ApplicationUser, string>>();

        _unitOfWorkMock
            .Setup(u => u.GetRepo<ApplicationUser, string>())
            .Returns(_userRepoMock.Object);

        _applicationUserService = new ApplicationUserService(_unitOfWorkMock.Object);
    }

    #region GetAllAsync Tests

    [Fact]
    public async Task GetAllAsync_ReturnsAllUsers()
    {
        // Arrange
        var users = new List<ApplicationUser>
        {
            new ApplicationUser { Id = "1", Email = "user1@test.com" },
            new ApplicationUser { Id = "2", Email = "user2@test.com" }
        };

        _userRepoMock
            .Setup(r => r.GetAllAsync(null))
            .ReturnsAsync(users);

        // Act
        var result = await _applicationUserService.GetAllAsync();

        // Assert
        result.Should().HaveCount(2);
        result.Should().BeEquivalentTo(users);
    }

    [Fact]
    public async Task GetAllAsync_WhenNoUsers_ReturnsEmptyList()
    {
        // Arrange
        _userRepoMock
            .Setup(r => r.GetAllAsync(null))
            .ReturnsAsync(new List<ApplicationUser>());

        // Act
        var result = await _applicationUserService.GetAllAsync();

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region GetByIdAsync Tests

    [Fact]
    public async Task GetByIdAsync_WithValidId_ReturnsUser()
    {
        // Arrange
        var userId = "user-123";
        var user = new ApplicationUser { Id = userId, Email = "test@example.com" };

        _userRepoMock
            .Setup(r => r.GetIdAsync(userId))
            .ReturnsAsync(user);

        // Act
        var result = await _applicationUserService.GetByIdAsync(userId);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(userId);
    }

    #endregion
}
