namespace CodeAlta.Persistence;

/// <summary>
/// Represents a durable artifact identifier.
/// </summary>
public readonly record struct ArtifactId
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ArtifactId"/> struct.
    /// </summary>
    /// <param name="value">The identifier value.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="value"/> is empty.</exception>
    public ArtifactId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Artifact identifier cannot be empty.", nameof(value));
        }

        Value = value;
    }

    /// <summary>
    /// Gets the underlying GUID value.
    /// </summary>
    public Guid Value { get; }

    /// <summary>
    /// Creates a new artifact identifier using UUID v7.
    /// </summary>
    /// <returns>A new <see cref="ArtifactId"/>.</returns>
    public static ArtifactId NewVersion7() => new(Guid.CreateVersion7());

    /// <summary>
    /// Parses an identifier.
    /// </summary>
    /// <param name="value">The identifier string.</param>
    /// <returns>The parsed identifier.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <see langword="null"/>.</exception>
    public static ArtifactId Parse(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new ArtifactId(Guid.Parse(value));
    }

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}
