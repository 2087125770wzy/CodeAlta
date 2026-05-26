using CodeAlta.Catalog;
using CodeAlta.Orchestration.Runtime;

namespace CodeAlta.LiveTool;

internal interface IAltaSessionQueryService
{
    IAsyncEnumerable<AltaSessionInfo> LoadAsync(AltaCommandContext context);
}

internal sealed class AltaSessionQueryService : IAltaSessionQueryService
{
    public async IAsyncEnumerable<AltaSessionInfo> LoadAsync(
        AltaCommandContext context,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken = cancellationToken == default ? context.CancellationToken : cancellationToken;

        var runtime = context.Services.Get<SessionRuntimeService>();
        var threadCatalog = context.Services.Get<WorkThreadCatalog>();
        if (runtime is null && threadCatalog is null)
        {
            AltaJsonlWriter.WriteError(
                context.Stderr,
                context.CorrelationId,
                "service.unavailable",
                AltaExitCodes.ServiceUnavailable,
                $"Required in-process service '{nameof(SessionRuntimeService)}' or '{nameof(WorkThreadCatalog)}' is unavailable.");
            yield break;
        }

        WorkThreadViewState? viewState = null;
        if (threadCatalog is not null)
        {
            try
            {
                viewState = await threadCatalog.LoadViewStateAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                AltaJsonlWriter.WriteWarning(context.Stderr, context.CorrelationId, "thread.viewStateUnavailable", ex.Message);
            }
        }

        if (runtime is not null)
        {
            await foreach (var thread in runtime.StreamRecoverableSessionsAsync(cancellationToken: cancellationToken).ConfigureAwait(false))
            {
                yield return await BuildSessionInfoAsync(runtime, threadCatalog, viewState, thread, cancellationToken).ConfigureAwait(false);
            }

            yield break;
        }

        foreach (var thread in await threadCatalog!.LoadInternalAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return await BuildSessionInfoAsync(runtime, threadCatalog, viewState, thread, cancellationToken).ConfigureAwait(false);
        }
    }

    IAsyncEnumerable<AltaSessionInfo> IAltaSessionQueryService.LoadAsync(AltaCommandContext context)
        => LoadAsync(context);

    private static async Task<AltaSessionInfo> BuildSessionInfoAsync(
        SessionRuntimeService? runtime,
        WorkThreadCatalog? threadCatalog,
        WorkThreadViewState? viewState,
        SessionViewDescriptor thread,
        CancellationToken cancellationToken)
    {
        var localState = await TryGetLocalStateAsync(threadCatalog, viewState, thread, cancellationToken).ConfigureAwait(false);
        var preference = TryGetPreference(viewState, thread.ThreadId);
        var isRunning = runtime is not null && await runtime.HasActiveRunAsync(thread, cancellationToken).ConfigureAwait(false);
        var hasActiveSession = isRunning || (runtime is not null && await runtime.HasActiveCoordinatorSessionAsync(thread.ThreadId, cancellationToken).ConfigureAwait(false));
        if (localState?.Archived == true)
        {
            thread.Status = WorkThreadStatus.Archived;
        }

        if (localState?.MessageCount is not null)
        {
            thread.MessageCount = localState.MessageCount;
        }

        if (!string.IsNullOrWhiteSpace(localState?.ParentThreadId))
        {
            thread.ParentThreadId = localState.ParentThreadId;
        }

        if (localState?.CreatedBy is not null)
        {
            thread.CreatedBy = localState.CreatedBy;
        }

        if (!string.IsNullOrWhiteSpace(localState?.ProviderKey))
        {
            thread.ProviderKey = localState.ProviderKey;
        }

        if (!string.IsNullOrWhiteSpace(localState?.ModelId))
        {
            thread.ModelId = localState.ModelId;
        }

        if (localState?.ReasoningEffort is not null)
        {
            thread.ReasoningEffort = localState.ReasoningEffort;
        }

        return new AltaSessionInfo(thread, localState, preference, isRunning, hasActiveSession, ResolveState(thread, isRunning, hasActiveSession));
    }

    private static async Task<WorkThreadLocalState?> TryGetLocalStateAsync(
        WorkThreadCatalog? threadCatalog,
        WorkThreadViewState? viewState,
        SessionViewDescriptor thread,
        CancellationToken cancellationToken)
    {
        if (threadCatalog is not null)
        {
            try
            {
                var journalState = await threadCatalog.JournalStore
                    .ReadLatestStateAsync(thread.ThreadId, thread.CreatedAt, cancellationToken)
                    .ConfigureAwait(false);
                if (journalState is not null)
                {
                    return journalState;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or System.Text.Json.JsonException)
            {
            }
        }

        return viewState is not null && viewState.ThreadStates.TryGetValue(thread.ThreadId, out var state) ? state : null;
    }

    private static WorkThreadPreference? TryGetPreference(WorkThreadViewState? viewState, string threadId)
        => viewState is not null && viewState.ThreadPreferences.TryGetValue(threadId, out var preference) ? preference : null;

    private static string ResolveState(SessionViewDescriptor thread, bool isRunning, bool hasActiveSession)
    {
        if (thread.Status == WorkThreadStatus.Archived)
        {
            return "archived";
        }

        if (isRunning)
        {
            return "running";
        }

        return hasActiveSession ? "idle" : "inactive";
    }
}

internal sealed record AltaSessionInfo(
    SessionViewDescriptor Thread,
    WorkThreadLocalState? LocalState,
    WorkThreadPreference? Preference,
    bool IsRunning,
    bool HasActiveSession,
    string State);
