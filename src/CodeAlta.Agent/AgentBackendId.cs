using System.Text.Json.Serialization;

namespace CodeAlta.Agent;

/// <summary>
/// Identifies an agent backend (e.g. Copilot, Codex).
/// </summary>
/// <param name="Value">The backend identifier value.</param>
[JsonConverter(typeof(AgentBackendIdJsonConverter))]
public readonly record struct AgentBackendId(string Value)
{
    /// <inheritdoc />
    public override string ToString() => Value;
}
