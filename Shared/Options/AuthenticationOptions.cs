using System.ComponentModel.DataAnnotations;

namespace Shared.Options;

public class BoslaAuthenticationOptions
{
    public const string SectionName = "Authentication";

    public OAuthProviderOptions Google { get; set; } = new();
    public OAuthProviderOptions Github { get; set; } = new();
}

public class OAuthProviderOptions
{
    [Required]
    public string ClientId { get; set; } = string.Empty;

    [Required]
    public string ClientSecret { get; set; } = string.Empty;
}
