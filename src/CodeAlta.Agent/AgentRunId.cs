using System.Text.Json.Serialization;

namespace CodeAlta.Agent;

/// <summary>
/// Identifies a backend run (e.g. a Codex turn id or Copilot message id).
/// </summary>
/// <param name="Value">The identifier value.</param>
[JsonConverter(typeof(AgentRunIdJsonConverter))]
public readonly record struct AgentRunId(string Value)
{
    /// <inheritdoc />
    public override string ToString() => Value;
}
