using CodeAlta.Catalog;

namespace CodeAlta.App;

internal interface IWorkThreadArchiver
{
    Task<bool> ArchiveThreadAsync(WorkThreadDescriptor thread, CancellationToken cancellationToken);
}
