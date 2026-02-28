using SharpYaml.Serialization;

namespace CodeAlta.Persistence;

/// <summary>
/// Defines YAML frontmatter metadata for markdown artifacts.
/// </summary>
public sealed class ArtifactFrontmatter
{
    /// <summary>
    /// Gets or sets artifact identifier.
    /// </summary>
    [YamlMember("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets artifact type.
    /// </summary>
    [YamlMember("type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets optional title.
    /// </summary>
    [YamlMember("title")]
    public string? Title { get; set; }

    /// <summary>
    /// Gets or sets optional workspace identifier.
    /// </summary>
    [YamlMember("workspace_id")]
    public string? WorkspaceId { get; set; }

    /// <summary>
    /// Gets or sets optional workspace key.
    /// </summary>
    [YamlMember("workspace_key")]
    public string? WorkspaceKey { get; set; }

    /// <summary>
    /// Gets or sets optional project identifier.
    /// </summary>
    [YamlMember("project_id")]
    public string? ProjectId { get; set; }

    /// <summary>
    /// Gets or sets optional project key.
    /// </summary>
    [YamlMember("project_key")]
    public string? ProjectKey { get; set; }

    /// <summary>
    /// Gets or sets source metadata.
    /// </summary>
    [YamlMember("source")]
    public ArtifactSourceInfo? Source { get; set; }

    /// <summary>
    /// Gets or sets tags.
    /// </summary>
    [YamlMember("tags")]
    public List<string> Tags { get; set; } = [];

    /// <summary>
    /// Gets or sets structured links.
    /// </summary>
    [YamlMember("links")]
    public ArtifactLinks? Links { get; set; }

    /// <summary>
    /// Gets or sets creation timestamp in UTC.
    /// </summary>
    [YamlMember("created_at")]
    public string CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToString("O");

    /// <summary>
    /// Gets or sets last update timestamp in UTC.
    /// </summary>
    [YamlMember("updated_at")]
    public string UpdatedAt { get; set; } = DateTimeOffset.UtcNow.ToString("O");
}

/// <summary>
/// Describes the producer of an artifact.
/// </summary>
public sealed class ArtifactSourceInfo
{
    /// <summary>
    /// Gets or sets source kind.
    /// </summary>
    [YamlMember("kind")]
    public string? Kind { get; set; }

    /// <summary>
    /// Gets or sets optional agent identifier.
    /// </summary>
    [YamlMember("agent_id")]
    public string? AgentId { get; set; }
}

/// <summary>
/// Represents structured links in artifact frontmatter.
/// </summary>
public sealed class ArtifactLinks
{
    /// <summary>
    /// Gets or sets linked task identifiers.
    /// </summary>
    [YamlMember("tasks")]
    public List<string> Tasks { get; set; } = [];

    /// <summary>
    /// Gets or sets linked file references.
    /// </summary>
    [YamlMember("files")]
    public List<ArtifactFileLink> Files { get; set; } = [];
}

/// <summary>
/// Represents a linked file entry.
/// </summary>
public sealed class ArtifactFileLink
{
    /// <summary>
    /// Gets or sets file path.
    /// </summary>
    [YamlMember("path")]
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets optional line range.
    /// </summary>
    [YamlMember("range")]
    public ArtifactLineRange? Range { get; set; }
}

/// <summary>
/// Represents a source line range.
/// </summary>
public sealed class ArtifactLineRange
{
    /// <summary>
    /// Gets or sets start line (1-based).
    /// </summary>
    [YamlMember("startLine")]
    public int StartLine { get; set; }

    /// <summary>
    /// Gets or sets end line (1-based).
    /// </summary>
    [YamlMember("endLine")]
    public int EndLine { get; set; }
}
