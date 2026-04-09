using System.Security;
using System.Text;

namespace CodeAlta.Agent.LocalRuntime.Compaction;

internal static class LocalAgentCompactionSerializer
{
    private const int MaxToolResultCharacters = 2000;

    public static string SerializeForSummary(IReadOnlyList<LocalAgentConversationMessage> messages)
    {
        ArgumentNullException.ThrowIfNull(messages);

        var builder = new StringBuilder();
        foreach (var message in messages)
        {
            foreach (var line in SerializeMessage(message))
            {
                builder.AppendLine(line);
            }
        }

        return builder.ToString().Trim();
    }

    public static string BuildSummaryRequestBody(
        LocalAgentCompactionPreparation preparation,
        string? latestUserRequest,
        IReadOnlyList<string> readFiles,
        IReadOnlyList<string> modifiedFiles)
    {
        ArgumentNullException.ThrowIfNull(preparation);
        ArgumentNullException.ThrowIfNull(readFiles);
        ArgumentNullException.ThrowIfNull(modifiedFiles);

        var builder = new StringBuilder();
        builder.AppendLine("""<codealta-compaction-request version="1">""");
        AppendTag(builder, "mode", preparation.PreviousSummary is null ? "initial" : "update");
        AppendTag(builder, "trigger", preparation.Trigger.ToString().ToLowerInvariant());
        AppendTag(builder, "split-turn", preparation.IsSplitTurn ? "true" : "false");
        AppendTag(builder, "active-user-request", string.IsNullOrWhiteSpace(latestUserRequest) ? "(none recorded)" : latestUserRequest.Trim());

        if (!string.IsNullOrWhiteSpace(preparation.PreviousSummary))
        {
            AppendTag(builder, "previous-summary", preparation.PreviousSummary);
        }

        AppendTag(builder, "conversation", SerializeForSummary(preparation.MessagesToSummarize));

        if (preparation.TurnPrefixMessages.Count > 0)
        {
            AppendTag(builder, "retained-prefix", SerializeForSummary(preparation.TurnPrefixMessages));
        }

        if (preparation.MessagesToKeep.Count > 0)
        {
            AppendTag(builder, "retained-suffix", SerializeForSummary(preparation.MessagesToKeep));
        }

        AppendTag(builder, "relevant-files", RenderFileActivity(readFiles, modifiedFiles));
        builder.Append("""</codealta-compaction-request>""");
        return builder.ToString();
    }

    private static IEnumerable<string> SerializeMessage(LocalAgentConversationMessage message)
    {
        foreach (var part in message.Parts)
        {
            switch (part)
            {
                case LocalAgentMessagePart.Text text:
                    yield return $"[{GetRoleLabel(message.Role)}] {text.Value.Trim()}";
                    break;
                case LocalAgentMessagePart.Reasoning reasoning when !string.IsNullOrWhiteSpace(reasoning.Value):
                    yield return $"[Assistant reasoning] {reasoning.Value!.Trim()}";
                    break;
                case LocalAgentMessagePart.ToolCall toolCall:
                    yield return $"[Assistant tool calls] {toolCall.Name} {toolCall.Arguments.GetRawText()}";
                    break;
                case LocalAgentMessagePart.ToolResult toolResult:
                    yield return $"[Tool result] {Truncate(RenderToolResult(toolResult.Result), MaxToolResultCharacters)}";
                    break;
                case LocalAgentMessagePart.Uri uri:
                    yield return $"[Attachment] {uri.Value}";
                    break;
                case LocalAgentMessagePart.Data data:
                    yield return $"[Attachment] {data.Name ?? data.MediaType}";
                    break;
            }
        }
    }

    private static string GetRoleLabel(LocalAgentConversationRole role)
        => role switch
        {
            LocalAgentConversationRole.User => "User",
            LocalAgentConversationRole.Assistant => "Assistant",
            LocalAgentConversationRole.Tool => "Tool result",
            LocalAgentConversationRole.System => "System",
            _ => "Message",
        };

    private static string RenderToolResult(AgentToolResult result)
    {
        var segments = result.Items.Select(static item => item switch
        {
            AgentToolResultItem.Text text => text.Value,
            AgentToolResultItem.ImageUrl imageUrl => imageUrl.Url,
            _ => string.Empty,
        });
        var rendered = string.Join(Environment.NewLine, segments.Where(static value => !string.IsNullOrWhiteSpace(value)));
        return string.IsNullOrWhiteSpace(rendered) ? (result.Error ?? "(no output)") : rendered;
    }

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength] + "...";

    private static void AppendTag(StringBuilder builder, string tagName, string? value)
    {
        builder.Append('<').Append(tagName).AppendLine(">");
        builder.AppendLine(SecurityElement.Escape(value ?? string.Empty) ?? string.Empty);
        builder.Append("</").Append(tagName).AppendLine(">");
    }

    private static string RenderFileActivity(
        IReadOnlyList<string> readFiles,
        IReadOnlyList<string> modifiedFiles)
    {
        if (readFiles.Count == 0 && modifiedFiles.Count == 0)
        {
            return "- None tracked.";
        }

        var builder = new StringBuilder();
        if (readFiles.Count > 0)
        {
            builder.AppendLine("### Read");
            foreach (var path in readFiles)
            {
                builder.Append("- ").AppendLine(path);
            }
        }

        if (modifiedFiles.Count > 0)
        {
            builder.AppendLine("### Modified");
            foreach (var path in modifiedFiles)
            {
                builder.Append("- ").AppendLine(path);
            }
        }

        return builder.ToString().Trim();
    }
}
