using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using Shared.Options;

namespace Service.Helpers;

public class UserRateLimiter(IOptionsMonitor<AiOptions> options)
{
    private readonly ConcurrentDictionary<string, UserRequestTracker> _trackers = new();
    private readonly IOptionsMonitor<AiOptions> _options = options;

    public bool TryConsumeRequest(string userId, bool isSuperAdmin)
    {
        if (isSuperAdmin) return true;

        var maxRequests = _options.CurrentValue.Gemini.MaxRequestsPerUserPerDay;
        var tracker = _trackers.GetOrAdd(userId, _ => new UserRequestTracker());

        lock (tracker)
        {
            tracker.ResetIfNewDay();

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
                tracker.ResetIfNewDay();
                return Math.Max(0, maxRequests - tracker.Count);
            }
        }

        return maxRequests;
    }

    private class UserRequestTracker
    {
        public int Count;
        public DateTime ResetDate = DateTime.UtcNow.Date;

        public void ResetIfNewDay()
        {
            var today = DateTime.UtcNow.Date;
            if (today > ResetDate)
            {
                Count = 0;
                ResetDate = today;
            }
        }
    }
}
