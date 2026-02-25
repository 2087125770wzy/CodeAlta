namespace CodeNoesis.Agent;

/// <summary>
/// Identifies a backend run (e.g. a Codex turn id or Copilot message id).
/// </summary>
/// <param name="Value">The identifier value.</param>
public readonly record struct AgentRunId(string Value)
{
    /// <inheritdoc />
    public override string ToString() => Value;
}

