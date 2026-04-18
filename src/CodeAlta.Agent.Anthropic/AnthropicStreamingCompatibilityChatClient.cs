using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace CodeAlta.Agent.Anthropic;

internal sealed class AnthropicStreamingCompatibilityChatClient(IChatClient inner) : IChatClient
{
    public void Dispose() => inner.Dispose();

    public object? GetService(Type serviceType, object? serviceKey = null)
        => inner.GetService(serviceType, serviceKey);

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
        => inner.GetResponseAsync(messages, options, cancellationToken);

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = await inner.GetResponseAsync(messages, options, cancellationToken).ConfigureAwait(false);
        var assistantMessages = response.Messages
            .Where(static message => message.Role == ChatRole.Assistant)
            .ToArray();

        foreach (var message in assistantMessages)
        {
            foreach (var content in message.Contents)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return CreateUpdate(message, response, [content]);
            }
        }

        if (response.Usage is not null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return CreateUpdate(
                assistantMessages.LastOrDefault(),
                response,
                [new UsageContent(response.Usage)]);
        }
    }

    private static ChatResponseUpdate CreateUpdate(
        ChatMessage? message,
        ChatResponse response,
        IEnumerable<AIContent> contents)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(contents);

        var update = new ChatResponseUpdate(message?.Role ?? ChatRole.Assistant, [.. contents])
        {
            MessageId = message?.MessageId,
            ResponseId = response.ResponseId,
            ConversationId = response.ConversationId,
            ModelId = response.ModelId,
            CreatedAt = response.CreatedAt,
            FinishReason = response.FinishReason,
            RawRepresentation = response.RawRepresentation,
        };

        return update;
    }
}
