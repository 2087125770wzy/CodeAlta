using System.Text.Json;
using CodeAlta.Mcp;
using CodeAlta.Persistence;
using CodeAlta.Search;
using CodeAlta.Workspaces;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;

namespace CodeAlta.Mcp.Tests;

[TestClass]
public sealed class McpInfrastructureTests
{
    [TestMethod]
    public async Task Mcp_InProcess_CanListTools()
    {
        await using var context = await TestContext.CreateAsync().ConfigureAwait(false);
        var tools = await context.Connection.Client.ListToolsAsync().ConfigureAwait(false);

        var names = tools.Select(static x => x.Name).ToArray();
        CollectionAssert.Contains(names, "codealta.tasks.create");
        CollectionAssert.Contains(names, "codealta.tasks.get");
        CollectionAssert.Contains(names, "codealta.artifacts.write_markdown");
        CollectionAssert.Contains(names, "codealta.search.query");
        CollectionAssert.Contains(names, "codealta.workspaces.resolve_scope");
        CollectionAssert.Contains(names, "codealta.agents.register");
    }

    [TestMethod]
    public async Task Mcp_Tasks_CreateThenGet_RoundTrips()
    {
        await using var context = await TestContext.CreateAsync().ConfigureAwait(false);

        var createResult = await context.Connection.Client.CallToolAsync(
            "codealta.tasks.create",
            new Dictionary<string, object?>
            {
                ["title"] = "Implement MCP tools",
                ["workspaceId"] = "workspace-1",
                ["projectId"] = "project-1",
            }).ConfigureAwait(false);

        var createPayload = ParseJson(ReadTextContent(createResult));
        var taskId = createPayload.RootElement.GetProperty("taskId").GetString();
        Assert.IsFalse(string.IsNullOrWhiteSpace(taskId));

        var getResult = await context.Connection.Client.CallToolAsync(
            "codealta.tasks.get",
            new Dictionary<string, object?>
            {
                ["taskId"] = taskId!,
            }).ConfigureAwait(false);

        var getPayload = ParseJson(ReadTextContent(getResult));
        var task = getPayload.RootElement.GetProperty("task");
        Assert.AreEqual(taskId, task.GetProperty("taskId").GetString());
        Assert.AreEqual("Implement MCP tools", task.GetProperty("title").GetString());
        Assert.AreEqual("pending", task.GetProperty("status").GetString());
    }

    [TestMethod]
    public async Task Mcp_Search_Query_ReturnsLinkedArtifacts()
    {
        await using var context = await TestContext.CreateAsync().ConfigureAwait(false);

        var artifactUri = "artifact://wk-core/knowledge/perf";
        await context.Connection.Client.CallToolAsync(
            "codealta.search.index",
            new Dictionary<string, object?>
            {
                ["sourceKind"] = "artifact",
                ["sourceId"] = artifactUri,
                ["title"] = "Performance Notes",
                ["text"] = "Use Span<T> and ArrayPool<T> to reduce allocations.",
                ["workspaceId"] = "workspace-1",
                ["projectId"] = "project-1",
                ["processNow"] = true,
            }).ConfigureAwait(false);

        var queryResult = await context.Connection.Client.CallToolAsync(
            "codealta.search.query",
            new Dictionary<string, object?>
            {
                ["text"] = "allocations span",
                ["workspaceId"] = "workspace-1",
                ["projectId"] = "project-1",
                ["limit"] = 5,
            }).ConfigureAwait(false);

        var queryPayload = ParseJson(ReadTextContent(queryResult));
        var results = queryPayload.RootElement;
        Assert.AreEqual(JsonValueKind.Array, results.ValueKind);
        Assert.IsTrue(results.GetArrayLength() > 0);
        Assert.AreEqual(artifactUri, results[0].GetProperty("sourceId").GetString());
    }

    private static string ReadTextContent(CallToolResult result)
    {
        var text = result.Content
            .OfType<TextContentBlock>()
            .Select(static x => x.Text)
            .FirstOrDefault(static x => !string.IsNullOrWhiteSpace(x));
        if (string.IsNullOrWhiteSpace(text))
        {
            Assert.Fail("Expected a text content block in tool result.");
        }

        return text!;
    }

    private static JsonDocument ParseJson(string json)
    {
        return JsonDocument.Parse(json);
    }

    private sealed class TestContext : IAsyncDisposable
    {
        private readonly IServiceProvider _services;

        private TestContext(
            TempDirectory temp,
            IServiceProvider services,
            InProcessMcpConnection connection)
        {
            Temp = temp;
            _services = services;
            Connection = connection;
        }

        public TempDirectory Temp { get; }

        public InProcessMcpConnection Connection { get; }

        public static async Task<TestContext> CreateAsync()
        {
            var temp = TempDirectory.Create();
            var stateRoot = Path.Combine(temp.Path, "state", "db");
            var dbPath = Path.Combine(stateRoot, "codealta.db");

            var db = new CodeAltaDb(
                new CodeAltaDbOptions
                {
                    DatabasePath = dbPath,
                });
            await db.InitializeAsync().ConfigureAwait(false);

            var taskRepository = new TaskRepository(db);
            var artifactStore = new ArtifactStore();
            var artifactRepository = new ArtifactRepository(db);
            var agentRepository = new AgentRepository(db);
            var indexingQueue = new IndexingQueue();
            var documentIndexStore = new DocumentIndexStore(db);
            var embeddingManager = new EmbeddingModelManager(new HashEmbedder());
            var indexer = new Indexer(indexingQueue, documentIndexStore, embeddingManager);
            var searchService = new SearchService(documentIndexStore, embeddingManager);
            var workspaceCatalog = new WorkspaceCatalog(
                new WorkspaceCatalogOptions
                {
                    GlobalRepoRoot = temp.Path,
                });
            var workspaceResolver = new WorkspaceResolver(workspaceCatalog);
            var options = new CodeAltaMcpOptions
            {
                ServerName = "CodeAlta.Tests",
                ServerVersion = "1.0.0-test",
                ArtifactRoot = Path.Combine(temp.Path, "artifacts"),
            };

            var services = new ServiceCollection();
            services.AddSingleton(taskRepository);
            services.AddSingleton(artifactStore);
            services.AddSingleton(artifactRepository);
            services.AddSingleton(agentRepository);
            services.AddSingleton(indexer);
            services.AddSingleton(searchService);
            services.AddSingleton(workspaceCatalog);
            services.AddSingleton(workspaceResolver);
            services.AddSingleton(options);
            services.AddSingleton(new McpSessionRegistry());

            var provider = services.BuildServiceProvider();
            var factory = new CodeAltaMcpServerFactory(
                provider,
                provider.GetRequiredService<McpSessionRegistry>(),
                provider.GetRequiredService<CodeAltaMcpOptions>());
            var connection = await InProcessMcpConnection.CreateAsync(factory).ConfigureAwait(false);

            return new TestContext(temp, provider, connection);
        }

        public async ValueTask DisposeAsync()
        {
            await Connection.DisposeAsync().ConfigureAwait(false);

            switch (_services)
            {
                case IAsyncDisposable asyncDisposable:
                    await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                    break;

                case IDisposable disposable:
                    disposable.Dispose();
                    break;
            }

            Temp.Dispose();
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
                $"CodeAlta.Mcp.Tests.{Guid.NewGuid():N}");
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
                // Best-effort cleanup for temporary test files.
            }
        }
    }
}
