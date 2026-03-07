using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using Shared.Options;

namespace Service.Helpers;

public class UserRateLimiter : IDisposable
{
    private readonly ConcurrentDictionary<string, UserRequestTracker> _trackers = new();
    private readonly IOptionsMonitor<AiOptions> _options;
    private readonly Func<DateTime> _utcNow;
    private readonly Timer _pruneTimer;

    public UserRateLimiter(IOptionsMonitor<AiOptions> options)
        : this(options, () => DateTime.UtcNow) { }

    // Internal constructor for testing with injectable clock
    internal UserRateLimiter(IOptionsMonitor<AiOptions> options, Func<DateTime> utcNow)
    {
        _options = options;
        _utcNow = utcNow;

        // Prune stale entries every hour
        _pruneTimer = new Timer(_ => PruneStaleEntries(), null, TimeSpan.FromHours(1), TimeSpan.FromHours(1));
    }

    public bool TryConsumeRequest(string userId, bool isSuperAdmin)
    {
        if (isSuperAdmin) return true;

        var maxRequests = _options.CurrentValue.Gemini.MaxRequestsPerUserPerDay;
        var tracker = _trackers.GetOrAdd(userId, _ => new UserRequestTracker(_utcNow));

        lock (tracker)
        {
            tracker.ResetIfNewDay(_utcNow());

            if (tracker.Count >= maxRequests)
                return false;

            tracker.Count++;
            return true;
        }
    }

    public int GetRemainingRequests(string userId, bool isSuperAdmin)
    {
        if (isSuperAdmin) return int.MaxValue;

        var maxRequests = _options.CurrentValue.Gemini.MaxRequestsPerUserPerDay;

        if (_trackers.TryGetValue(userId, out var tracker))
        {
            lock (tracker)
            {
                tracker.ResetIfNewDay(_utcNow());
                return Math.Max(0, maxRequests - tracker.Count);
            }
        }

        return maxRequests;
    }

    private void PruneStaleEntries()
    {
        var today = _utcNow().Date;
        foreach (var kvp in _trackers)
        {
            if (kvp.Value.ResetDate < today)
            {
                _trackers.TryRemove(kvp.Key, out _);
            }
        }
    }

    public void Dispose()
    {
        _pruneTimer?.Dispose();
        GC.SuppressFinalize(this);
    }

    private class UserRequestTracker(Func<DateTime> utcNow)
    {
        public int Count;
        public DateTime ResetDate = utcNow().Date;

        public void ResetIfNewDay(DateTime now)
        {
            var today = now.Date;
            if (today > ResetDate)
            {
                Count = 0;
                ResetDate = today;
            }
        }
    }
}
