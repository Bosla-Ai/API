using System.ComponentModel.DataAnnotations;

namespace Shared.Options;

public class OAuthSettingsOptions
{
    public const string SectionName = "OAuthSettings";

    public string DefaultReturnUrl { get; set; } = "https://bosla.me/";
    public string AllowedDomain { get; set; } = "https://bosla.me";
    public string AlternateReturnUrl { get; set; } = "https://front.bosla.almiraj.xyz/";
    public string AlternateDomain { get; set; } = "https://front.bosla.almiraj.xyz";
}
