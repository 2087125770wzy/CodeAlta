namespace CodeNoesis.Agent;

/// <summary>
/// Identifies an agent backend (e.g. Copilot, Codex).
/// </summary>
/// <param name="Value">The backend identifier value.</param>
public readonly record struct AgentBackendId(string Value)
{
    /// <inheritdoc />
    public override string ToString() => Value;
}

