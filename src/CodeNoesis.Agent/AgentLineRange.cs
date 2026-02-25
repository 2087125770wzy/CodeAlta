namespace CodeNoesis.Agent;

/// <summary>
/// Represents a line range in a text document.
/// </summary>
/// <param name="StartLine">The 1-based start line.</param>
/// <param name="EndLine">The 1-based end line.</param>
public sealed record AgentLineRange(int StartLine, int EndLine);

