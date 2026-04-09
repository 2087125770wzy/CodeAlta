namespace CodeAlta.Agent.LocalRuntime.Compaction;

internal enum LocalAgentCompactionTrigger
{
    Manual,
    Threshold,
    Overflow,
}

internal sealed record LocalAgentTokenBudget(
    long? ContextWindow,
    long? InputTokenLimit,
    long? OutputTokenLimit,
    long? UsablePromptBudget,
    int ReservedOutputTokens,
    int ReservedOverheadTokens);

internal sealed record LocalAgentTokenEstimate(
    long Tokens,
    string Source,
    bool IsEstimated);

internal sealed record LocalAgentCompactionPreparation(
    LocalAgentCompactionTrigger Trigger,
    IReadOnlyList<LocalAgentConversationMessage> MessagesToSummarize,
    IReadOnlyList<LocalAgentConversationMessage> TurnPrefixMessages,
    IReadOnlyList<LocalAgentConversationMessage> MessagesToKeep,
    string? AnchorContentId,
    bool IsSplitTurn,
    LocalAgentTokenEstimate TokensBefore,
    string? PreviousSummary);

internal sealed record LocalAgentCompactionResult(
    string Summary,
    string? AnchorContentId,
    bool IsSplitTurn,
    long TokensBefore,
    long? TokensAfter,
    int MessagesSummarized,
    IReadOnlyList<string> ReadFiles,
    IReadOnlyList<string> ModifiedFiles);

internal sealed record LocalAgentCompactionSummaryRequest(
    AgentBackendId BackendId,
    LocalAgentProviderDescriptor Provider,
    string SessionId,
    string? ModelId,
    AgentModelInfo? ModelInfo,
    string? WorkingDirectory,
    LocalAgentSessionState State,
    string SystemMessage,
    string UserMessage,
    int MaxOutputTokens);

internal sealed record LocalAgentCompactionSummaryResponse(
    string Summary,
    AgentSessionUsage? Usage);

internal interface ILocalAgentCompactionSummaryExecutor
{
    Task<LocalAgentCompactionSummaryResponse> ExecuteAsync(
        LocalAgentCompactionSummaryRequest request,
        CancellationToken cancellationToken = default);
}
