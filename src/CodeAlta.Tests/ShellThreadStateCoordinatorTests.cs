using CodeAlta.Agent;
using CodeAlta.App;
using CodeAlta.Catalog;
using CodeAlta.Threading;

namespace CodeAlta.Tests;

[TestClass]
public sealed class ShellThreadStateCoordinatorTests
{
    [TestMethod]
    public void ApplyRecoveredCatalogState_AppliesPersistedThreadLocalState()
    {
        using var temp = TempDirectory.Create();
        var options = new CatalogOptions { GlobalRoot = temp.Path };
        var coordinator = CreateCoordinator(options);
        coordinator.ViewState = new WorkThreadViewState
        {
            ThreadStates = new Dictionary<string, WorkThreadLocalState>(StringComparer.OrdinalIgnoreCase)
            {
                ["thread-1"] = new WorkThreadLocalState
                {
                    Archived = true,
                    MessageCount = 12,
                },
            },
        };

        coordinator.ApplyRecoveredCatalogState([], [CreateThread("thread-1")]);

        var thread = coordinator.Threads.Single();
        Assert.AreEqual(WorkThreadStatus.Archived, thread.Status);
        Assert.AreEqual(12, thread.MessageCount);
    }

    [TestMethod]
    public async Task PersistThreadLocalStateAsync_StoresArchivedAndMessageCountInViewState()
    {
        using var temp = TempDirectory.Create();
        var options = new CatalogOptions { GlobalRoot = temp.Path };
        var threadCatalog = new WorkThreadCatalog(options);
        var coordinator = CreateCoordinator(options, threadCatalog);
        coordinator.ViewState = new WorkThreadViewState();

        var thread = CreateThread("thread-1");
        thread.Status = WorkThreadStatus.Archived;
        thread.MessageCount = 6;
        await coordinator.PersistThreadLocalStateAsync(thread).ConfigureAwait(false);

        var reloaded = await threadCatalog.LoadViewStateAsync().ConfigureAwait(false);
        Assert.IsTrue(reloaded.ThreadStates["thread-1"].Archived);
        Assert.AreEqual(6, reloaded.ThreadStates["thread-1"].MessageCount);
    }

    private static ShellThreadStateCoordinator CreateCoordinator(CatalogOptions options, WorkThreadCatalog? threadCatalog = null)
    {
        threadCatalog ??= new WorkThreadCatalog(options);
        return new ShellThreadStateCoordinator(
            new ProjectCatalog(options),
            threadCatalog,
            static () => new InlineUiDispatcher(),
            static () => null,
            static _ => true,
            static _ => { },
            static (_, _, _, _, _) => { },
            static (_, _) => Task.CompletedTask,
            static () => { },
            static () => { },
            static () => { },
            static _ => { },
            static (_, _, _) => { });
    }

    private static WorkThreadDescriptor CreateThread(string threadId)
    {
        var timestamp = DateTimeOffset.Parse("2026-03-29T12:00:00+00:00");
        return new WorkThreadDescriptor
        {
            ThreadId = threadId,
            Kind = WorkThreadKind.ProjectThread,
            BackendId = AgentBackendIds.Codex.Value,
            BackendSessionId = $"session-{threadId}",
            ProjectRef = "project-1",
            WorkingDirectory = @"C:\repo",
            Title = "Test thread",
            Status = WorkThreadStatus.Active,
            CreatedAt = timestamp,
            UpdatedAt = timestamp,
            LastActiveAt = timestamp,
        };
    }

    private sealed class InlineUiDispatcher : IUiDispatcher
    {
        public bool CheckAccess() => true;

        public void Post(Action action)
        {
            ArgumentNullException.ThrowIfNull(action);
            action();
        }

        public Task InvokeAsync(Action action)
        {
            ArgumentNullException.ThrowIfNull(action);
            action();
            return Task.CompletedTask;
        }

        public Task<T> InvokeAsync<T>(Func<T> action)
        {
            ArgumentNullException.ThrowIfNull(action);
            return Task.FromResult(action());
        }
    }

    private sealed class TempDirectory : IDisposable
    {
        private TempDirectory(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static TempDirectory Create()
        {
            var path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"CodeAlta.Tests.{Guid.NewGuid():N}");
            Directory.CreateDirectory(path);
            return new TempDirectory(path);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                {
                    Directory.Delete(Path, recursive: true);
                }
            }
            catch
            {
            }
        }
    }
}
