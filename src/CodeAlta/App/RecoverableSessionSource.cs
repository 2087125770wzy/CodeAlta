using CodeAlta.Agent;
using CodeAlta.Catalog;
using CodeAlta.Orchestration.Runtime;

namespace CodeAlta.App;

internal sealed class RecoverableSessionSource : IRecoverableSessionSource
{
    private readonly SessionRuntimeService _runtimeService;

    public RecoverableSessionSource(SessionRuntimeService runtimeService)
    {
        ArgumentNullException.ThrowIfNull(runtimeService);
        _runtimeService = runtimeService;
    }

    public Func<ModelProviderId, bool>? ShouldListProviderSessions { get; set; }

    public IAsyncEnumerable<SessionViewDescriptor> StreamRecoverableSessionsAsync(CancellationToken cancellationToken)
        => _runtimeService.StreamRecoverableSessionsAsync(cancellationToken: cancellationToken);

    public IAsyncEnumerable<SessionViewDescriptor> StreamRecoverableSessionsAsync(
        Func<ModelProviderId, bool>? shouldListProviderSessions,
        CancellationToken cancellationToken)
        => _runtimeService.StreamRecoverableSessionsAsync(
            providerId => (ShouldListProviderSessions?.Invoke(providerId) ?? true) &&
                          (shouldListProviderSessions?.Invoke(providerId) ?? true),
            cancellationToken);

    public Task<IReadOnlyList<SessionViewDescriptor>> ListRecoverableSessionsAsync(CancellationToken cancellationToken)
        => _runtimeService.ListRecoverableSessionsAsync(cancellationToken: cancellationToken);

    public Task<IReadOnlyList<SessionViewDescriptor>> ListRecoverableSessionsAsync(
        Func<ModelProviderId, bool>? shouldListProviderSessions,
        CancellationToken cancellationToken)
        => _runtimeService.ListRecoverableSessionsAsync(
            providerId => (ShouldListProviderSessions?.Invoke(providerId) ?? true) &&
                          (shouldListProviderSessions?.Invoke(providerId) ?? true),
            cancellationToken);
}
