using System.ComponentModel.DataAnnotations;

namespace Shared.Options;

public class ConnectionStringsOptions
{
    public const string SectionName = "ConnectionStrings";

    public string CS { get; set; } = string.Empty;
    public string ServerConnection { get; set; } = string.Empty;
    public string Redis { get; set; } = string.Empty;
}


