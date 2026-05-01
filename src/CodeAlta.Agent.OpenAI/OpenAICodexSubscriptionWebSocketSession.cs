#pragma warning disable OPENAI001

using System.Buffers;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text.Json;
using CodeAlta.Agent.OpenAI.CodexSubscription;
using OpenAI.Responses;

namespace CodeAlta.Agent.OpenAI;

internal sealed class OpenAICodexSubscriptionWebSocketSession : IOpenAIResponsesWebSocketSession
{
    private const int ReceiveBufferSize = 1024 * 16;
    private const string ResponsesWebSocketsBetaHeader = "responses_websockets=2026-02-06";

    private readonly Uri _baseUri;
    private readonly OpenAICodexSubscriptionOptions _options;
    private readonly OpenAICodexSubscriptionAuthManager _authManager;
    private readonly string _sessionId;
    private readonly string _userAgentApplicationId;
    private readonly SemaphoreSlim _streamSemaphore = new(initialCount: 1, maxCount: 1);

    private ClientWebSocket? _webSocket;
    private bool _disposed;

    public OpenAICodexSubscriptionWebSocketSession(
        Uri? baseUri,
        OpenAICodexSubscriptionOptions options,
        OpenAICodexSubscriptionAuthManager authManager,
        string sessionId,
        string userAgentApplicationId)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(authManager);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(userAgentApplicationId);

        _baseUri = baseUri ?? new Uri("https://chatgpt.com/backend-api/codex");
        _options = options;
        _authManager = authManager;
        _sessionId = sessionId.Trim();
        _userAgentApplicationId = userAgentApplicationId.Trim();
    }

    public AsyncCollectionResult<StreamingResponseUpdate> CreateResponseStreamingAsync(
        CreateResponseOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.StreamingEnabled != true)
        {
            throw new InvalidOperationException(
                $"{nameof(CreateResponseOptions.StreamingEnabled)} must be set to true for Codex subscription WebSocket streaming.");
        }

        return new OpenAIResponsesWebSocketUpdateCollection(
            CreateResponseStreamingCoreAsync(options, cancellationToken));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _webSocket?.Dispose();
        _webSocket = null;
        _streamSemaphore.Dispose();
    }

    internal static Uri ResolveWebSocketUri(Uri baseUri)
    {
        ArgumentNullException.ThrowIfNull(baseUri);
        var responsesUri = ResolveResponsesUri(baseUri);
        var builder = new UriBuilder(responsesUri)
        {
            Scheme = responsesUri.Scheme.ToLowerInvariant() switch
            {
                "http" => "ws",
                "https" => "wss",
                _ => responsesUri.Scheme,
            },
            Query = string.Empty,
        };
        return builder.Uri;
    }

    private async IAsyncEnumerable<StreamingResponseUpdate> CreateResponseStreamingCoreAsync(
        CreateResponseOptions options,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await _streamSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        var sawTerminalEvent = false;

        try
        {
            await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);
            await SendRequestAsync(CreateWebSocketRequest(options), cancellationToken).ConfigureAwait(false);

            await foreach (var message in ReceiveMessagesAsync(_webSocket!, cancellationToken).ConfigureAwait(false))
            {
                var normalizedMessage = NormalizeWebSocketMessage(message, out var eventType);
                var update = ModelReaderWriter.Read<StreamingResponseUpdate>(
                    normalizedMessage,
                    new ModelReaderWriterOptions("J"),
                    OpenAIResponsesContext.Default)
                    ?? throw new InvalidOperationException("Codex subscription WebSocket returned an unsupported response update.");

                yield return update;

                if (IsTerminalEvent(eventType))
                {
                    sawTerminalEvent = true;
                    yield break;
                }
            }

            if (!sawTerminalEvent)
            {
                throw new InvalidOperationException("Codex subscription WebSocket stream closed before a terminal response event was received.");
            }
        }
        finally
        {
            if (!sawTerminalEvent)
            {
                await CloseWebSocketSilentlyAsync().ConfigureAwait(false);
            }

            _streamSemaphore.Release();
        }
    }

    private async Task EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_webSocket?.State == WebSocketState.Open)
        {
            return;
        }

        _webSocket?.Dispose();
        _webSocket = await CreateAndConnectWebSocketAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<ClientWebSocket> CreateAndConnectWebSocketAsync(CancellationToken cancellationToken)
    {
        var credential = await _authManager.GetCredentialAsync(cancellationToken).ConfigureAwait(false);
        var accountContext = await _authManager.GetAccountContextAsync(cancellationToken).ConfigureAwait(false);
        var webSocket = new ClientWebSocket();
        ApplyHeaders(webSocket.Options, credential.AccessToken, accountContext);

        try
        {
            await webSocket.ConnectAsync(ResolveWebSocketUri(_baseUri), cancellationToken).ConfigureAwait(false);
            return webSocket;
        }
        catch
        {
            webSocket.Dispose();
            throw;
        }
    }

    private void ApplyHeaders(
        ClientWebSocketOptions options,
        string accessToken,
        OpenAICodexSubscriptionAccountContext accountContext)
    {
        options.SetRequestHeader("Authorization", $"Bearer {accessToken}");
        options.SetRequestHeader("OpenAI-Beta", ResponsesWebSocketsBetaHeader);
        options.SetRequestHeader("originator", "codealta");
        options.SetRequestHeader("session_id", _sessionId);
        options.SetRequestHeader("x-client-request-id", _sessionId);
        options.SetRequestHeader("User-Agent", _userAgentApplicationId);

        var accountId = !string.IsNullOrWhiteSpace(_options.AccountId)
            ? _options.AccountId
            : accountContext.AccountId;
        if (!string.IsNullOrWhiteSpace(accountId))
        {
            options.SetRequestHeader("ChatGPT-Account-Id", accountId);
        }

        if (accountContext.IsFedRamp)
        {
            options.SetRequestHeader("X-OpenAI-Fedramp", "true");
        }
    }

    private async Task SendRequestAsync(BinaryData request, CancellationToken cancellationToken)
    {
        var bytes = request.ToArray();
        await _webSocket!.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                endOfMessage: true,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async IAsyncEnumerable<BinaryData> ReceiveMessagesAsync(
        ClientWebSocket webSocket,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(ReceiveBufferSize);
        try
        {
            using var stream = new MemoryStream();
            while (true)
            {
                stream.SetLength(0);
                WebSocketReceiveResult result;
                do
                {
                    result = await webSocket.ReceiveAsync(
                            new ArraySegment<byte>(buffer),
                            cancellationToken)
                        .ConfigureAwait(false);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        yield break;
                    }

                    if (result.MessageType != WebSocketMessageType.Text)
                    {
                        throw new InvalidOperationException("Codex subscription WebSocket returned a non-text frame.");
                    }

                    stream.Write(buffer, 0, result.Count);
                }
                while (!result.EndOfMessage);

                yield return BinaryData.FromBytes(stream.ToArray());
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private async Task CloseWebSocketSilentlyAsync()
    {
        var webSocket = _webSocket;
        _webSocket = null;
        if (webSocket is null)
        {
            return;
        }

        try
        {
            if (webSocket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                await webSocket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "done",
                        cancellationTokenSource.Token)
                    .ConfigureAwait(false);
            }
        }
        catch
        {
            // Best-effort shutdown only.
        }
        finally
        {
            webSocket.Dispose();
        }
    }

    private static BinaryData CreateWebSocketRequest(CreateResponseOptions options)
    {
        using var optionsDocument = JsonDocument.Parse(SerializeModel(options));
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("type"u8, "response.create");
            foreach (var property in optionsDocument.RootElement.EnumerateObject())
            {
                property.WriteTo(writer);
            }

            writer.WriteEndObject();
        }

        return BinaryData.FromBytes(stream.ToArray());
    }

    private static BinaryData NormalizeWebSocketMessage(BinaryData message, out string? eventType)
    {
        using var document = JsonDocument.Parse(message);
        eventType = document.RootElement.TryGetProperty("type"u8, out var typeElement)
            ? typeElement.GetString()
            : null;
        if (!string.Equals(eventType, "response.done", StringComparison.Ordinal))
        {
            return message;
        }

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (property.NameEquals("type"u8))
                {
                    writer.WriteString("type"u8, "response.completed");
                }
                else
                {
                    property.WriteTo(writer);
                }
            }

            writer.WriteEndObject();
        }

        return BinaryData.FromBytes(stream.ToArray());
    }

    private static Uri ResolveResponsesUri(Uri baseUri)
    {
        var builder = new UriBuilder(baseUri);
        var path = builder.Path.TrimEnd('/');
        builder.Path = path.EndsWith("/responses", StringComparison.OrdinalIgnoreCase)
            ? path
            : path + "/responses";
        return builder.Uri;
    }

    private static bool IsTerminalEvent(string? eventType)
        => eventType is "response.completed" or "response.incomplete" or "response.failed" or "response.done";

    private static string SerializeModel<T>(T model)
        where T : notnull
        => model is IPersistableModel<T> persistable
            ? persistable.Write(new ModelReaderWriterOptions("J")).ToString()
            : model.ToString() ?? string.Empty;

    private sealed class OpenAIResponsesWebSocketUpdateCollection(
        IAsyncEnumerable<StreamingResponseUpdate> updates) : AsyncCollectionResult<StreamingResponseUpdate>
    {
        public override async IAsyncEnumerable<ClientResult> GetRawPagesAsync()
        {
            yield return ClientResult.FromResponse(new OpenAIResponsesWebSocketPipelineResponse());
            await Task.CompletedTask.ConfigureAwait(false);
        }

        protected override async IAsyncEnumerable<StreamingResponseUpdate> GetValuesFromPageAsync(ClientResult page)
        {
            await foreach (var update in updates.ConfigureAwait(false))
            {
                yield return update;
            }
        }

        public override ContinuationToken GetContinuationToken(ClientResult page) => default!;
    }

    private sealed class OpenAIResponsesWebSocketPipelineResponse : PipelineResponse
    {
        private readonly PipelineResponseHeaders _headers = new EmptyPipelineResponseHeaders();
        private readonly BinaryData _content = BinaryData.FromString("{}");

        public override int Status => 200;

        public override string ReasonPhrase => "OK";

        protected override PipelineResponseHeaders HeadersCore => _headers;

        public override Stream? ContentStream { get; set; }

        public override BinaryData Content => _content;

        public override BinaryData BufferContent(CancellationToken cancellationToken = default) => _content;

        public override ValueTask<BinaryData> BufferContentAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult(_content);

        public override void Dispose()
        {
        }
    }

    private sealed class EmptyPipelineResponseHeaders : PipelineResponseHeaders
    {
        public override IEnumerator<KeyValuePair<string, string>> GetEnumerator()
        {
            yield break;
        }

        public override bool TryGetValue(string name, out string? value)
        {
            value = null;
            return false;
        }

        public override bool TryGetValues(string name, out IEnumerable<string>? values)
        {
            values = null;
            return false;
        }
    }
}
