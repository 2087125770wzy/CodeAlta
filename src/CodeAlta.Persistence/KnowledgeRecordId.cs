namespace CodeAlta.Persistence;

/// <summary>
/// Represents a knowledge record identifier.
/// </summary>
public readonly record struct KnowledgeRecordId
{
    /// <summary>
    /// Initializes a new instance of the <see cref="KnowledgeRecordId"/> struct.
    /// </summary>
    /// <param name="value">The identifier value.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="value"/> is empty.</exception>
    public KnowledgeRecordId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Knowledge record identifier cannot be empty.", nameof(value));
        }

        Value = value;
    }

    /// <summary>
    /// Gets the underlying GUID value.
    /// </summary>
    public Guid Value { get; }

    /// <summary>
    /// Creates a new knowledge record identifier using UUID v7.
    /// </summary>
    /// <returns>A new <see cref="KnowledgeRecordId"/>.</returns>
    public static KnowledgeRecordId NewVersion7() => new(Guid.CreateVersion7());

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}
