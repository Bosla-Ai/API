namespace Shared.DTOs;

public class CareerPulseDTO
{
    public int ReadinessScore { get; set; }
    public string ReadinessLevel { get; set; } = string.Empty;
    public string[] MatchedSkills { get; set; } = [];
    public SkillGap[] TopGaps { get; set; } = [];
    public string Insight { get; set; } = string.Empty;
    public string TargetRole { get; set; } = string.Empty;
    public int JobsAnalyzed { get; set; }
}

public class SkillGap
{
    public string Skill { get; set; } = string.Empty;
    public int DemandPercent { get; set; }
    public string Category { get; set; } = string.Empty;
}
