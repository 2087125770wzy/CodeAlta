using System.ComponentModel;
using CodeAlta.Workspaces;
using ModelContextProtocol.Server;

namespace CodeAlta.Mcp.Tools;

/// <summary>
/// MCP tools for workspace and scope resolution operations.
/// </summary>
[McpServerToolType]
public sealed class WorkspacesTools
{
    private readonly WorkspaceCatalog _catalog;
    private readonly WorkspaceResolver _resolver;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkspacesTools"/> class.
    /// </summary>
    /// <param name="catalog">Workspace catalog.</param>
    /// <param name="resolver">Workspace resolver.</param>
    /// <exception cref="ArgumentNullException">Thrown when required dependencies are <see langword="null"/>.</exception>
    public WorkspacesTools(WorkspaceCatalog catalog, WorkspaceResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(resolver);

        _catalog = catalog;
        _resolver = resolver;
    }

    /// <summary>
    /// Lists known workspaces.
    /// </summary>
    [McpServerTool(Name = "codealta.workspaces.list"), Description("Lists all known workspaces.")]
    public async Task<string> ListAsync(CancellationToken cancellationToken = default)
    {
        var workspaces = await _catalog.LoadAsync(cancellationToken).ConfigureAwait(false);
        return McpToolJson.Serialize(workspaces.Select(static workspace => new
        {
            workspaceId = workspace.WorkspaceId.ToString(),
            key = workspace.Key,
            displayName = workspace.DisplayName,
            defaultCheckoutRoot = workspace.DefaultCheckoutRoot,
            projectCount = workspace.Projects.Count,
            projects = workspace.Projects.Select(static project => new
            {
                projectId = project.ProjectId.ToString(),
                key = project.Key,
                displayName = project.DisplayName,
                repoUrl = project.RepoUrl,
                defaultBranch = project.DefaultBranch,
            }).ToArray(),
        }).ToArray());
    }

    /// <summary>
    /// Gets a workspace by key.
    /// </summary>
    [McpServerTool(Name = "codealta.workspaces.get"), Description("Gets a workspace descriptor by key.")]
    public async Task<string> GetAsync(
        [Description("Workspace key.")] string workspaceKey,
        CancellationToken cancellationToken = default)
    {
        var workspace = await _catalog.GetByKeyAsync(workspaceKey, cancellationToken).ConfigureAwait(false);
        if (workspace is null)
        {
            throw new InvalidOperationException($"Workspace '{workspaceKey}' was not found.");
        }

        return McpToolJson.Serialize(
            new
            {
                workspaceId = workspace.WorkspaceId.ToString(),
                key = workspace.Key,
                displayName = workspace.DisplayName,
                defaultCheckoutRoot = workspace.DefaultCheckoutRoot,
                projects = workspace.Projects.Select(static project => new
                {
                    projectId = project.ProjectId.ToString(),
                    key = project.Key,
                    displayName = project.DisplayName,
                    repoUrl = project.RepoUrl,
                    defaultBranch = project.DefaultBranch,
                }).ToArray(),
            });
    }

    /// <summary>
    /// Resolves a scope selector into concrete checkout and .codealta roots.
    /// </summary>
    [McpServerTool(Name = "codealta.workspaces.resolve_scope"), Description("Resolves a scope selector into concrete workspace/project roots.")]
    public async Task<string> ResolveScopeAsync(
        [Description("Scope kind: global|workspace|project.")] string kind,
        [Description("Workspace key for workspace scope.")] string? workspaceKey = null,
        [Description("Project key for project scope.")] string? projectKey = null,
        [Description("Optional machine id for applying machine profile overrides.")] string? machineId = null,
        CancellationToken cancellationToken = default)
    {
        var selector = ParseSelector(kind, workspaceKey, projectKey);
        MachineProfile? machineProfile = null;
        if (!string.IsNullOrWhiteSpace(machineId))
        {
            machineProfile = await _catalog.LoadMachineProfileAsync(machineId, cancellationToken).ConfigureAwait(false);
        }

        var resolutions = await _resolver.ResolveAsync(
            selector,
            machineProfile,
            cancellationToken).ConfigureAwait(false);

        return McpToolJson.Serialize(resolutions.Select(static resolution => new
        {
            workspace = new
            {
                workspaceId = resolution.Workspace.WorkspaceId.ToString(),
                key = resolution.Workspace.Key,
                displayName = resolution.Workspace.DisplayName,
            },
            projects = resolution.Projects.Select(static project => new
            {
                projectId = project.Project.ProjectId.ToString(),
                key = project.Project.Key,
                displayName = project.Project.DisplayName,
                repoUrl = project.Project.RepoUrl,
                checkoutPath = project.CheckoutPath,
                codeAltaRoot = project.CodeAltaRoot,
            }).ToArray(),
            codeAltaRoots = resolution.CodeAltaRoots,
        }).ToArray());
    }

    private static ScopeSelector ParseSelector(string kind, string? workspaceKey, string? projectKey)
    {
        return kind.Trim().ToLowerInvariant() switch
        {
            "global" => ScopeSelector.Global(),
            "workspace" => ScopeSelector.Workspace(workspaceKey ?? string.Empty),
            "project" => ScopeSelector.Project(projectKey ?? string.Empty),
            _ => throw new ArgumentException(
                "kind must be one of global, workspace, project.",
                nameof(kind)),
        };
    }
}
