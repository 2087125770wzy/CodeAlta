namespace CodeAlta.Persistence;

/// <summary>
/// Represents a persisted agent registration.
/// </summary>
public sealed record AgentRecord
{
    /// <summary>
    /// Gets the agent identifier.
    /// </summary>
    public required AgentId AgentId { get; init; }

    /// <summary>
    /// Gets the agent role.
    /// </summary>
    public required string Role { get; init; }

    /// <summary>
    /// Gets the scope kind.
    /// </summary>
    public required string ScopeKind { get; init; }

    /// <summary>
    /// Gets the optional scope identifier.
    /// </summary>
    public string? ScopeId { get; init; }

    /// <summary>
    /// Gets the backend identifier.
    /// </summary>
    public required string BackendId { get; init; }

    /// <summary>
    /// Gets the creation timestamp in UTC.
    /// </summary>
    public required DateTimeOffset CreatedAt { get; init; }
}

/// <summary>
/// Represents a persisted agent session.
/// </summary>
public sealed record AgentSessionRecord
{
    /// <summary>
    /// Gets the session identifier.
    /// </summary>
    public required string SessionId { get; init; }

    /// <summary>
    /// Gets the owning agent identifier.
    /// </summary>
    public required AgentId AgentId { get; init; }

    /// <summary>
    /// Gets the backend session identifier.
    /// </summary>
    public string? BackendSessionId { get; init; }

    /// <summary>
    /// Gets the creation timestamp in UTC.
    /// </summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// Gets the last-used timestamp in UTC.
    /// </summary>
    public required DateTimeOffset LastUsedAt { get; init; }
}
