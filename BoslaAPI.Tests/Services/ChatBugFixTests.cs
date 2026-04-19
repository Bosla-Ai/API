using System.Reflection;
using FluentAssertions;
using Service.Helpers;
using Service.Implementations;
using Shared;
using Shared.DTOs;
using Shared.Enums;
using Shared.Options;

namespace BoslaAPI.Tests.Services;

public class ChatBugFixTests
{
    #region Dual Prompt Architecture (Workstream 1)

    [Fact]
    public void PromptOptions_HasDeepModeFields()
    {
        var opts = new PromptOptions();

        opts.ChatSystemPromptDeep.Should().NotBeNull();
        opts.LanguageRules.Should().NotBeNull();
    }

    [Fact]
    public void PromptOptions_DeepFieldsDefaultToEmpty()
    {
        var opts = new PromptOptions();

        opts.ChatSystemPromptDeep.Should().BeEmpty();
        opts.LanguageRules.Should().BeEmpty();
    }

    [Fact]
    public void BuildChatPrompt_NormalMode_UsesNormalPrompt()
    {
        var method = typeof(CustomerService).GetMethod(
            "BuildChatPrompt",
            BindingFlags.NonPublic | BindingFlags.Instance);
        method.Should().NotBeNull();

        var parameters = method!.GetParameters();
        parameters.Should().Contain(p => p.Name == "chatMode");
        parameters.First(p => p.Name == "chatMode").DefaultValue.Should().Be(ChatMode.Fast);
    }

    [Fact]
    public void DetectIntentAsync_AcceptsChatModeParameter()
    {
        var method = typeof(CustomerService).GetMethod(
            "DetectIntentAsync",
            BindingFlags.NonPublic | BindingFlags.Instance);
        method.Should().NotBeNull();

        var parameters = method!.GetParameters();
        parameters.Should().Contain(p => p.Name == "chatMode");
        parameters.First(p => p.Name == "chatMode").DefaultValue.Should().Be(ChatMode.Fast);
    }

    #endregion

    #region System Prompt Plumbing (Workstream 2)

    [Fact]
    public void CustomerHelper_SendRequestWithModel_AcceptsSystemPrompt()
    {
        var method = typeof(CustomerHelper).GetMethod(
            "SendRequestWithModel",
            BindingFlags.Public | BindingFlags.Instance);
        method.Should().NotBeNull();

        var parameters = method!.GetParameters();
        parameters.Should().Contain(p => p.Name == "systemPrompt");
    }

    [Fact]
    public void CustomerHelper_SendRequestByTask_AcceptsSystemPrompt()
    {
        var method = typeof(CustomerHelper).GetMethod(
            "SendRequestByTask",
            BindingFlags.Public | BindingFlags.Instance);
        method.Should().NotBeNull();

        var parameters = method!.GetParameters();
        parameters.Should().Contain(p => p.Name == "systemPrompt");
    }

    [Fact]
    public void CustomerHelper_SendRequestToGemini_AcceptsSystemPrompt()
    {
        var method = typeof(CustomerHelper).GetMethod(
            "SendRequestToGemini",
            BindingFlags.Public | BindingFlags.Instance);
        method.Should().NotBeNull();

        var parameters = method!.GetParameters();
        parameters.Should().Contain(p => p.Name == "systemPrompt");
    }

    [Theory]
    [InlineData("ExecuteLlmRequest")]
    [InlineData("ExecuteGroqRequest")]
    [InlineData("ExecuteMistralRequest")]
    [InlineData("ExecuteGeminiRequestWithRotation")]
    [InlineData("ExecuteLlmRequestWithKeyRotation")]
    [InlineData("ExecuteGroqRequestWithKeyRotation")]
    [InlineData("ExecuteMistralRequestWithKeyRotation")]
    public void CustomerHelper_InternalMethods_AcceptSystemPrompt(string methodName)
    {
        var method = typeof(CustomerHelper).GetMethod(
            methodName,
            BindingFlags.NonPublic | BindingFlags.Instance);
        method.Should().NotBeNull($"Method {methodName} should exist");

        var parameters = method!.GetParameters();
        parameters.Should().Contain(p => p.Name == "systemPrompt",
            $"Method {methodName} should accept systemPrompt parameter");
    }

    #endregion

    #region Loop Breaker - Discovery State (Workstream 4)

    [Fact]
    public void RoadmapIntentHelper_HasDiscoveryAskedConstant()
    {
        RoadmapIntentHelper.RoadmapStateDiscoveryAsked.Should().Be("discovery_asked");
    }

    [Fact]
    public void RoadmapIntentHelper_AllStatesAreDifferent()
    {
        var states = new[]
        {
            RoadmapIntentHelper.RoadmapStatePendingConfirmation,
            RoadmapIntentHelper.RoadmapStateDiscoveryAsked,
            RoadmapIntentHelper.RoadmapStateCompleted,
            RoadmapIntentHelper.RoadmapStateIdle
        };

        states.Should().OnlyHaveUniqueItems();
    }

    [Theory]
    [InlineData("roadmap_state:discovery_asked", "discovery_asked")]
    [InlineData("roadmap_state:pending_confirmation", "pending_confirmation")]
    [InlineData("roadmap_state:completed", "completed")]
    [InlineData("roadmap_state:idle", "idle")]
    [InlineData("random text", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void RoadmapIntentHelper_ExtractRoadmapState_ParsesCorrectly(string? message, string? expected)
    {
        var result = RoadmapIntentHelper.ExtractRoadmapState(message);
        result.Should().Be(expected);
    }

    [Fact]
    public void RoadmapIntentHelper_BuildRoadmapStateMessage_FormatsCorrectly()
    {
        var result = RoadmapIntentHelper.BuildRoadmapStateMessage("discovery_asked");
        result.Should().Be("roadmap_state:discovery_asked");

        var parsed = RoadmapIntentHelper.ExtractRoadmapState(result);
        parsed.Should().Be("discovery_asked");
    }

    #endregion

    #region Profile Parser Enhancement (Workstream 4D)

    [Theory]
    [InlineData("roadmap_experience: Intermediate", "Intermediate", null)]
    [InlineData("roadmap_target_role: Software Engineer", null, "Software Engineer")]
    [InlineData("What is your current experience level: Beginner", "Beginner", null)]
    [InlineData("Which role are you targeting with this roadmap: Backend Developer", null, "Backend Developer")]
    [InlineData("مستواك الحالي إيه في المجال: متوسط", "متوسط", null)]
    public void ExtractProfileFromUserMessage_ParsesDiscoveryAnswers(string query, string? expectedExperience, string? expectedRole)
    {
        var method = typeof(CustomerService).GetMethod(
            "ExtractProfileFromUserMessage",
            BindingFlags.NonPublic | BindingFlags.Static);
        method.Should().NotBeNull();

        var result = (UserProfileEntity?)method!.Invoke(null, ["user1", query]);

        result.Should().NotBeNull();
        if (expectedExperience != null)
            result!.ExperienceLevel.Should().Be(expectedExperience);
        if (expectedRole != null)
            result!.TargetRole.Should().Be(expectedRole);
    }

    [Theory]
    [InlineData("roadmap_budget: Free only")]
    [InlineData("تفضّل مصادر: مجاني فقط")]
    public void ExtractProfileFromUserMessage_ParsesBudgetAnswers(string query)
    {
        var method = typeof(CustomerService).GetMethod(
            "ExtractProfileFromUserMessage",
            BindingFlags.NonPublic | BindingFlags.Static);

        var result = (UserProfileEntity?)method!.Invoke(null, ["user1", query]);

        result.Should().NotBeNull();
        result!.Constraints.Should().NotBeEmpty();
    }

    [Theory]
    [InlineData("roadmap_focus: Algorithms, System Design")]
    [InlineData("Which topics should this roadmap prioritize first: React, Node.js")]
    public void ExtractProfileFromUserMessage_ParsesFocusAnswers(string query)
    {
        var method = typeof(CustomerService).GetMethod(
            "ExtractProfileFromUserMessage",
            BindingFlags.NonPublic | BindingFlags.Static);

        var result = (UserProfileEntity?)method!.Invoke(null, ["user1", query]);

        result.Should().NotBeNull();
        result!.Interests.Should().NotBeEmpty();
    }

    [Fact]
    public void ExtractProfileFromUserMessage_MultiLineDiscoveryAnswers()
    {
        var query = "roadmap_experience: Beginner\nroadmap_target_role: Software Engineer\nroadmap_budget: Free only";

        var method = typeof(CustomerService).GetMethod(
            "ExtractProfileFromUserMessage",
            BindingFlags.NonPublic | BindingFlags.Static);

        var result = (UserProfileEntity?)method!.Invoke(null, ["user1", query]);

        result.Should().NotBeNull();
        result!.ExperienceLevel.Should().Be("Beginner");
        result!.TargetRole.Should().Be("Software Engineer");
        result!.Constraints.Should().Contain("Free only");
    }

    [Fact]
    public void ExtractProfileFromConversationContext_ParsesUserBlocksOnly()
    {
        var context = """
            Recent Conversation:
            [assistant]: Before I generate your roadmap, I need a bit more context.
            [user]: What is your current experience level?: Beginner
            Which role are you targeting with this roadmap?: Backend Engineer
            [assistant]: Perfect, thanks!
            """;

        var method = typeof(CustomerService).GetMethod(
            "ExtractProfileFromConversationContext",
            BindingFlags.NonPublic | BindingFlags.Static);

        var result = (UserProfileEntity?)method!.Invoke(null, ["user1", context]);

        result.Should().NotBeNull();
        result!.ExperienceLevel.Should().Be("Beginner");
        result.TargetRole.Should().Be("Backend Engineer");
    }

    [Fact]
    public void ExtractProfileFromConversationContext_IgnoresAssistantOnlySignals()
    {
        var context = """
            Recent Conversation:
            [assistant]: What is your current experience level?: Beginner
            [assistant]: Which role are you targeting with this roadmap?: Backend Engineer
            """;

        var method = typeof(CustomerService).GetMethod(
            "ExtractProfileFromConversationContext",
            BindingFlags.NonPublic | BindingFlags.Static);

        var result = (UserProfileEntity?)method!.Invoke(null, ["user1", context]);

        result.Should().BeNull();
    }

    [Fact]
    public void ExtractProfileFromUserMessage_ReturnsNull_ForIrrelevantText()
    {
        var method = typeof(CustomerService).GetMethod(
            "ExtractProfileFromUserMessage",
            BindingFlags.NonPublic | BindingFlags.Static);

        var result = (UserProfileEntity?)method!.Invoke(null, ["user1", "hello how are you"]);

        result.Should().BeNull();
    }

    #endregion

    #region HasSufficientRoadmapProfile Tests

    [Fact]
    public void HasSufficientRoadmapProfile_ReturnsFalse_WhenProfileIsNull()
    {
        var method = typeof(CustomerService).GetMethod(
            "HasSufficientRoadmapProfile",
            BindingFlags.NonPublic | BindingFlags.Static);

        var result = (bool)method!.Invoke(null, [null])!;
        result.Should().BeFalse();
    }

    [Fact]
    public void HasSufficientRoadmapProfile_ReturnsFalse_WhenMissingTargetRole()
    {
        var profile = new UserProfileEntity
        {
            UserId = "u1",
            ExperienceLevel = "Beginner",
            TargetRole = null
        };

        var method = typeof(CustomerService).GetMethod(
            "HasSufficientRoadmapProfile",
            BindingFlags.NonPublic | BindingFlags.Static);

        var result = (bool)method!.Invoke(null, [profile])!;
        result.Should().BeFalse();
    }

    [Fact]
    public void HasSufficientRoadmapProfile_ReturnsFalse_WhenMissingExperience()
    {
        var profile = new UserProfileEntity
        {
            UserId = "u1",
            ExperienceLevel = null,
            TargetRole = "Backend Developer"
        };

        var method = typeof(CustomerService).GetMethod(
            "HasSufficientRoadmapProfile",
            BindingFlags.NonPublic | BindingFlags.Static);

        var result = (bool)method!.Invoke(null, [profile])!;
        result.Should().BeFalse();
    }

    [Fact]
    public void HasSufficientRoadmapProfile_ReturnsTrue_WhenComplete()
    {
        var profile = new UserProfileEntity
        {
            UserId = "u1",
            ExperienceLevel = "Intermediate",
            TargetRole = "Backend Developer"
        };

        var method = typeof(CustomerService).GetMethod(
            "HasSufficientRoadmapProfile",
            BindingFlags.NonPublic | BindingFlags.Static);

        var result = (bool)method!.Invoke(null, [profile])!;
        result.Should().BeTrue();
    }

    [Fact]
    public void HasSufficientRoadmapProfile_ReturnsTrue_WhenRoleAndInterestsPresentWithoutExperience()
    {
        var profile = new UserProfileEntity
        {
            UserId = "u1",
            ExperienceLevel = null,
            TargetRole = "Backend Developer",
            Interests = ["React", "Node.js"]
        };

        var method = typeof(CustomerService).GetMethod(
            "HasSufficientRoadmapProfile",
            BindingFlags.NonPublic | BindingFlags.Static);

        var result = (bool)method!.Invoke(null, [profile])!;
        result.Should().BeTrue();
    }

    #endregion

    #region BuildRoadmapDiscoveryQuestions Tests

    [Fact]
    public void BuildRoadmapDiscoveryQuestions_AsksAboutAllMissingFields()
    {
        var method = typeof(CustomerService).GetMethod(
            "BuildRoadmapDiscoveryQuestions",
            BindingFlags.NonPublic | BindingFlags.Static);

        var result = (AskUserQuestion[])method!.Invoke(null, [null, "I want a roadmap", ""])!;

        result.Should().HaveCountGreaterThanOrEqualTo(2);
        result.Should().HaveCountLessThanOrEqualTo(3);
        result.Should().Contain(q => q.Id == "roadmap_experience");
        result.Should().Contain(q => q.Id == "roadmap_target_role");
    }

    [Fact]
    public void BuildRoadmapDiscoveryQuestions_SkipsKnownFields()
    {
        var profile = new UserProfileEntity
        {
            UserId = "u1",
            ExperienceLevel = "Intermediate",
            TargetRole = null
        };

        var method = typeof(CustomerService).GetMethod(
            "BuildRoadmapDiscoveryQuestions",
            BindingFlags.NonPublic | BindingFlags.Static);

        var result = (AskUserQuestion[])method!.Invoke(null, [profile, "I want a roadmap", ""])!;

        result.Should().NotContain(q => q.Id == "roadmap_experience");
        result.Should().Contain(q => q.Id == "roadmap_target_role");
    }

    [Fact]
    public void BuildRoadmapDiscoveryQuestions_ArabicInput_ReturnsArabicQuestions()
    {
        var method = typeof(CustomerService).GetMethod(
            "BuildRoadmapDiscoveryQuestions",
            BindingFlags.NonPublic | BindingFlags.Static);

        var result = (AskUserQuestion[])method!.Invoke(null, [null, "عايز رودماب", ""])!;

        result.Should().NotBeEmpty();
        result[0].Text.Should().MatchRegex("[\\u0600-\\u06FF]");
    }

    [Fact]
    public void BuildRoadmapDiscoveryQuestions_AllFieldsKnown_ReturnsExperienceQuestion()
    {
        var profile = new UserProfileEntity
        {
            UserId = "u1",
            ExperienceLevel = "Intermediate",
            TargetRole = "Backend Developer",
            Constraints = ["Free only"]
        };

        var method = typeof(CustomerService).GetMethod(
            "BuildRoadmapDiscoveryQuestions",
            BindingFlags.NonPublic | BindingFlags.Static);

        var result = (AskUserQuestion[])method!.Invoke(null, [profile, "I want a roadmap", ""])!;

        result.Should().HaveCount(1);
        result[0].Id.Should().Be("roadmap_experience");
    }

    #endregion
}
