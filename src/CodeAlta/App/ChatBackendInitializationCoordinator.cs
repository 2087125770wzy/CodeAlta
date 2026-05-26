using System.ComponentModel;
using CodeAlta.Agent;
using CodeAlta.App.Events;
using CodeAlta.Models;
using CodeAlta.Orchestration.Runtime;
using CodeAlta.Presentation.Chat;
using XenoAtom.Logging;

namespace CodeAlta.App;

internal sealed class ChatBackendInitializationCoordinator
{
    private static readonly Logger Logger = LogManager.GetLogger("CodeAlta.App.ChatBackendInitialization");
    private readonly AgentHub _agentHub;
    private readonly IReadOnlyList<ModelProviderDescriptor> _backendDescriptors;
    private readonly Dictionary<string, ChatBackendState> _chatBackendStates;
    private readonly Action<Action> _dispatchToUi;
    private readonly FrontendEventPublisher _frontendEvents;
    private readonly Action<string?>? _setProviderInitializationStatus;
    private readonly Action<AgentBackendId, bool>? _setBackendSessionLoadingEnabled;
    private long _providerInitializationStatusVersion;

    public ChatBackendInitializationCoordinator(
        AgentHub agentHub,
        IReadOnlyList<ModelProviderDescriptor> backendDescriptors,
        Dictionary<string, ChatBackendState> chatBackendStates,
        Action<Action> dispatchToUi,
        FrontendEventPublisher frontendEvents,
        Action<string?>? setProviderInitializationStatus = null,
        Action<AgentBackendId, bool>? setBackendSessionLoadingEnabled = null)
    {
        ArgumentNullException.ThrowIfNull(agentHub);
        ArgumentNullException.ThrowIfNull(backendDescriptors);
        ArgumentNullException.ThrowIfNull(chatBackendStates);
        ArgumentNullException.ThrowIfNull(dispatchToUi);
        ArgumentNullException.ThrowIfNull(frontendEvents);

        _agentHub = agentHub;
        _backendDescriptors = backendDescriptors;
        _chatBackendStates = chatBackendStates;
        _dispatchToUi = dispatchToUi;
        _frontendEvents = frontendEvents;
        _setProviderInitializationStatus = setProviderInitializationStatus;
        _setBackendSessionLoadingEnabled = setBackendSessionLoadingEnabled;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        var progress = new ProviderInitializationProgress(_backendDescriptors);
        ReportProviderInitializationProgress(progress.Snapshot(null));
        try
        {
            await Task.WhenAll(_backendDescriptors.Select(descriptor => RefreshAndReportAsync(descriptor, progress, cancellationToken)))
                .ConfigureAwait(false);
        }
        finally
        {
            ReportProviderInitializationProgress(null);
        }
    }

    public Task RefreshBackendAsync(AgentBackendId backendId, CancellationToken cancellationToken = default)
    {
        return RefreshAsync(new ModelProviderId(backendId.Value), cancellationToken);
    }

    public Task RefreshBackendsAsync(IEnumerable<AgentBackendId> backendIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(backendIds);
        return Task.WhenAll(backendIds.Distinct().Select(backendId => RefreshAsync(new ModelProviderId(backendId.Value), cancellationToken)));
    }

    internal static (ChatBackendAvailability Availability, string StatusMessage) ClassifyFailure(
        ChatBackendState state,
        Exception exception)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(exception);

        var root = exception.GetBaseException();
        if (root is FileNotFoundException or DirectoryNotFoundException)
        {
            return (ChatBackendAvailability.Unsupported, ChatBackendPresentation.BuildUnsupportedBackendMessage(state, root.Message));
        }

        if (root is Win32Exception win32Exception && win32Exception.NativeErrorCode == 2)
        {
            return (ChatBackendAvailability.Unsupported, ChatBackendPresentation.BuildUnsupportedBackendMessage(state, root.Message));
        }

        var message = root.Message.Trim();
        if (message.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("No such file", StringComparison.OrdinalIgnoreCase))
        {
            return (ChatBackendAvailability.Unsupported, ChatBackendPresentation.BuildUnsupportedBackendMessage(state, message));
        }

        return (ChatBackendAvailability.Failed, ChatBackendPresentation.BuildFailedBackendMessage(state, message));
    }

    internal static bool CanReuseLoadedBackendState(ModelProviderId providerId, ChatBackendState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return IsProcessBackedProviderBackend(providerId) &&
               state.Availability == ChatBackendAvailability.Ready;
    }

    internal static string? FormatProviderInitializationStatus(
        int completedProviderCount,
        int totalProviderCount,
        IReadOnlyList<string> initializingProviderDisplayNames)
    {
        ArgumentNullException.ThrowIfNull(initializingProviderDisplayNames);

        if (totalProviderCount <= 0 || completedProviderCount >= totalProviderCount)
        {
            return null;
        }

        var completed = Math.Clamp(completedProviderCount, 0, totalProviderCount);
        var initializingNames = initializingProviderDisplayNames
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .Take(2)
            .ToArray();
        var initializingText = initializingNames.Length == 0
            ? "providers"
            : string.Join(", ", initializingNames) + (initializingProviderDisplayNames.Count > initializingNames.Length ? ", …" : string.Empty);

        return $"Initializing {initializingText} {BuildProgressBar(completed, totalProviderCount)} {completed}/{totalProviderCount}";
    }

    private async Task RefreshAndReportAsync(
        ModelProviderDescriptor descriptor,
        ProviderInitializationProgress progress,
        CancellationToken cancellationToken)
    {
        try
        {
            await RefreshAsync(descriptor.ProviderId, descriptor.DisplayName, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            ReportProviderInitializationProgress(progress.Snapshot(descriptor));
        }
    }

    private async Task RefreshAsync(ModelProviderId providerId, CancellationToken cancellationToken)
        => await RefreshAsync(providerId, displayName: null, cancellationToken).ConfigureAwait(false);

    private async Task RefreshAsync(
        ModelProviderId providerId,
        string? displayName,
        CancellationToken cancellationToken)
    {
        var state = await EnsureBackendStateAsync(providerId, displayName, cancellationToken).ConfigureAwait(false);
        var backendId = new AgentBackendId(providerId.Value);
        if (CanReuseLoadedBackendState(providerId, state))
        {
            _setBackendSessionLoadingEnabled?.Invoke(backendId, true);
            LogInfo(
                $"Skipping chat backend refresh for loaded process-backed backend provider={providerId.Value} displayName={state.DisplayName} models={state.Models.Count}");
            return;
        }

        _setBackendSessionLoadingEnabled?.Invoke(backendId, false);
        LogInfo($"Refreshing chat backend provider={providerId.Value} displayName={state.DisplayName}");
        _dispatchToUi(
            () =>
            {
                state.Availability = ChatBackendAvailability.Connecting;
                state.StatusMessage = "Detecting provider...";
                PublishProviderStateChanged(providerId);
            });

        try
        {
            // Backend discovery is explicit background I/O. Any state mutation after this point
            // must go back through the UI dispatcher.
            var models = await _agentHub.ListModelsAsync(backendId, cancellationToken).ConfigureAwait(false);
            _setBackendSessionLoadingEnabled?.Invoke(backendId, true);
            _dispatchToUi(
                () =>
                {
                    state.Models.Clear();
                    state.Models.AddRange(models);
                    state.SelectedModelId = ResolveSelectedModelIdAfterDiscovery(models, state.SelectedModelId);
                    state.SelectedReasoningEffort = ChatBackendPresentation.ResolvePreferredReasoningEffort(
                        ModelProviderPreferenceCoordinator.FindModel(models, state.SelectedModelId),
                        state.SelectedReasoningEffort);
                    state.Availability = ChatBackendAvailability.Ready;
                    state.StatusMessage = ChatBackendPresentation.BuildReadyStatusMessage(state);
                    LogInfo(
                        $"Chat backend ready provider={providerId.Value} displayName={state.DisplayName} models={models.Count} status={state.StatusMessage}");
                    PublishProviderStateChanged(providerId);
                });
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _setBackendSessionLoadingEnabled?.Invoke(backendId, false);
            var (availability, statusMessage) = ClassifyFailure(state, ex);
            LogWarn(
                ex,
                $"Chat backend initialization failed provider={providerId.Value} displayName={state.DisplayName} classifiedAvailability={availability} status={statusMessage}");
            _dispatchToUi(
                () =>
                {
                    state.Models.Clear();
                    state.SelectedModelId = null;
                    state.SelectedReasoningEffort = null;
                    state.DraftScopeKey = null;
                    state.Availability = availability;
                    state.StatusMessage = statusMessage;
                    PublishProviderStateChanged(providerId);
                });
        }
    }

    private static string? ResolveSelectedModelIdAfterDiscovery(
        IReadOnlyList<AgentModelInfo> models,
        string? selectedModelId)
    {
        ArgumentNullException.ThrowIfNull(models);

        return string.IsNullOrWhiteSpace(selectedModelId)
            ? ChatBackendPresentation.ResolvePreferredModelId(models, selectedModelId)
            : selectedModelId.Trim();
    }

    private async Task<ChatBackendState> EnsureBackendStateAsync(
        ModelProviderId providerId,
        string? displayName,
        CancellationToken cancellationToken)
    {
        if (_chatBackendStates.TryGetValue(providerId.Value, out var state))
        {
            return state;
        }

        var completion = new TaskCompletionSource<ChatBackendState>(TaskCreationOptions.RunContinuationsAsynchronously);
        _dispatchToUi(
            () =>
            {
                try
                {
                    if (!_chatBackendStates.TryGetValue(providerId.Value, out var state))
                    {
                        state = new ChatBackendState(
                            providerId,
                            string.IsNullOrWhiteSpace(displayName) ? providerId.Value : displayName.Trim());
                        _chatBackendStates[providerId.Value] = state;
                    }

                    completion.SetResult(state);
                }
                catch (Exception ex)
                {
                    completion.SetException(ex);
                }
            });

        return await completion.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    private void PublishProviderStateChanged(ModelProviderId providerId)
    {
        _frontendEvents.Publish(new ModelProviderStateChangedEvent(providerId.Value));
        _frontendEvents.Publish(new HeaderChangedEvent());
    }

    private static bool IsProcessBackedProviderBackend(ModelProviderId providerId)
        => false;

    private void ReportProviderInitializationProgress(ProviderInitializationProgressSnapshot? progress)
    {
        if (_setProviderInitializationStatus is null)
        {
            return;
        }

        var status = progress is null
            ? null
            : FormatProviderInitializationStatus(
                progress.CompletedProviderCount,
                progress.TotalProviderCount,
                progress.InitializingProviderDisplayNames);
        var version = Interlocked.Increment(ref _providerInitializationStatusVersion);
        _dispatchToUi(
            () =>
            {
                if (version == Volatile.Read(ref _providerInitializationStatusVersion))
                {
                    _setProviderInitializationStatus(status);
                }
            });
    }

    private static string BuildProgressBar(int completed, int total)
    {
        const int width = 8;
        if (total <= 0)
        {
            return "[□□□□□□□□]";
        }

        var filled = (int)Math.Round(Math.Clamp(completed, 0, total) / (double)total * width, MidpointRounding.AwayFromZero);
        filled = Math.Clamp(filled, 0, width);
        return "[" + new string('■', filled) + new string('□', width - filled) + "]";
    }

    private sealed class ProviderInitializationProgress
    {
        private readonly object _gate = new();
        private readonly List<string> _initializingProviderDisplayNames;
        private int _completedProviderCount;

        public ProviderInitializationProgress(IReadOnlyList<ModelProviderDescriptor> descriptors)
        {
            ArgumentNullException.ThrowIfNull(descriptors);

            TotalProviderCount = descriptors.Count;
            _initializingProviderDisplayNames = descriptors.Select(static descriptor => descriptor.DisplayName).ToList();
        }

        public int TotalProviderCount { get; }

        public ProviderInitializationProgressSnapshot Snapshot(ModelProviderDescriptor? completedDescriptor)
        {
            lock (_gate)
            {
                if (completedDescriptor is not null)
                {
                    _completedProviderCount++;
                    _initializingProviderDisplayNames.RemoveAll(
                        name => string.Equals(name, completedDescriptor.DisplayName, StringComparison.Ordinal));
                }

                return new ProviderInitializationProgressSnapshot(
                    _completedProviderCount,
                    TotalProviderCount,
                    _initializingProviderDisplayNames.ToArray());
            }
        }
    }

    private sealed record ProviderInitializationProgressSnapshot(
        int CompletedProviderCount,
        int TotalProviderCount,
        IReadOnlyList<string> InitializingProviderDisplayNames);

    private static void LogInfo(string message)
    {
        Logger.Info(message);
    }

    private static void LogWarn(Exception exception, string message)
    {
        Logger.Warn(exception, message);
    }
}
