namespace CodeAlta.Persistence;

/// <summary>
/// Represents durable task status values.
/// </summary>
public enum TaskStatus
{
    /// <summary>
    /// Task is pending.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Task is actively in progress.
    /// </summary>
    InProgress = 1,

    /// <summary>
    /// Task completed successfully.
    /// </summary>
    Completed = 2,

    /// <summary>
    /// Task is blocked.
    /// </summary>
    Blocked = 3,

    /// <summary>
    /// Task was cancelled.
    /// </summary>
    Cancelled = 4,
}
