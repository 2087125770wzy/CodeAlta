namespace CodeAlta.Agent;

/// <summary>
/// Specifies the reasoning effort level for models that support it.
/// </summary>
public enum AgentReasoningEffort
{
    /// <summary>
    /// Low reasoning effort.
    /// </summary>
    Low = 0,

    /// <summary>
    /// Medium reasoning effort.
    /// </summary>
    Medium = 1,

    /// <summary>
    /// High reasoning effort.
    /// </summary>
    High = 2,

    /// <summary>
    /// Extra-high reasoning effort.
    /// </summary>
    XHigh = 3,

    /// <summary>
    /// No additional reasoning effort.
    /// </summary>
    None = 4,

    /// <summary>
    /// Minimal reasoning effort.
    /// </summary>
    Minimal = 5,
}
