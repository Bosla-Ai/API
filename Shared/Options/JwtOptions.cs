using System.ComponentModel.DataAnnotations;

namespace Shared.Options;

public class JwtOptions
{
    public const string SectionName = "JWT";

    [Required]
    public string Issuer { get; set; } = string.Empty;

    [Required]
    public string Audience { get; set; } = string.Empty;

    [Required]
    public string Key { get; set; } = string.Empty;

    public double Expires { get; set; } = 30;
    public double RefreshTokenLifeTime { get; set; } = 7;
}
