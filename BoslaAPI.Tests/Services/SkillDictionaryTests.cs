using Service.Helpers;

namespace BoslaAPI.Tests.Services;

public class SkillDictionaryTests
{
    [Theory]
    [InlineData("I want to learn React and Node.js", false)]
    [InlineData("REST API development", false)]
    [InlineData("programming in Ruby", false)]
    [InlineData("I want to learn R for data science", true)]
    [InlineData("R is great for statistics", true)]
    public void ExtractSkills_ShortSkill_R_UsesWordBoundary(string input, bool shouldContainR)
    {
        var result = SkillDictionary.ExtractSkills(input);
        Assert.Equal(shouldContainR, result.ContainsKey("R"));
    }

    [Theory]
    [InlineData("Google Cloud Platform", false)]
    [InlineData("I want to learn Go and Rust", true)]
    public void ExtractSkills_ShortSkill_Go_UsesWordBoundary(string input, bool shouldContainGo)
    {
        var result = SkillDictionary.ExtractSkills(input);
        Assert.Equal(shouldContainGo, result.ContainsKey("Go"));
    }

    [Fact]
    public void ExtractSkills_LongerSkills_StillUseContains()
    {
        var result = SkillDictionary.ExtractSkills("I want to learn React and TypeScript");
        Assert.True(result.ContainsKey("React"));
        Assert.True(result.ContainsKey("TypeScript"));
    }
}
