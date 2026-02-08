namespace Shared.Options;

public class CosmosDbOptions
{
    public const string SectionName = "CosmosDb";

    public string Endpoint { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = "BoslaChat";
    public string ContainerName { get; set; } = "chat_messages";
}
