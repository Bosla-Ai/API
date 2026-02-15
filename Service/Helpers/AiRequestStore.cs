using System.Collections.Concurrent;
using Shared;

namespace Service.Helpers;

public class AiRequestStore
{
    private readonly ConcurrentDictionary<string, RequestEntry> _requests = new();
    private readonly TimeSpan _ttl = TimeSpan.FromSeconds(60);
    private readonly Lock _cleanupLock = new();
    private DateTime _lastCleanup = DateTime.UtcNow;
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromSeconds(30);

    private record RequestEntry(string UserId, AiQueryRequest Request, DateTime CreatedAt);

    public string Create(string userId, AiQueryRequest request)
    {
        if (string.IsNullOrEmpty(userId))
            throw new ArgumentNullException(nameof(userId));
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        // Generate secure, unpredictable requestId
        var requestId = $"req_{Guid.NewGuid():N}";
        var entry = new RequestEntry(userId, request, DateTime.UtcNow);

        _requests[requestId] = entry;

        // Trigger cleanup if needed (non-blocking)
        TryCleanupExpired();

        return requestId;
    }

    public (string UserId, AiQueryRequest Request)? GetAndRemove(string requestId)
    {
        if (string.IsNullOrEmpty(requestId))
            return null;

        if (!_requests.TryRemove(requestId, out var entry))
            return null;

        // Check if expired
        if (DateTime.UtcNow - entry.CreatedAt > _ttl)
            return null;

        return (entry.UserId, entry.Request);
    }

    public bool Exists(string requestId)
    {
        if (string.IsNullOrEmpty(requestId))
            return false;

        if (!_requests.TryGetValue(requestId, out var entry))
            return false;

        // Check if expired
        if (DateTime.UtcNow - entry.CreatedAt > _ttl)
        {
            _requests.TryRemove(requestId, out _);
            return false;
        }

        return true;
    }

    private void TryCleanupExpired()
    {
        if (DateTime.UtcNow - _lastCleanup < _cleanupInterval)
            return;

        lock (_cleanupLock)
        {
            if (DateTime.UtcNow - _lastCleanup < _cleanupInterval)
                return;

            var now = DateTime.UtcNow;
            var expiredKeys = _requests
                .Where(kvp => now - kvp.Value.CreatedAt > _ttl)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in expiredKeys)
            {
                _requests.TryRemove(key, out _);
            }

            _lastCleanup = DateTime.UtcNow;
        }
    }

    public int Count => _requests.Count;
}
