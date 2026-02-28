namespace CodeAlta.Persistence;

/// <summary>
/// Represents an artifact markdown document and its frontmatter.
/// </summary>
public sealed record ArtifactDocument
{
    /// <summary>
    /// Gets or sets frontmatter metadata.
    /// </summary>
    public required ArtifactFrontmatter Frontmatter { get; set; }

    /// <summary>
    /// Gets or sets markdown body content.
    /// </summary>
    public string Body { get; set; } = string.Empty;
}
