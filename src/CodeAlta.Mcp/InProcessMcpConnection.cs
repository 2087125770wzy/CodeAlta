using System.IO.Pipelines;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace CodeAlta.Mcp;

/// <summary>
/// Represents an in-memory client/server MCP connection for internal use and tests.
/// </summary>
public sealed class InProcessMcpConnection : IAsyncDisposable
{
    private readonly CancellationTokenSource _shutdown;
    private readonly CodeAltaMcpServerSession _serverSession;
    private readonly Task _serverRunTask;
    private bool _disposed;

    private InProcessMcpConnection(
        McpClient client,
        CodeAltaMcpServerSession serverSession,
        Task serverRunTask,
        CancellationTokenSource shutdown)
    {
        Client = client;
        _serverSession = serverSession;
        _serverRunTask = serverRunTask;
        _shutdown = shutdown;
    }

    /// <summary>
    /// Gets the connected MCP client.
    /// </summary>
    public McpClient Client { get; }

    /// <summary>
    /// Creates an in-process connection backed by two in-memory pipes.
    /// </summary>
    /// <param name="factory">Factory used to create the server endpoint.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An active in-process connection.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="factory"/> is <see langword="null"/>.</exception>
    public static async Task<InProcessMcpConnection> CreateAsync(
        CodeAltaMcpServerFactory factory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(factory);

        var clientToServer = new Pipe();
        var serverToClient = new Pipe();

        var serverInput = clientToServer.Reader.AsStream();
        var serverOutput = serverToClient.Writer.AsStream();
        var clientInput = serverToClient.Reader.AsStream();
        var clientOutput = clientToServer.Writer.AsStream();

        var serverSession = factory.Create(serverInput, serverOutput);
        var shutdown = new CancellationTokenSource();
        var serverRunTask = serverSession.Server.RunAsync(shutdown.Token);

        var transport = new StreamClientTransport(clientOutput, clientInput);
        var client = await McpClient.CreateAsync(
            transport,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        var connection = new InProcessMcpConnection(client, serverSession, serverRunTask, shutdown);

        return connection;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _shutdown.Cancel();

        await Client.DisposeAsync().ConfigureAwait(false);

        try
        {
            await _serverRunTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected when stopping the in-memory session.
        }

        await _serverSession.DisposeAsync().ConfigureAwait(false);
        _shutdown.Dispose();
    }
}
