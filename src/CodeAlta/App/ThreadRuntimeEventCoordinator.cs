using CodeAlta.App.State;
using CodeAlta.App.Events;
using CodeAlta.Agent;
using CodeAlta.Catalog;
using CodeAlta.Models;
using CodeAlta.Orchestration.Runtime;
using CodeAlta.Presentation.Shell;
using CodeAlta.Views;
using XenoAtom.Logging;

namespace CodeAlta.App;

internal sealed class ThreadRuntimeEventCoordinator
{
    private readonly ShellStateStore _stateStore;
    private readonly Func<string, OpenThreadState?> _findOpenThread;
    private readonly Func<string, bool> _isSelectedThread;
    private readonly IShellStatusPort _statusPort;
    private readonly Func<OpenThreadState, CancellationToken, Task> _drainQueuedPromptAsync;
    private readonly IProjectFileSearchService _projectFileSearchService;
    private readonly PluginHostBridge? _pluginHostBridge;
    private readonly FrontendEventPublisher? _frontendEvents;
    private readonly ThreadRuntimeStateReducer _stateReducer;
    private readonly ThreadRuntimeTimelineRenderer _timelineRenderer;

    public ThreadRuntimeEventCoordinator(
        ShellStateStore stateStore,
        Func<string, OpenThreadState?> findOpenThread,
        Func<bool> getAutoApproveEnabled,
        Func<string, bool> isSelectedThread,
        IShellStatusPort statusPort,
        Func<OpenThreadState, CancellationToken, Task> drainQueuedPromptAsync,
        IProjectFileSearchService projectFileSearchService,
        PluginHostBridge? pluginHostBridge = null,
        FrontendEventPublisher? frontendEvents = null)
    {
        ArgumentNullException.ThrowIfNull(stateStore);
        ArgumentNullException.ThrowIfNull(findOpenThread);
        ArgumentNullException.ThrowIfNull(getAutoApproveEnabled);
        ArgumentNullException.ThrowIfNull(isSelectedThread);
        ArgumentNullException.ThrowIfNull(statusPort);
        ArgumentNullException.ThrowIfNull(drainQueuedPromptAsync);
        ArgumentNullException.ThrowIfNull(projectFileSearchService);

        _stateStore = stateStore;
        _findOpenThread = findOpenThread;
        _isSelectedThread = isSelectedThread;
        _statusPort = statusPort;
        _drainQueuedPromptAsync = drainQueuedPromptAsync;
        _projectFileSearchService = projectFileSearchService;
        _pluginHostBridge = pluginHostBridge;
        _frontendEvents = frontendEvents;
        _stateReducer = new ThreadRuntimeStateReducer();
        _timelineRenderer = new ThreadRuntimeTimelineRenderer(getAutoApproveEnabled);
    }

    public void ApplyRuntimeEvent(WorkThreadRuntimeEvent runtimeEvent)
    {
        ArgumentNullException.ThrowIfNull(runtimeEvent);

        var thread = FindThread(runtimeEvent.ThreadId);
        if (thread is null)
        {
            return;
        }

        var tab = _findOpenThread(thread.ThreadId);
        var reduction = _stateReducer.ReduceRuntimeEvent(
            thread,
            tab,
            runtimeEvent,
            _isSelectedThread(thread.ThreadId));

        if (tab is not null)
        {
            switch (runtimeEvent)
            {
                case WorkThreadAgentEvent agentEvent:
                    tab.HistoryEvents?.Add(agentEvent.Event);
                    TryRenderInteraction(tab, () => _timelineRenderer.RenderAgentEvent(tab, agentEvent.Event), "agent event");
                    _frontendEvents?.Publish(new RuntimeTimelineChangedEvent(thread.ThreadId));
                    break;

                case WorkThreadHostEvent hostEvent:
                    TryRenderInteraction(tab, () => _timelineRenderer.RenderHostEvent(tab, hostEvent), "host event");
                    _frontendEvents?.Publish(new RuntimeTimelineChangedEvent(thread.ThreadId));
                    break;
            }
        }

        if (runtimeEvent is WorkThreadAgentEvent agentRuntimeEvent)
        {
            InvalidateProjectFileSearchIfNeeded(thread, agentRuntimeEvent.Event);
            _ = ObservePluginAgentEventAsync(thread, agentRuntimeEvent.Event);
        }

        ApplyReduction(tab, reduction);
    }

    public void HandleAgentEvent(WorkThreadDescriptor thread, OpenThreadState tab, AgentEvent @event)
    {
        ArgumentNullException.ThrowIfNull(thread);
        ArgumentNullException.ThrowIfNull(tab);
        ArgumentNullException.ThrowIfNull(@event);

        var reduction = _stateReducer.ReduceAgentEvent(thread, tab, @event, _isSelectedThread(thread.ThreadId));
        TryRenderInteraction(tab, () => _timelineRenderer.RenderAgentEvent(tab, @event), "agent event");
        _frontendEvents?.Publish(new RuntimeTimelineChangedEvent(thread.ThreadId));
        InvalidateProjectFileSearchIfNeeded(thread, @event);
        _ = ObservePluginAgentEventAsync(thread, @event);
        ApplyReduction(tab, reduction);
    }

    public void TryRenderInteraction(OpenThreadState tab, Action action, string context)
    {
        ArgumentNullException.ThrowIfNull(tab);
        ArgumentNullException.ThrowIfNull(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(context);

        try
        {
            action();
        }
        catch (Exception ex)
        {
            if (LogManager.IsInitialized && CodeAltaApp.UiLogger.IsEnabled(LogLevel.Error))
            {
                CodeAltaApp.UiLogger.Error(ex, $"Failed to render thread {context}");
            }

            _statusPort.SetShellStatus(new ShellStatusUpdate($"Failed to render thread {context}: {ex.Message}", false, StatusTone.Error));
            tab.Timeline.ClearPendingAssistant();
        }
    }

    private async Task ObservePluginAgentEventAsync(WorkThreadDescriptor thread, AgentEvent @event)
    {
        if (_pluginHostBridge is null)
        {
            return;
        }

        try
        {
            await _pluginHostBridge.ObserveAgentEventAsync(thread, @event);
        }
        catch (Exception ex)
        {
            if (LogManager.IsInitialized && CodeAltaApp.UiLogger.IsEnabled(LogLevel.Error))
            {
                CodeAltaApp.UiLogger.Error(ex, $"Plugin agent event observer failed for thread {thread.ThreadId}");
            }
        }
    }

    public static bool ShouldPromoteAgentEventToThinking(AgentEvent @event)
        => ThreadRuntimeStateReducer.ShouldPromoteAgentEventToThinking(@event);

    public static bool ShouldRefreshShellChromeAfterRuntimeEvent(WorkThreadRuntimeEvent runtimeEvent)
        => ThreadRuntimeStateReducer.ShouldRefreshShellChromeAfterRuntimeEvent(runtimeEvent);

    public static string SummarizeContent(string content)
        => ThreadRuntimeStateReducer.SummarizeContent(content);

    private WorkThreadDescriptor? FindThread(string threadId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(threadId);
        return _stateStore.Snapshot.Threads.FirstOrDefault(thread => string.Equals(thread.ThreadId, threadId, StringComparison.OrdinalIgnoreCase));
    }

    private void InvalidateProjectFileSearchIfNeeded(WorkThreadDescriptor thread, AgentEvent @event)
    {
        ArgumentNullException.ThrowIfNull(thread);
        ArgumentNullException.ThrowIfNull(@event);

        if (!ShouldInvalidateProjectFileSearch(@event) ||
            string.IsNullOrWhiteSpace(thread.WorkingDirectory))
        {
            return;
        }

        _ = InvalidateProjectFileSearchAsync(thread.WorkingDirectory);
    }

    private async Task InvalidateProjectFileSearchAsync(string projectRoot)
    {
        try
        {
            await _projectFileSearchService.InvalidateAsync(projectRoot, ProjectFileInvalidationReason.FileSystemWrite);
        }
        catch
        {
        }
    }

    private static bool ShouldInvalidateProjectFileSearch(AgentEvent @event)
        => @event switch
        {
            AgentActivityEvent { Kind: AgentActivityKind.FileChange } => true,
            AgentSessionUpdateEvent { Kind: AgentSessionUpdateKind.DiffUpdated } => true,
            _ => false,
        };

    private void ApplyReduction(OpenThreadState? tab, ThreadRuntimeReductionResult reduction)
    {
        if (tab is not null)
        {
            if (reduction.ClearThreadStatus)
            {
                _statusPort.ClearThreadStatus(tab);
                _frontendEvents?.Publish(new ThreadStatusChangedEvent(tab.Thread.ThreadId));
            }

            if (reduction.ThreadStatus is { } status)
            {
                _statusPort.SetThreadStatus(tab, new ThreadStatusUpdate(status.Message, status.ShowSpinner, status.Tone));
                _frontendEvents?.Publish(new ThreadStatusChangedEvent(tab.Thread.ThreadId));
            }

            if (reduction.DrainQueuedPrompt)
            {
                global::CodeAlta.CodeAltaTaskMonitor.Observe(
                    _drainQueuedPromptAsync(tab, CancellationToken.None),
                    $"Queued prompt drain for thread {tab.Thread.ThreadId}");
            }
        }

        if (reduction.RefreshPromptStrip && tab is not null)
        {
            _frontendEvents?.Publish(new QueuedPromptListChangedEvent(tab.Thread.ThreadId));
        }

        if (reduction.InvalidateSelectedSessionUsage && tab is not null)
        {
            _frontendEvents?.Publish(new SessionUsageChangedEvent(tab.Thread.ThreadId));
        }

        if (reduction.RefreshShellChrome)
        {
            _frontendEvents?.Publish(new ShellChromeChangedEvent());
        }
    }
}
