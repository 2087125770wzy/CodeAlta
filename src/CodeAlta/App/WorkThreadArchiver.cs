using CodeAlta.Catalog;
using CodeAlta.Orchestration.Runtime;

namespace CodeAlta.App;

internal sealed class WorkThreadArchiver : IWorkThreadArchiver
{
    private readonly WorkThreadRuntimeService _runtimeService;

    public WorkThreadArchiver(WorkThreadRuntimeService runtimeService)
    {
        ArgumentNullException.ThrowIfNull(runtimeService);
        _runtimeService = runtimeService;
    }

    public Task<bool> ArchiveThreadAsync(WorkThreadDescriptor thread, CancellationToken cancellationToken)
        => _runtimeService.ArchiveThreadAsync(thread, cancellationToken);
}
