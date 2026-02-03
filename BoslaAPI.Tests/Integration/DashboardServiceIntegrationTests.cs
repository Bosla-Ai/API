using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Persistence.Data.Contexts;
using Persistence.Repositories;
using Service.Implementations;

namespace BoslaAPI.Tests.Integration;

public class DashboardServiceIntegrationTests : IDisposable
{
    private readonly ApplicationDbContext _dbContext;
    private readonly UnitOfWork _unitOfWork;
    private readonly DashboardService _service;

    public DashboardServiceIntegrationTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new ApplicationDbContext(options);
        _unitOfWork = new UnitOfWork(_dbContext);

        _service = new DashboardService(_unitOfWork);
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    [Fact]
    public async Task GetDashboardDataAsync_CalculatesOnlineUsers_DistinctByUserId()
    {
        // Arrange
        var userId1 = "user-1";
        var userId2 = "user-2";

        // User 1 has 3 active sessions (tokens)
        _dbContext.RefreshTokens.AddRange(
            CreateToken(userId1, DateTime.UtcNow.AddHours(1)),
            CreateToken(userId1, DateTime.UtcNow.AddHours(2)),
            CreateToken(userId1, DateTime.UtcNow.AddHours(3))
        );

        // User 2 has 1 active session
        _dbContext.RefreshTokens.Add(
            CreateToken(userId2, DateTime.UtcNow.AddHours(1))
        );

        // User 3 has EXPIRED sessions only (should ignored)
        _dbContext.RefreshTokens.Add(
            CreateToken("user-3", DateTime.UtcNow.AddHours(-1))
        );

        // User 4 has REVOKED sessions only (should be ignored)
        var revoked = CreateToken("user-4", DateTime.UtcNow.AddHours(1));
        revoked.IsRevoked = true;
        _dbContext.RefreshTokens.Add(revoked);

        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetDashboardDataAsync();

        // Assert
        // Should be 2 (User1 and User2). User3 is expired, User4 is revoked.
        // User1 is counted ONCE despite 3 tokens.
        Assert.Equal(2, result.Data.OnlineUsersCount);
    }

    private RefreshToken CreateToken(string userId, DateTime expires, bool isRevoked = false)
    {
        return new RefreshToken
        {
            UserId = userId,
            TokenHash = Guid.NewGuid().ToString(),
            TokenSalt = "salt",
            Created = DateTime.UtcNow,
            ExpiresAt = expires,
            IsRevoked = isRevoked,
            DeviceId = Guid.NewGuid()
        };
    }
}
