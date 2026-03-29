using CodeAlta.Catalog;

namespace CodeAlta.App;

internal interface IProjectCatalogStore : IProjectCatalogLoader
{
    Task<ProjectDescriptor?> GetByIdAsync(string projectId, CancellationToken cancellationToken);

    Task SaveAsync(ProjectDescriptor project, CancellationToken cancellationToken);
}
