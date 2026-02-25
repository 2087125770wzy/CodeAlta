namespace CodeNoesis.Agent;

/// <summary>
/// Captures repository and directory context for a session.
/// </summary>
/// <param name="Cwd">The session working directory.</param>
/// <param name="GitRoot">The git repository root, if any.</param>
/// <param name="Repository">The GitHub repository in "owner/repo" format, if known.</param>
/// <param name="Branch">The current git branch, if known.</param>
public sealed record AgentSessionContext(
    string? Cwd = null,
    string? GitRoot = null,
    string? Repository = null,
    string? Branch = null);

