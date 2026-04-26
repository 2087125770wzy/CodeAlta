using CodeAlta.Agent;
using CodeAlta.Catalog;

namespace CodeAlta.App;

internal interface IRecoverableThreadSource
{
    Task<IReadOnlyList<WorkThreadDescriptor>> ListRecoverableThreadsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<WorkThreadDescriptor>> ListRecoverableThreadsAsync(
        Func<AgentBackendId, bool>? shouldListBackendSessions,
        CancellationToken cancellationToken)
        => ListRecoverableThreadsAsync(cancellationToken);
}
