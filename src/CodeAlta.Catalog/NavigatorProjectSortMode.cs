namespace CodeAlta.Catalog;

/// <summary>
/// Defines how projects are ordered in the navigator.
/// </summary>
public enum NavigatorProjectSortMode
{
    /// <summary>
    /// Orders projects alphabetically by display name.
    /// </summary>
    Name = 0,

    /// <summary>
    /// Orders projects by most recent activity first.
    /// </summary>
    Date = 1,
}
