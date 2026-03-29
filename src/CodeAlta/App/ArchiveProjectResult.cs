namespace CodeAlta.App;

internal sealed record ArchiveProjectResult(
    string ProjectId,
    IReadOnlyList<string> ArchivedThreadIds);
