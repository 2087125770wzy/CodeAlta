using System.Collections.Concurrent;

namespace CodeAlta.Mcp;

/// <summary>
/// Tracks active MCP sessions created by <see cref="CodeAltaMcpServerFactory"/>.
/// </summary>
public sealed class McpSessionRegistry
{
    private readonly ConcurrentDictionary<Guid, DateTimeOffset> _sessions = new();

    /// <summary>
    /// Registers a session and returns its generated session id.
    /// </summary>
    /// <returns>The created session id.</returns>
    public Guid Register()
    {
        var sessionId = Guid.CreateVersion7();
        _sessions[sessionId] = DateTimeOffset.UtcNow;
        return sessionId;
    }

    /// <summary>
    /// Unregisters a session.
    /// </summary>
    /// <param name="sessionId">The session id to remove.</param>
    public void Unregister(Guid sessionId)
    {
        _sessions.TryRemove(sessionId, out _);
    }

    /// <summary>
    /// Returns a snapshot of currently-active session ids.
    /// </summary>
    /// <returns>Active session ids.</returns>
    public IReadOnlyList<Guid> ListSessions()
    {
        return _sessions.Keys
            .OrderBy(static x => x)
            .ToArray();
    }
}
