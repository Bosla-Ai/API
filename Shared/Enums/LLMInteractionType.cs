namespace Shared.Enums;

public enum LLMInteractionType
{
    RoadmapGeneration,
    CVAnalysis,
    ChatWithAI,
    ChooseTrack,
    [Obsolete("Merged into ChatWithAI. Kept for backward compatibility.")]
    ChooseMethod,
    TopicPreview,
    ProgressCheck,
}