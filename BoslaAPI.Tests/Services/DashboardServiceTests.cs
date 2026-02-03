using System.Linq.Expressions;
using System.Net;
using Domain.Contracts;
using Domain.Entities;
using Domain.ModelsSpecifications;
using Moq;
using Service.Implementations;

namespace BoslaAPI.Tests.Services;

public class DashboardServiceTests
{
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<IGenericRepository<Roadmap, int>> _mockRoadmapRepo;
    private readonly Mock<IGenericRepository<Customer, int>> _mockCustomerRepo;
    private readonly Mock<IGenericRepository<Course, int>> _mockCourseRepo;
    private readonly Mock<IGenericRepository<Domains, int>> _mockDomainsRepo;
    private readonly Mock<IGenericRepository<RefreshToken, int>> _mockRefreshTokenRepo;

    private readonly DashboardService _service;

    public DashboardServiceTests()
    {
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockRoadmapRepo = new Mock<IGenericRepository<Roadmap, int>>();
        _mockCustomerRepo = new Mock<IGenericRepository<Customer, int>>();
        _mockCourseRepo = new Mock<IGenericRepository<Course, int>>();
        _mockDomainsRepo = new Mock<IGenericRepository<Domains, int>>();
        _mockRefreshTokenRepo = new Mock<IGenericRepository<RefreshToken, int>>();

        // Setup generic UoW calls
        _mockUnitOfWork.Setup(u => u.GetRepo<Roadmap, int>()).Returns(_mockRoadmapRepo.Object);
        _mockUnitOfWork.Setup(u => u.GetRepo<Customer, int>()).Returns(_mockCustomerRepo.Object);
        _mockUnitOfWork.Setup(u => u.GetRepo<Course, int>()).Returns(_mockCourseRepo.Object);
        _mockUnitOfWork.Setup(u => u.GetRepo<Domains, int>()).Returns(_mockDomainsRepo.Object);
        _mockUnitOfWork.Setup(u => u.GetRepo<RefreshToken, int>()).Returns(_mockRefreshTokenRepo.Object);

        _service = new DashboardService(_mockUnitOfWork.Object);
    }

    [Fact]
    public async Task GetDashboardDataAsync_ReturnsCorrectCounts()
    {
        // Arrange
        int roadmapCount = 10;
        int customerCount = 20;
        int courseCount = 30;
        int domainCount = 5;
        int onlineUserCount = 15;

        _mockRoadmapRepo.Setup(r => r.CountAsync(null))
            .ReturnsAsync(roadmapCount);

        _mockCustomerRepo.Setup(r => r.CountAsync(null))
            .ReturnsAsync(customerCount);

        _mockCourseRepo.Setup(r => r.CountAsync(null))
            .ReturnsAsync(courseCount);

        _mockDomainsRepo.Setup(r => r.CountAsync(null))
            .ReturnsAsync(domainCount);

        // For online users, it uses CountDistinctAsync with a spec and selector
        _mockRefreshTokenRepo.Setup(r => r.CountDistinctAsync(
                It.IsAny<ActiveRefreshTokensSpecification>(),
                It.IsAny<Expression<Func<RefreshToken, string>>>()))
            .ReturnsAsync(onlineUserCount);

        // Act
        var result = await _service.GetDashboardDataAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.NotNull(result.Data);
        Assert.Equal(roadmapCount, result.Data.RoadmapsGenerartedCount);
        Assert.Equal(customerCount, result.Data.AllCustomersCount);
        Assert.Equal(courseCount, result.Data.CoursesStoredCount);
        Assert.Equal(domainCount, result.Data.DomainsCount);
        Assert.Equal(onlineUserCount, result.Data.OnlineUsersCount);
    }
}
