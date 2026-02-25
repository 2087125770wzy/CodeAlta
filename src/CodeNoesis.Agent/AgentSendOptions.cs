namespace CodeNoesis.Agent;

/// <summary>
/// Options for sending user input to a session.
/// </summary>
public sealed class AgentSendOptions
{
    /// <summary>
    /// Gets or initializes the input to send.
    /// </summary>
    public required AgentInput Input { get; init; }

    /// <summary>
    /// Gets or initializes an optional backend-specific mode.
    /// </summary>
    public string? Mode { get; init; }
}

