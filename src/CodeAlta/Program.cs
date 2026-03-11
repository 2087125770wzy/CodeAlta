using CodeAlta.Agent;
using CodeAlta.Agent.Codex;
using CodeAlta.Agent.Copilot;
using CodeAlta.Orchestration.Runtime;
using CodeAlta.Persistence;
using CodeAlta.Workspaces;
using CodeAlta.Workspaces.Roles;
using XenoAtom.Logging;

var cancellationTokenSource = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cancellationTokenSource.Cancel();
};

await using var host = await TerminalHost.CreateAsync(cancellationTokenSource.Token).ConfigureAwait(false);
await host.RunAsync(cancellationTokenSource.Token).ConfigureAwait(false);

internal sealed class TerminalHost : IAsyncDisposable
{
    private readonly bool _ownsLogging;
    private readonly CodeAltaDb _db;
    private readonly WorkspaceCatalogOptions _catalogOptions;
    private readonly WorkspaceCatalog _workspaceCatalog;
    private readonly WorkspaceResolver _workspaceResolver;
    private readonly WorkThreadCatalog _threadCatalog;
    private readonly AgentHub _agentHub;
    private readonly WorkThreadRuntimeService _runtimeService;

    private TerminalHost(
        bool ownsLogging,
        CodeAltaDb db,
        WorkspaceCatalogOptions catalogOptions,
        WorkspaceCatalog workspaceCatalog,
        WorkspaceResolver workspaceResolver,
        WorkThreadCatalog threadCatalog,
        AgentHub agentHub,
        WorkThreadRuntimeService runtimeService)
    {
        _ownsLogging = ownsLogging;
        _db = db;
        _catalogOptions = catalogOptions;
        _workspaceCatalog = workspaceCatalog;
        _workspaceResolver = workspaceResolver;
        _threadCatalog = threadCatalog;
        _agentHub = agentHub;
        _runtimeService = runtimeService;
    }

    public static async Task<TerminalHost> CreateAsync(CancellationToken cancellationToken)
    {
        var homeRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".codealta");
        Directory.CreateDirectory(homeRoot);
        var ownsLogging = CodeAltaLogging.Initialize(homeRoot);

        var machineRoot = Path.Combine(homeRoot, "machine");
        Directory.CreateDirectory(machineRoot);

        var db = new CodeAltaDb(
            new CodeAltaDbOptions
            {
                DatabasePath = Path.Combine(machineRoot, "codealta.db"),
            });
        await db.InitializeAsync(cancellationToken).ConfigureAwait(false);

        var agentRepository = new AgentRepository(db);
        var catalogOptions = new WorkspaceCatalogOptions
        {
            GlobalRepoRoot = homeRoot,
        };
        var workspaceCatalog = new WorkspaceCatalog(catalogOptions);
        var workspaceResolver = new WorkspaceResolver(workspaceCatalog);
        var threadCatalog = new WorkThreadCatalog(workspaceCatalog, catalogOptions);
        var roleProfileStore = new RoleProfileStore();
        var instructionTemplateProvider = new AgentInstructionTemplateProvider();

        var backendFactory = new AgentBackendFactory();
        backendFactory.RegisterCodex(new CodexAgentBackendOptions());
        backendFactory.RegisterCopilot(new CopilotAgentBackendOptions());

        var agentHub = new AgentHub(backendFactory, agentRepository);
        var runtimeService = new WorkThreadRuntimeService(
            agentHub,
            workspaceCatalog,
            threadCatalog,
            roleProfileStore,
            instructionTemplateProvider,
            catalogOptions);

        return new TerminalHost(
            ownsLogging,
            db,
            catalogOptions,
            workspaceCatalog,
            workspaceResolver,
            threadCatalog,
            agentHub,
            runtimeService);
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        await using var ui = new CodeAltaTerminalUi(
            _workspaceCatalog,
            _workspaceResolver,
            _threadCatalog,
            _runtimeService,
            _catalogOptions,
            _agentHub);

        await ui.RunAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        await _runtimeService.DisposeAsync().ConfigureAwait(false);
        await _agentHub.DisposeAsync().ConfigureAwait(false);

        GC.KeepAlive(_db);

        if (_ownsLogging)
        {
            LogManager.Shutdown();
        }
    }
}
