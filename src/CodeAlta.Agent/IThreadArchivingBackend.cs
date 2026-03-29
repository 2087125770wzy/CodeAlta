namespace CodeAlta.Agent;

/// <summary>
/// Represents a backend that can archive backend-owned threads.
/// </summary>
public interface IThreadArchivingBackend
{
    /// <summary>
    /// Attempts to archive a backend-owned thread.
    /// </summary>
    /// <param name="threadId">The backend thread identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true"/> when the backend archived the thread; otherwise <see langword="false"/>.</returns>
    Task<bool> TryArchiveThreadAsync(string threadId, CancellationToken cancellationToken = default);
}
