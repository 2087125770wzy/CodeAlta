namespace CodeNoesis.Agent;

/// <summary>
/// Well-known backend identifiers.
/// </summary>
public static class AgentBackendIds
{
    /// <summary>
    /// GitHub Copilot CLI runtime backend.
    /// </summary>
    public static readonly AgentBackendId Copilot = new("copilot");

    /// <summary>
    /// Codex app-server runtime backend.
    /// </summary>
    public static readonly AgentBackendId Codex = new("codex");
}

