using CodeAlta.Agent;
using CodeAlta.Orchestration.Runtime;
using CodeAlta.Orchestration;
using CodeAlta.Persistence;

namespace CodeAlta.Tests;

[TestClass]
public sealed class AgentHubBackendReloadTests
{
    [TestMethod]
    public async Task UnloadBackendAsync_DisposesCachedBackendWithoutActiveSession()
    {
        using var temp = TempDirectory.Create();
        var db = await CreateDbAsync(temp.Path).ConfigureAwait(false);
        var repository = new AgentRepository(db);
        var backendFactory = new AgentBackendFactory();
        var backend = new ReloadableBackend();
        backendFactory.Register("reloadable", () => backend);

        await using var hub = new AgentHub(backendFactory, repository);

        var models = await hub.ListModelsAsync(new AgentBackendId("reloadable")).ConfigureAwait(false);
        Assert.AreEqual(1, models.Count);

        var unloaded = await hub.UnloadBackendAsync(new AgentBackendId("reloadable")).ConfigureAwait(false);

        Assert.IsTrue(unloaded);
        Assert.AreEqual(1, backend.StopCount);
        Assert.AreEqual(1, backend.DisposeCount);
    }

    [TestMethod]
    public async Task UnloadBackendAsync_ThrowsWhenBackendHasActiveSession()
    {
        using var temp = TempDirectory.Create();
        var db = await CreateDbAsync(temp.Path).ConfigureAwait(false);
        var repository = new AgentRepository(db);
        var backendFactory = new AgentBackendFactory();
        var backend = new ReloadableBackend();
        backendFactory.Register("reloadable", () => backend);

        await using var hub = new AgentHub(backendFactory, repository);
        var agent = await hub.RegisterAgentAsync(
                "test",
                new AgentScope
                {
                    Kind = AgentScopeKind.Global,
                    Id = "global",
                },
                new AgentBackendId("reloadable"))
            .ConfigureAwait(false);
        _ = await hub.StartSessionAsync(
                agent.AgentId,
                new AgentSessionCreateOptions
                {
                    WorkingDirectory = Environment.CurrentDirectory,
                    OnPermissionRequest = static (_, _) =>
                        Task.FromResult(new AgentPermissionDecision(AgentPermissionDecisionKind.AllowOnce)),
                })
            .ConfigureAwait(false);

        var exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
                () => hub.UnloadBackendAsync(new AgentBackendId("reloadable")))
            .ConfigureAwait(false);

        StringAssert.Contains(exception.Message, "active sessions");
    }

    private static async Task<CodeAltaDb> CreateDbAsync(string rootPath)
    {
        var dbPath = Path.Combine(rootPath, "state", "db", "codealta.db");
        var db = new CodeAltaDb(new CodeAltaDbOptions { DatabasePath = dbPath });
        await db.InitializeAsync().ConfigureAwait(false);
        return db;
    }

    private sealed class ReloadableBackend : IAgentBackend
    {
        public AgentBackendId BackendId => new("reloadable");

        public string DisplayName => "Reloadable";

        public int StopCount { get; private set; }

        public int DisposeCount { get; private set; }

        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            StopCount++;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<AgentModelInfo>> ListModelsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<AgentModelInfo>>([new AgentModelInfo("model-a")]);

        public Task<IReadOnlyList<AgentSessionMetadata>> ListSessionsAsync(
            AgentSessionListFilter? filter = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<AgentSessionMetadata>>([]);

        public Task<IAgentSession> CreateSessionAsync(
            AgentSessionCreateOptions options,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IAgentSession>(new ReloadableSession(this));

        public Task<IAgentSession> ResumeSessionAsync(
            string sessionId,
            AgentSessionResumeOptions options,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IAgentSession>(new ReloadableSession(this, sessionId));

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }

        private sealed class ReloadableSession : IAgentSession
        {
            private readonly ReloadableBackend _backend;

            public ReloadableSession(ReloadableBackend backend, string? sessionId = null)
            {
                _backend = backend;
                SessionId = sessionId ?? "reloadable-session";
            }

            public AgentBackendId BackendId => _backend.BackendId;

            public string SessionId { get; }

            public string? WorkspacePath => null;

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;

            public async IAsyncEnumerable<AgentEvent> StreamEventsAsync(
                [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
            {
                await Task.CompletedTask.ConfigureAwait(false);
                yield break;
            }

            public IDisposable Subscribe(Action<AgentEvent> handler)
            {
                ArgumentNullException.ThrowIfNull(handler);
                return DisposableAction.Create(static () => { });
            }

            public Task<AgentRunId> SendAsync(AgentSendOptions options, CancellationToken cancellationToken = default)
                => Task.FromResult(new AgentRunId("reloadable-run"));

            public Task<AgentRunId> SteerAsync(AgentSteerOptions options, CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task AbortAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

            public Task CompactAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

            public Task<IReadOnlyList<AgentEvent>> GetHistoryAsync(CancellationToken cancellationToken = default)
                => Task.FromResult<IReadOnlyList<AgentEvent>>([]);
        }
    }

    private sealed class DisposableAction(Action dispose) : IDisposable
    {
        private bool _disposed;

        public static IDisposable Create(Action dispose)
        {
            ArgumentNullException.ThrowIfNull(dispose);
            return new DisposableAction(dispose);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            dispose();
        }
    }

    private sealed class TempDirectory(string path) : IDisposable
    {
        public string Path { get; } = path;

        public static TempDirectory Create()
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"codealta-agenthub-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(path);
            return new TempDirectory(path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
