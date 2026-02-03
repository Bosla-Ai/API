using System.Text.Json.Serialization;

namespace Shared.DTOs.DashboardDTOs;

public sealed class Dashboard
{
    [JsonPropertyName("roadmaps_count")]
    public int RoadmapsGenerartedCount { get; set; }

    [JsonPropertyName("online_users_count")]
    public int OnlineUsersCount { get; set; }

    [JsonPropertyName("customers_count")]
    public int AllCustomersCount { get; set; }

    [JsonPropertyName("courses_count")]
    public int CoursesStoredCount { get; set; }

    [JsonPropertyName("domains_count")]
    public int DomainsCount { get; set; }
}
