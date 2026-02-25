namespace CodeNoesis.Agent;

/// <summary>
/// Represents a position in a text document.
/// </summary>
/// <param name="Line">The 1-based line number.</param>
/// <param name="Character">The 1-based character index.</param>
public sealed record AgentPosition(int Line, int Character);

