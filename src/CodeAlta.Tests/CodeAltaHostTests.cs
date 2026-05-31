using CodeAlta.Agent;
using CodeAlta.Catalog;
using CodeAlta.Orchestration.Hosting;
using CodeAlta.Orchestration.Runtime;

namespace CodeAlta.Tests;

[TestClass]
public sealed class CodeAltaHostTests
{
    [TestMethod]
    public async Task CreateAsync_HeadlessWithoutPlugins_ConstructsAndDisposesRuntimeServices()
    {
        using var temp = TempDirectory.Create();
        var projectRoot = Path.Combine(temp.Path, "project");
        Directory.CreateDirectory(projectRoot);
        var options = new CodeAltaHostOptions
        {
            GlobalRoot = Path.Combine(temp.Path, "home"),
            CurrentProjectPath = projectRoot,
            IsHeadless = true,
            HasInteractiveUi = false,
            PluginSafeMode = true,
            StartPlugins = false,
            RawArguments = ["--headless"],
        };

        await using var host = await CodeAltaHost.CreateAsync(options, CancellationToken.None);

        Assert.AreEqual(Path.GetFullPath(options.GlobalRoot), host.CatalogOptions.GlobalRoot);
        Assert.AreEqual(projectRoot, host.CurrentProject.ProjectPath);
        Assert.IsFalse((await host.ProjectCatalog.LoadAsync(CancellationToken.None).ConfigureAwait(false)).Any());
        Assert.IsNotNull(host.ProjectCatalog);
        Assert.IsNotNull(host.SessionViewCatalog);
        Assert.IsNotNull(host.SkillCatalog);
        Assert.IsNotNull(host.AgentHub);
        Assert.IsNotNull(host.RuntimeService);
        Assert.IsNotNull(host.ProjectFileSearchService);
        Assert.IsNotNull(host.PluginRuntime);
    }

    [TestMethod]
    public async Task CreateAsync_ConfiguresHostRegisteredModelProviderRuntimes()
    {
        using var temp = TempDirectory.Create();
        var projectRoot = Path.Combine(temp.Path, "project");
        Directory.CreateDirectory(projectRoot);
        var ProviderId = new ModelProviderId("test-provider");
        var options = new CodeAltaHostOptions
        {
            GlobalRoot = Path.Combine(temp.Path, "home"),
            CurrentProjectPath = projectRoot,
            IsHeadless = true,
            HasInteractiveUi = false,
            PluginSafeMode = true,
            StartPlugins = false,
            ConfigureModelProviders = registry => registry.RegisterOrReplaceSessionRuntime(new ModelProviderDescriptor(new ModelProviderId(ProviderId.Value), "Test Provider"), () => new TestModelProviderRuntime(ProviderId)),
        };

        await using var host = await CodeAltaHost.CreateAsync(options, CancellationToken.None);

        var handle = await host.AgentHub.StartSessionAsync(
                new AgentSessionCreateOptions
                {
                    ProviderKey = ProviderId.Value,
                    WorkingDirectory = projectRoot,
                    OnPermissionRequest = static (_, _) => Task.FromResult(new AgentPermissionDecision(AgentPermissionDecisionKind.AllowOnce)),
                },
                CancellationToken.None)
            .ConfigureAwait(false);

        Assert.AreEqual("test-session", handle.SessionId);
    }

    [TestMethod]
    public async Task CreateProjectSessionAsync_PersistsTransientCurrentProject()
    {
        using var temp = TempDirectory.Create();
        var projectRoot = Path.Combine(temp.Path, "project");
        Directory.CreateDirectory(projectRoot);
        var providerId = new ModelProviderId("test-provider");
        var options = new CodeAltaHostOptions
        {
            GlobalRoot = Path.Combine(temp.Path, "home"),
            CurrentProjectPath = projectRoot,
            IsHeadless = true,
            HasInteractiveUi = false,
            PluginSafeMode = true,
            StartPlugins = false,
            ConfigureModelProviders = registry => registry.RegisterOrReplaceSessionRuntime(new ModelProviderDescriptor(new ModelProviderId(providerId.Value), "Test Provider"), () => new TestModelProviderRuntime(providerId)),
        };

        await using var host = await CodeAltaHost.CreateAsync(options, CancellationToken.None);
        Assert.IsFalse((await host.ProjectCatalog.LoadAsync(CancellationToken.None).ConfigureAwait(false)).Any());

        var session = await host.RuntimeService.CreateProjectSessionAsync(
                host.CurrentProject,
                new SessionExecutionOptions
                {
                    ProviderId = providerId,
                    ProviderKey = providerId.Value,
                    WorkingDirectory = projectRoot,
                    ProjectRoots = [projectRoot],
                    OnPermissionRequest = static (_, _) => Task.FromResult(new AgentPermissionDecision(AgentPermissionDecisionKind.AllowOnce)),
                },
                title: "Test session",
                CancellationToken.None)
            .ConfigureAwait(false);

        var projects = await host.ProjectCatalog.LoadAsync(CancellationToken.None).ConfigureAwait(false);
        Assert.AreEqual(1, projects.Count);
        Assert.AreEqual(host.CurrentProject.Id, projects[0].Id);
        Assert.AreEqual(projects[0].Id, session.ProjectRef);
    }

    [TestMethod]
    public async Task CreateProjectSessionAsync_DoesNotKeepTransientProjectWhenSessionStartFails()
    {
        using var temp = TempDirectory.Create();
        var projectRoot = Path.Combine(temp.Path, "project");
        Directory.CreateDirectory(projectRoot);
        var providerId = new ModelProviderId("test-provider");
        var options = new CodeAltaHostOptions
        {
            GlobalRoot = Path.Combine(temp.Path, "home"),
            CurrentProjectPath = projectRoot,
            IsHeadless = true,
            HasInteractiveUi = false,
            PluginSafeMode = true,
            StartPlugins = false,
            ConfigureModelProviders = registry => registry.RegisterOrReplaceSessionRuntime(new ModelProviderDescriptor(new ModelProviderId(providerId.Value), "Test Provider"), () => new FailingModelProviderRuntime(providerId)),
        };

        await using var host = await CodeAltaHost.CreateAsync(options, CancellationToken.None).ConfigureAwait(false);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => host.RuntimeService.CreateProjectSessionAsync(
            host.CurrentProject,
            new SessionExecutionOptions
            {
                ProviderId = providerId,
                ProviderKey = providerId.Value,
                WorkingDirectory = projectRoot,
                ProjectRoots = [projectRoot],
                OnPermissionRequest = static (_, _) => Task.FromResult(new AgentPermissionDecision(AgentPermissionDecisionKind.AllowOnce)),
            },
            title: "Test session",
            CancellationToken.None));

        Assert.IsFalse((await host.ProjectCatalog.LoadAsync(CancellationToken.None).ConfigureAwait(false)).Any());
    }

    [TestMethod]
    public async Task CreateAsync_ArchivedCurrentProjectIsVisibleInMemoryWithoutSaving()
    {
        using var temp = TempDirectory.Create();
        var projectRoot = Path.Combine(temp.Path, "project");
        Directory.CreateDirectory(projectRoot);
        var catalogOptions = new CatalogOptions { GlobalRoot = Path.Combine(temp.Path, "home") };
        var archivedProject = new ProjectDescriptor
        {
            Id = ProjectId.NewVersion7().ToString(),
            Slug = "project",
            Name = "project",
            DisplayName = "project",
            ProjectPath = projectRoot,
            Archived = true,
        };
        await new ProjectCatalog(catalogOptions).SaveAsync(archivedProject, CancellationToken.None).ConfigureAwait(false);
        var options = new CodeAltaHostOptions
        {
            GlobalRoot = catalogOptions.GlobalRoot,
            CurrentProjectPath = projectRoot,
            IsHeadless = true,
            HasInteractiveUi = false,
            PluginSafeMode = true,
            StartPlugins = false,
        };

        await using var host = await CodeAltaHost.CreateAsync(options, CancellationToken.None).ConfigureAwait(false);

        Assert.IsFalse(host.CurrentProject.Archived);
        var persisted = await host.ProjectCatalog.GetByIdAsync(archivedProject.Id, CancellationToken.None).ConfigureAwait(false);
        Assert.IsNotNull(persisted);
        Assert.IsTrue(persisted.Archived);
    }

    private sealed class TestModelProviderRuntime(ModelProviderId ProviderId) : ITestModelProviderSessionRuntime
    {
        public ModelProviderId ProviderId { get; } = ProviderId;

        public string DisplayName => "Test Provider";

        public Task StartAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<AgentModelInfo>> ListModelsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<AgentModelInfo>>(Array.Empty<AgentModelInfo>());

        public async IAsyncEnumerable<AgentSessionMetadata> ListSessionsAsync(
            AgentSessionListFilter? filter = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.CompletedTask;
            yield break;
        }

        public Task<IAgentSession> CreateSessionAsync(
            AgentSessionCreateOptions options,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IAgentSession>(new TestAgentSession(ProviderId, options.SessionId ?? "test-session"));

        public Task<IAgentSession> ResumeSessionAsync(
            string sessionId,
            AgentSessionResumeOptions options,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public ValueTask DisposeAsync()
            => ValueTask.CompletedTask;

        private sealed class TestAgentSession(ModelProviderId ProviderId, string sessionId) : IAgentSession
        {
            public ModelProviderId ProviderId { get; } = ProviderId;

            public string SessionId { get; } = sessionId;

            public string? WorkspacePath => null;

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;

            public async IAsyncEnumerable<AgentEvent> StreamEventsAsync(
                [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.CompletedTask.ConfigureAwait(false);
                yield break;
            }

            public IDisposable Subscribe(Action<AgentEvent> handler) => NullSubscription.Instance;

            public Task<AgentRunId> SendAsync(AgentSendOptions options, CancellationToken cancellationToken = default)
                => Task.FromResult(new AgentRunId("test-run"));

            public Task<AgentRunId> SteerAsync(AgentSteerOptions options, CancellationToken cancellationToken = default)
                => Task.FromResult(options.ExpectedRunId ?? new AgentRunId("test-run"));

            public Task AbortAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

            public Task CompactAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

            public Task<IReadOnlyList<AgentEvent>> GetHistoryAsync(CancellationToken cancellationToken = default)
                => Task.FromResult<IReadOnlyList<AgentEvent>>([]);
        }

        private sealed class NullSubscription : IDisposable
        {
            public static readonly NullSubscription Instance = new();

            public void Dispose()
            {
            }
        }
    }

    private sealed class FailingModelProviderRuntime(ModelProviderId ProviderId) : ITestModelProviderSessionRuntime
    {
        public ModelProviderId ProviderId { get; } = ProviderId;

        public string DisplayName => "Test Provider";

        public Task StartAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<AgentModelInfo>> ListModelsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<AgentModelInfo>>(Array.Empty<AgentModelInfo>());

        public async IAsyncEnumerable<AgentSessionMetadata> ListSessionsAsync(
            AgentSessionListFilter? filter = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.CompletedTask;
            yield break;
        }

        public Task<IAgentSession> CreateSessionAsync(
            AgentSessionCreateOptions options,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Session start failed.");

        public Task<IAgentSession> ResumeSessionAsync(
            string sessionId,
            AgentSessionResumeOptions options,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public ValueTask DisposeAsync()
            => ValueTask.CompletedTask;
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "CodeAltaHostTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public static TempDirectory Create() => new();

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                {
                    Directory.Delete(Path, recursive: true);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
