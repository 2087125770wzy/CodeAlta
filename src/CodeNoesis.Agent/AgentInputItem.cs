namespace CodeNoesis.Agent;

/// <summary>
/// Represents a typed user input item (text, image, attachment, skill, etc.).
/// </summary>
public abstract record AgentInputItem
{
    /// <summary>
    /// A plain text input.
    /// </summary>
    /// <param name="Value">The text content.</param>
    public sealed record Text(string Value) : AgentInputItem;

    /// <summary>
    /// An image URL input.
    /// </summary>
    /// <param name="Url">The image URL.</param>
    public sealed record ImageUrl(string Url) : AgentInputItem;

    /// <summary>
    /// A local image path input.
    /// </summary>
    /// <param name="Path">The local path to an image.</param>
    public sealed record LocalImage(string Path) : AgentInputItem;

    /// <summary>
    /// A file attachment input.
    /// </summary>
    /// <param name="Path">The file path.</param>
    /// <param name="DisplayName">Optional display name.</param>
    /// <param name="LineRange">Optional line range.</param>
    public sealed record File(string Path, string? DisplayName = null, AgentLineRange? LineRange = null) : AgentInputItem;

    /// <summary>
    /// A directory attachment input.
    /// </summary>
    /// <param name="Path">The directory path.</param>
    /// <param name="DisplayName">Optional display name.</param>
    /// <param name="LineRange">Optional line range.</param>
    public sealed record Directory(string Path, string? DisplayName = null, AgentLineRange? LineRange = null) : AgentInputItem;

    /// <summary>
    /// A code selection attachment input.
    /// </summary>
    /// <param name="FilePath">The selection file path.</param>
    /// <param name="DisplayName">The selection display name.</param>
    /// <param name="SelectedText">The selected text.</param>
    /// <param name="Range">The selection range.</param>
    public sealed record Selection(string FilePath, string DisplayName, string SelectedText, AgentSelectionRange Range) : AgentInputItem;

    /// <summary>
    /// A skill reference input.
    /// </summary>
    /// <param name="Name">The skill name.</param>
    /// <param name="Path">The skill path.</param>
    public sealed record Skill(string Name, string Path) : AgentInputItem;

    /// <summary>
    /// A mention input (e.g., app invocation).
    /// </summary>
    /// <param name="Name">The mention name.</param>
    /// <param name="Path">The mention path.</param>
    public sealed record Mention(string Name, string Path) : AgentInputItem;
}
