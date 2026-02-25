namespace CodeNoesis.Agent;

/// <summary>
/// Describes a stored or active session known to a backend.
/// </summary>
/// <param name="SessionId">The backend session identifier.</param>
/// <param name="CreatedAt">The time the session was created.</param>
/// <param name="UpdatedAt">The time the session was last updated.</param>
/// <param name="Summary">Optional session summary or preview text.</param>
/// <param name="Context">Optional directory/repo context.</param>
/// <param name="WorkspacePath">Optional backend-managed workspace path for the session.</param>
public sealed record AgentSessionMetadata(
    string SessionId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string? Summary = null,
    AgentSessionContext? Context = null,
    string? WorkspacePath = null);

