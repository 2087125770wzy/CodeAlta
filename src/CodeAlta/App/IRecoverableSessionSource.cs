using CodeAlta.Agent;
using CodeAlta.Catalog;

namespace CodeAlta.App;

internal interface IRecoverableSessionSource
{
    IAsyncEnumerable<SessionViewDescriptor> StreamRecoverableSessionsAsync(CancellationToken cancellationToken);

    IAsyncEnumerable<SessionViewDescriptor> StreamRecoverableSessionsAsync(
        Func<ModelProviderId, bool>? shouldListProviderSessions,
        CancellationToken cancellationToken)
        => StreamRecoverableSessionsAsync(cancellationToken);

    Task<IReadOnlyList<SessionViewDescriptor>> ListRecoverableSessionsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<SessionViewDescriptor>> ListRecoverableSessionsAsync(
        Func<ModelProviderId, bool>? shouldListProviderSessions,
        CancellationToken cancellationToken)
        => ListRecoverableSessionsAsync(cancellationToken);
}
