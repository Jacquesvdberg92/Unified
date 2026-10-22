using System.Collections.Concurrent;

namespace Unified.Hubs;

/// <summary>
/// Remembers which SignalR connections each user has open, so server code can add or remove
/// all of a user's connections from a group (SignalR has no "remove user from group" API).
/// Per-process: fine for a single app instance; a scaled-out deployment would need a backplane.
/// </summary>
public class HubConnectionTracker
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _connections = new();

    public void Add(string userId, string connectionId) =>
        _connections.GetOrAdd(userId, _ => new ConcurrentDictionary<string, byte>())[connectionId] = 0;

    public void Remove(string userId, string connectionId)
    {
        if (!_connections.TryGetValue(userId, out var userConnections)) return;
        userConnections.TryRemove(connectionId, out _);
        if (userConnections.IsEmpty)
            _connections.TryRemove(new KeyValuePair<string, ConcurrentDictionary<string, byte>>(userId, userConnections));
    }

    public IReadOnlyCollection<string> GetConnections(string userId) =>
        _connections.TryGetValue(userId, out var userConnections) ? userConnections.Keys.ToList() : [];
}
