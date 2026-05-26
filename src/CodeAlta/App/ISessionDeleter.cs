using CodeAlta.Catalog;

namespace CodeAlta.App;

internal interface ISessionDeleter
{
    Task<bool> DeleteSessionAsync(SessionViewDescriptor thread, CancellationToken cancellationToken);
}
