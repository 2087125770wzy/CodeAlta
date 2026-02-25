namespace CodeNoesis.Agent;

/// <summary>
/// Represents a selection range in a text document.
/// </summary>
/// <param name="Start">The selection start position.</param>
/// <param name="End">The selection end position.</param>
public sealed record AgentSelectionRange(AgentPosition Start, AgentPosition End);

