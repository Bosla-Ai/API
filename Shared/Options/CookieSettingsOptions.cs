namespace Shared.Options;

public class CookieSettingsOptions
{
    public const string SectionName = "CookieSettings";

    public string AllowedSubDomain { get; set; } = string.Empty;
    public bool Secure { get; set; } = true;
    public string SameSite { get; set; } = "None";
    public bool Partitioned { get; set; } = false;
}
