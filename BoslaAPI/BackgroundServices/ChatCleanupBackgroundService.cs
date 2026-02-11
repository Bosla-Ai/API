using Service.Abstraction;

namespace BoslaAPI.BackgroundServices;

public class ChatCleanupBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<ChatCleanupBackgroundService> logger) : BackgroundService
{
    private const int CLEANUP_INTERVAL_HOURS = 24;
    private const int MAX_CHAT_AGE_DAYS = 7;
    private const int RENEWAL_GRACE_DAYS = 3;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Chat cleanup service started, running every {Hours} hours", CLEANUP_INTERVAL_HOURS);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PerformCleanupAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during chat cleanup");
            }

            await Task.Delay(TimeSpan.FromHours(CLEANUP_INTERVAL_HOURS), stoppingToken);
        }
    }

    private async Task PerformCleanupAsync()
    {
        using var scope = scopeFactory.CreateScope();
        var chatHistoryService = scope.ServiceProvider.GetRequiredService<IChatHistoryService>();

        var deletedCount = await chatHistoryService.CleanInactiveChatsAsync(MAX_CHAT_AGE_DAYS, RENEWAL_GRACE_DAYS);

        if (deletedCount > 0)
        {
            logger.LogInformation("Chat cleanup removed {Count} old messages", deletedCount);
        }
    }
}
