namespace Service.Helpers;

public static class TagNormalizer
{
    private static readonly string[] CommonSuffixes =
        ["fundamentals", "essentials", "basics", "development", "programming", "tutorial", "course"];

    public static string StripSuffixes(string tag)
    {
        var normalized = tag.ToLowerInvariant().Trim();
        foreach (var suffix in CommonSuffixes)
        {
            if (normalized.EndsWith($" {suffix}"))
                normalized = normalized[..^(suffix.Length + 1)].Trim();
        }
        return normalized;
    }
}
