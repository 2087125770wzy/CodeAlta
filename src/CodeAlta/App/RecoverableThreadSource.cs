using CodeAlta.Agent;
using CodeAlta.Catalog;
using CodeAlta.Orchestration.Runtime;

namespace CodeAlta.App;

internal sealed class RecoverableThreadSource : IRecoverableThreadSource
{
    private readonly WorkThreadRuntimeService _runtimeService;

    public RecoverableThreadSource(WorkThreadRuntimeService runtimeService)
    {
        ArgumentNullException.ThrowIfNull(runtimeService);
        _runtimeService = runtimeService;
    }

    public Func<AgentBackendId, bool>? ShouldListBackendSessions { get; set; }

    public Task<IReadOnlyList<WorkThreadDescriptor>> ListRecoverableThreadsAsync(CancellationToken cancellationToken)
        => _runtimeService.ListRecoverableThreadsAsync(ShouldListBackendSessions, cancellationToken);

    public Task<IReadOnlyList<WorkThreadDescriptor>> ListRecoverableThreadsAsync(
        Func<AgentBackendId, bool>? shouldListBackendSessions,
        CancellationToken cancellationToken)
        => _runtimeService.ListRecoverableThreadsAsync(
            backendId => (ShouldListBackendSessions?.Invoke(backendId) ?? true) &&
                         (shouldListBackendSessions?.Invoke(backendId) ?? true),
            cancellationToken);
}
