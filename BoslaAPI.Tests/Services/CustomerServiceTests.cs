using System.Reflection;
using AutoMapper;
using Domain.Contracts;
using Domain.Entities;
using FluentAssertions;
using Moq;
using Service.Implementations;
using Shared;

namespace BoslaAPI.Tests.Services;

/// <summary>
/// Unit tests for CustomerService - focusing on repository operations
/// that don't require the CustomerHelper dependency
/// </summary>
public class CustomerServiceTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IGenericRepository<Customer, string>> _customerRepoMock;

    public CustomerServiceTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _customerRepoMock = new Mock<IGenericRepository<Customer, string>>();

        _unitOfWorkMock
            .Setup(u => u.GetRepo<Customer, string>())
            .Returns(_customerRepoMock.Object);
    }

    #region Intent Guardrail Tests

    [Theory]
    [InlineData("Create a roadmap for backend", true)]
    [InlineData("I need a learning path in .NET", true)]
    [InlineData("Analyze the backend market and give me a report", false)]
    [InlineData("Compare backend and devops salaries", false)]
    [InlineData("", false)]
    public void IsExplicitRoadmapRequest_DetectsOnlyExplicitRoadmapQueries(string query, bool expected)
    {
        // Arrange
        var method = typeof(CustomerService).GetMethod(
            "IsExplicitRoadmapRequest",
            BindingFlags.NonPublic | BindingFlags.Static);

        // Act
        var result = (bool)method!.Invoke(null, [query])!;

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void IsRoadmapIntakeQuestionSet_ReturnsTrue_ForRoadmapIntakeQuestions()
    {
        // Arrange
        var questions = new[]
        {
            new AskUserQuestion
            {
                Id = "q1",
                Text = "What specific job title are you aiming for?",
                Type = "text",
                Required = true
            },
            new AskUserQuestion
            {
                Id = "q2",
                Text = "What is your current programming experience level?",
                Type = "checkbox",
                Options = ["Beginner", "Intermediate", "Advanced"],
                Required = true
            }
        };

        var method = typeof(CustomerService).GetMethod(
            "IsRoadmapIntakeQuestionSet",
            BindingFlags.NonPublic | BindingFlags.Static);

        // Act
        var result = (bool)method!.Invoke(null, [questions])!;

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsRoadmapIntakeQuestionSet_ReturnsFalse_ForGenericAnalysisQuestions()
    {
        // Arrange
        var questions = new[]
        {
            new AskUserQuestion
            {
                Id = "q1",
                Text = "Which region should I analyze?",
                Type = "checkbox",
                Options = ["US", "EU", "MENA"],
                Required = true
            },
            new AskUserQuestion
            {
                Id = "q2",
                Text = "What seniority level should the report focus on?",
                Type = "checkbox",
                Options = ["Junior", "Mid", "Senior"],
                Required = false
            }
        };

        var method = typeof(CustomerService).GetMethod(
            "IsRoadmapIntakeQuestionSet",
            BindingFlags.NonPublic | BindingFlags.Static);

        // Act
        var result = (bool)method!.Invoke(null, [questions])!;

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region GetAllAsync Tests

    [Fact]
    public async Task GetAllAsync_ReturnsAllCustomers()
    {
        // Arrange
        var customers = new List<Customer>
        {
            new() { ApplicationUserId = "1" },
            new() { ApplicationUserId = "2" },
            new() { ApplicationUserId = "3" }
        };

        _customerRepoMock
            .Setup(r => r.GetAllAsync(It.IsAny<Specifications<Customer>>()))
            .ReturnsAsync(customers);

        var mapperMock = new Mock<IMapper>();
        var service = CreateServiceWithMockedDependencies(mapperMock.Object);

        // Act
        var result = await service.GetAllAsync();

        // Assert
        result.Should().HaveCount(3);
        result.Should().BeEquivalentTo(customers);
    }

    [Fact]
    public async Task GetAllAsync_WhenNoCustomers_ReturnsEmptyList()
    {
        // Arrange
        _customerRepoMock
            .Setup(r => r.GetAllAsync(It.IsAny<Specifications<Customer>>()))
            .ReturnsAsync([]);

        var mapperMock = new Mock<IMapper>();
        var service = CreateServiceWithMockedDependencies(mapperMock.Object);

        // Act
        var result = await service.GetAllAsync();

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region GetByIdAsync Tests

    [Fact]
    public async Task GetByIdAsync_WithValidId_ReturnsCustomer()
    {
        // Arrange
        string customerId = "customer-123";
        var customer = new Customer { ApplicationUserId = customerId };

        _customerRepoMock
            .Setup(r => r.GetIdAsync(customerId))
            .ReturnsAsync(customer);

        var mapperMock = new Mock<IMapper>();
        var service = CreateServiceWithMockedDependencies(mapperMock.Object);

        // Act
        var result = await service.GetByIdAsync(customerId);

        // Assert
        result.Should().NotBeNull();
        result.ApplicationUserId.Should().Be(customerId);
    }

    #endregion

    #region CreateAsync Tests

    [Fact]
    public async Task CreateAsync_CallsRepositoryCreate()
    {
        // Arrange
        var customer = new Customer { ApplicationUserId = "new-customer" };
        var mapperMock = new Mock<IMapper>();
        var service = CreateServiceWithMockedDependencies(mapperMock.Object);

        // Act
        await service.CreateAsync(customer);

        // Assert
        _customerRepoMock.Verify(r => r.CreateAsync(customer), Times.Once);
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task UpdateAsync_CallsRepositoryUpdate()
    {
        // Arrange
        var customer = new Customer { ApplicationUserId = "existing-customer" };
        var mapperMock = new Mock<IMapper>();
        var service = CreateServiceWithMockedDependencies(mapperMock.Object);

        // Act
        await service.UpdateAsync(customer);

        // Assert
        _customerRepoMock.Verify(r => r.UpdateAsync(customer), Times.Once);
    }

    #endregion

    #region DeleteAsync Tests

    [Fact]
    public async Task DeleteAsync_CallsRepositoryDelete()
    {
        // Arrange
        var customer = new Customer { ApplicationUserId = "to-delete-customer" };
        var mapperMock = new Mock<IMapper>();
        var service = CreateServiceWithMockedDependencies(mapperMock.Object);

        // Act
        await service.DeleteAsync(customer);

        // Assert
        _customerRepoMock.Verify(r => r.DeleteAsync(customer), Times.Once);
    }

    #endregion

    // Helper to create service - CustomerHelper and ConversationContextManager have constructor dependencies
    // so we skip testing methods that require them
    private CustomerService CreateServiceWithMockedDependencies(IMapper mapper)
    {
        return new CustomerService(_unitOfWorkMock.Object, mapper, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!);
    }
}
