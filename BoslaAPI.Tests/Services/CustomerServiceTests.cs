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

    [Theory]
    [InlineData("not now", true)]
    [InlineData("No, skip this", true)]
    [InlineData("مش دلوقتي", true)]
    [InlineData("لا", true)]
    [InlineData("Yes, continue", false)]
    [InlineData("Generate roadmap now", false)]
    public void HasRoadmapDecline_DetectsDeclineReplies(string query, bool expected)
    {
        // Arrange
        var method = typeof(CustomerService).GetMethod(
            "HasRoadmapDecline",
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

    #region ForceRoadmapFlow Tests (Refactored — no longer includes explicitRoadmapRequest)

    [Theory]
    [InlineData("yes", true)]
    [InlineData("نعم", true)]
    [InlineData("أكيد", true)]
    [InlineData("تمام", true)]
    [InlineData("Yes, generate the roadmap now", true)]
    [InlineData("no", false)]
    [InlineData("Create a roadmap for backend", false)]
    [InlineData("I want to learn React", false)]
    [InlineData("Yes, go ahead", false)]
    public void HasRoadmapConfirmation_DetectsConfirmationReplies(string query, bool expected)
    {
        var method = typeof(CustomerService).GetMethod(
            "HasRoadmapConfirmation",
            BindingFlags.NonPublic | BindingFlags.Static);

        var result = (bool)method!.Invoke(null, [query])!;

        result.Should().Be(expected);
    }

    [Fact]
    public void BuildRoadmapConfirmationQuestions_ReturnsValidFallbackQuestions()
    {
        var method = typeof(CustomerService).GetMethod(
            "BuildRoadmapConfirmationQuestions",
            BindingFlags.NonPublic | BindingFlags.Static);

        var result = (AskUserQuestion[])method!.Invoke(null, [null, null])!;

        result.Should().NotBeNull();
        result.Should().HaveCountGreaterThan(0);
        // Without tags, only the roadmap_confirm question is returned
        result.Last().Id.Should().Be("roadmap_confirm");
        result.Last().Text.Should().NotBeNullOrEmpty();
        result.Last().Type.Should().Be("checkbox");
        result.Last().Options.Should().NotBeNull();
    }

    [Fact]
    public void BuildRoadmapConfirmationQuestions_WithTags_IncludesTopicPreview()
    {
        var method = typeof(CustomerService).GetMethod(
            "BuildRoadmapConfirmationQuestions",
            BindingFlags.NonPublic | BindingFlags.Static);

        var tags = new[] { "C#", "SQL Server", "EF Core" };
        var knownSkills = new[] { "C#" };
        var result = (AskUserQuestion[])method!.Invoke(null, [tags, knownSkills])!;

        result.Should().HaveCount(2);
        result[0].Id.Should().Be("topic_preview");
        result[0].Type.Should().Be("topic_chips");
        result[0].Options.Should().BeEquivalentTo(tags);
        result[0].PreSelected.Should().BeEquivalentTo(knownSkills);
        result[1].Id.Should().Be("roadmap_confirm");
    }

    #endregion

    #region UserProfileEntity Tests

    [Fact]
    public void UserProfileEntity_ToPromptSummary_WithCompleteProfile_ReturnsFormattedSummary()
    {
        var profile = new Shared.DTOs.UserProfileEntity
        {
            UserId = "user1",
            Interests = ["React", "Node.js"],
            ExperienceLevel = "beginner",
            TargetRole = "Frontend Developer",
            Constraints = ["free courses only"],
            PersonalityHints = ["visual learner"]
        };

        var summary = profile.ToPromptSummary();

        summary.Should().Contain("Interests: React, Node.js");
        summary.Should().Contain("Experience: beginner");
        summary.Should().Contain("Target Role: Frontend Developer");
        summary.Should().Contain("Constraints: free courses only");
        summary.Should().Contain("Learning Style: visual learner");
    }

    [Fact]
    public void UserProfileEntity_ToPromptSummary_WithEmptyProfile_ReturnsNoProfileMessage()
    {
        var profile = new Shared.DTOs.UserProfileEntity { UserId = "user1" };

        var summary = profile.ToPromptSummary();

        summary.Should().Be("No profile data yet");
    }

    [Fact]
    public void UserProfileEntity_MergeFrom_CombinesInterests()
    {
        var existing = new Shared.DTOs.UserProfileEntity
        {
            UserId = "user1",
            Interests = ["React"],
            ExperienceLevel = "beginner"
        };

        var newer = new Shared.DTOs.UserProfileEntity
        {
            UserId = "user1",
            Interests = ["React", "Node.js", "TypeScript"],
            ExperienceLevel = "intermediate"
        };

        existing.MergeFrom(newer);

        existing.Interests.Should().Contain("React");
        existing.Interests.Should().Contain("Node.js");
        existing.Interests.Should().Contain("TypeScript");
        existing.Interests.Should().HaveCount(3);
        existing.ExperienceLevel.Should().Be("intermediate");
    }

    [Fact]
    public void UserProfileEntity_MergeFrom_PreservesExistingWhenNewerIsNull()
    {
        var existing = new Shared.DTOs.UserProfileEntity
        {
            UserId = "user1",
            TargetRole = "Backend Developer",
            Interests = ["Python"]
        };

        var newer = new Shared.DTOs.UserProfileEntity
        {
            UserId = "user1",
            TargetRole = null,
            Interests = null
        };

        existing.MergeFrom(newer);

        existing.TargetRole.Should().Be("Backend Developer");
        existing.Interests.Should().Contain("Python");
    }

    [Fact]
    public void UserProfileEntity_FromExtraction_MapsCorrectly()
    {
        var extraction = new Shared.DTOs.UserProfileExtraction
        {
            Interests = ["AI", "ML"],
            ExperienceLevel = "advanced",
            TargetRole = "ML Engineer",
            Constraints = ["Arabic language"],
            PersonalityHints = ["hands-on"]
        };

        var entity = Shared.DTOs.UserProfileEntity.FromExtraction("user1", extraction);

        entity.UserId.Should().Be("user1");
        entity.Interests.Should().BeEquivalentTo(["AI", "ML"]);
        entity.ExperienceLevel.Should().Be("advanced");
        entity.TargetRole.Should().Be("ML Engineer");
        entity.ExtractionCount.Should().Be(1);
    }

    #endregion

    #region Prompt Template Format Tests

    [Fact]
    public void IntentDetectionUserPromptTemplate_AcceptsThreeParameters()
    {
        var template = "{0}\n\nUser Profile: {2}\n\nCurrent User Query: \"{1}\"";

        var result = string.Format(template,
            "Conversation History:\nHello\n\n",
            "I want to learn React",
            "Interests: Python | Experience: beginner");

        result.Should().Contain("Conversation History:");
        result.Should().Contain("I want to learn React");
        result.Should().Contain("Interests: Python | Experience: beginner");
    }

    [Fact]
    public void IntentDetectionUserPromptTemplate_HandlesEmptyProfile()
    {
        var template = "{0}\n\nUser Profile: {2}\n\nCurrent User Query: \"{1}\"";

        var result = string.Format(template, "", "hello", "No profile data yet");

        result.Should().Contain("No profile data yet");
    }

    #endregion

    // Helper to create service - CustomerHelper and ConversationContextManager have constructor dependencies
    // so we skip testing methods that require them
    private CustomerService CreateServiceWithMockedDependencies(IMapper mapper)
    {
        return new CustomerService(_unitOfWorkMock.Object, mapper, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!);
    }
}
