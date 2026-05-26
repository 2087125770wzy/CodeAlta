using CodeAlta.Catalog;
using CodeAlta.Orchestration.Runtime;

namespace CodeAlta.App;

internal sealed class SessionDeleter : ISessionDeleter
{
    private readonly SessionRuntimeService _runtimeService;

    public SessionDeleter(SessionRuntimeService runtimeService)
    {
        ArgumentNullException.ThrowIfNull(runtimeService);
        _runtimeService = runtimeService;
    }

    public Task<bool> DeleteSessionAsync(SessionViewDescriptor thread, CancellationToken cancellationToken)
        => _runtimeService.DeleteSessionAsync(thread, cancellationToken);
}
