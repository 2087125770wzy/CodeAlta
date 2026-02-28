namespace CodeAlta.Persistence;

/// <summary>
/// Options for initializing the CodeAlta SQLite database.
/// </summary>
public sealed class CodeAltaDbOptions
{
    /// <summary>
    /// Gets or sets the full database file path.
    /// </summary>
    public string DatabasePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the optional sqlite-vec extension path.
    /// </summary>
    public string? SqliteVecExtensionPath { get; set; }

    /// <summary>
    /// Gets or sets whether sqlite-vec extension loading is required.
    /// </summary>
    public bool RequireSqliteVec { get; set; }

    /// <summary>
    /// Gets or sets whether SQLite connection pooling is enabled.
    /// </summary>
    public bool EnablePooling { get; set; }
}
