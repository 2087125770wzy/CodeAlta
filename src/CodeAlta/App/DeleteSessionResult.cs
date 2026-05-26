namespace CodeAlta.App;

internal sealed record DeleteSessionResult(
    IReadOnlyList<string> DeletedThreadIds,
    bool DeletedByBackend);
