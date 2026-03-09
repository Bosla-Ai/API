namespace Shared.Options;

public class StackExchangeOptions
{
    public const string SectionName = "StackExchange";

    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.stackexchange.com/2.3";
    public int TimeoutSeconds { get; set; } = 5;
}
