namespace CodeAlta.Persistence;

/// <summary>
/// Represents a durable task identifier.
/// </summary>
public readonly record struct TaskId
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TaskId"/> struct.
    /// </summary>
    /// <param name="value">The identifier value.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="value"/> is empty.</exception>
    public TaskId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Task identifier cannot be empty.", nameof(value));
        }

        Value = value;
    }

    /// <summary>
    /// Gets the underlying GUID value.
    /// </summary>
    public Guid Value { get; }

    /// <summary>
    /// Creates a new task identifier using UUID v7.
    /// </summary>
    /// <returns>A new <see cref="TaskId"/>.</returns>
    public static TaskId NewVersion7() => new(Guid.CreateVersion7());

    /// <summary>
    /// Parses an identifier.
    /// </summary>
    /// <param name="value">The identifier string.</param>
    /// <returns>The parsed identifier.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <see langword="null"/>.</exception>
    public static TaskId Parse(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new TaskId(Guid.Parse(value));
    }

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}
