namespace CodeNoesis.Agent;

/// <summary>
/// Represents user input composed of one or more input items.
/// </summary>
/// <param name="Items">The input items.</param>
public sealed record AgentInput(IReadOnlyList<AgentInputItem> Items)
{
    /// <summary>
    /// Creates a text-only input.
    /// </summary>
    /// <param name="text">The text to send.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is <see langword="null"/>.</exception>
    public static AgentInput Text(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return new AgentInput([new AgentInputItem.Text(text)]);
    }
}

