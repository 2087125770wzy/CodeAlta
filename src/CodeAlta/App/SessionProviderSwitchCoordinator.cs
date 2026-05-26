using CodeAlta.Agent;
using CodeAlta.App.State;
using CodeAlta.Catalog;
using CodeAlta.Models;
using CodeAlta.Presentation.Chat;

namespace CodeAlta.App;

internal sealed class SessionProviderSwitchCoordinator
{
    private readonly IReadOnlyDictionary<string, ModelProviderState> _chatBackendStates;
    private readonly IReadOnlyDictionary<string, LocalRuntimeBackendInfo> _localRuntimeBackends;
    private readonly Func<OpenThreadState, Task> _applyThreadPreferenceAsync;
    private readonly Func<string, Task<bool>> _detachThreadSessionAsync;
    private readonly Action<SessionViewDescriptor> _updateThreadState;
    private readonly Func<Task> _persistViewStateAsync;

    public SessionProviderSwitchCoordinator(
        CodeAltaConfigStore configStore,
        IReadOnlyDictionary<string, ModelProviderState> chatBackendStates,
        Func<OpenThreadState, Task> applyThreadPreferenceAsync,
        Func<string, Task<bool>> detachThreadSessionAsync,
        Action<SessionViewDescriptor> updateThreadState,
        Func<Task> persistViewStateAsync)
    {
        ArgumentNullException.ThrowIfNull(configStore);
        ArgumentNullException.ThrowIfNull(chatBackendStates);
        ArgumentNullException.ThrowIfNull(applyThreadPreferenceAsync);
        ArgumentNullException.ThrowIfNull(detachThreadSessionAsync);
        ArgumentNullException.ThrowIfNull(updateThreadState);
        ArgumentNullException.ThrowIfNull(persistViewStateAsync);

        _chatBackendStates = chatBackendStates;
        _applyThreadPreferenceAsync = applyThreadPreferenceAsync;
        _detachThreadSessionAsync = detachThreadSessionAsync;
        _updateThreadState = updateThreadState;
        _persistViewStateAsync = persistViewStateAsync;
        _localRuntimeBackends = configStore.LoadGlobalProviderDefinitions(includeDisabled: true)
            .Where(static definition => TryMapProtocolFamily(definition.ProviderType, out _))
            .Select(definition =>
            {
                TryMapProtocolFamily(definition.ProviderType, out var protocolFamily);
                return new KeyValuePair<string, LocalRuntimeBackendInfo>(
                    definition.ProviderKey,
                    new LocalRuntimeBackendInfo(
                        new ModelProviderId(definition.ProviderKey),
                        definition.ProviderKey,
                        protocolFamily!));
            })
            .ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.OrdinalIgnoreCase);
    }

    public bool CanSelectThreadProvider(SessionViewDescriptor thread, OpenThreadState tab)
    {
        ArgumentNullException.ThrowIfNull(thread);
        ArgumentNullException.ThrowIfNull(tab);

        return !tab.StatusBusy &&
               tab.ActiveRunId is null &&
               IsSwitchableSourceProvider(new ModelProviderId(thread.BackendId));
    }

    public bool CanSwitchThreadProvider(
        SessionViewDescriptor thread,
        OpenThreadState tab,
        ModelProviderId targetProviderId)
    {
        ArgumentNullException.ThrowIfNull(thread);
        ArgumentNullException.ThrowIfNull(tab);

        return CanSelectThreadProvider(thread, tab) &&
               !string.Equals(thread.BackendId, targetProviderId.Value, StringComparison.OrdinalIgnoreCase) &&
               TryGetLocalRuntimeBackendInfo(targetProviderId, out _) &&
               _chatBackendStates.TryGetValue(targetProviderId.Value, out var targetState) &&
               targetState.Availability == ModelProviderAvailability.Ready;
    }

    public async Task<bool> SwitchThreadProviderAsync(
        SessionViewDescriptor thread,
        OpenThreadState tab,
        ModelProviderId targetProviderId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(thread);
        ArgumentNullException.ThrowIfNull(tab);
        cancellationToken.ThrowIfCancellationRequested();

        if (!CanSwitchThreadProvider(thread, tab, targetProviderId) ||
            !TryGetLocalRuntimeBackendInfo(targetProviderId, out var targetBackend))
        {
            return false;
        }

        var timestamp = DateTimeOffset.UtcNow;
        var oldThreadId = thread.ThreadId;
        var oldThreadBackendId = thread.BackendId;
        var oldThreadProviderKey = thread.ProviderKey;
        var oldThreadUpdatedAt = thread.UpdatedAt;
        var oldThreadModelId = thread.ModelId;
        var oldThreadReasoningEffort = thread.ReasoningEffort;
        var oldTabBackendId = tab.BackendId;
        var oldTabModelId = tab.ModelId;
        var oldTabReasoningEffort = tab.ReasoningEffort;
        var oldTabUsage = tab.Usage;

        thread.BackendId = targetBackend.ProviderId.Value;
        thread.ProviderKey = targetBackend.ProviderKey;
        thread.UpdatedAt = timestamp;

        tab.BackendId = new AgentBackendId(targetBackend.ProviderId.Value);
        tab.ModelId = null;
        tab.ReasoningEffort = null;
        tab.Usage = null;

        try
        {
            await _applyThreadPreferenceAsync(tab);
            NormalizeTargetModelSelection(tab, targetBackend.ProviderId);
            thread.ModelId = tab.ModelId;
            thread.ReasoningEffort = tab.ReasoningEffort;
            if (!string.IsNullOrWhiteSpace(oldThreadId))
            {
                await _detachThreadSessionAsync(oldThreadId);
            }
        }
        catch
        {
            thread.BackendId = oldThreadBackendId;
            thread.ProviderKey = oldThreadProviderKey;
            thread.UpdatedAt = oldThreadUpdatedAt;
            thread.ModelId = oldThreadModelId;
            thread.ReasoningEffort = oldThreadReasoningEffort;
            tab.BackendId = oldTabBackendId;
            tab.ModelId = oldTabModelId;
            tab.ReasoningEffort = oldTabReasoningEffort;
            tab.Usage = oldTabUsage;
            throw;
        }

        _updateThreadState(thread);
        await _persistViewStateAsync();
        return true;
    }

    private bool TryGetLocalRuntimeBackendInfo(ModelProviderId providerId, out LocalRuntimeBackendInfo backendInfo)
    {
        return _localRuntimeBackends.TryGetValue(providerId.Value, out backendInfo!);
    }

    private bool IsSwitchableSourceProvider(ModelProviderId providerId)
        => TryGetLocalRuntimeBackendInfo(providerId, out _) || IsNativeProvider(providerId);

    private void NormalizeTargetModelSelection(OpenThreadState tab, ModelProviderId targetProviderId)
    {
        if (!_chatBackendStates.TryGetValue(targetProviderId.Value, out var targetState) ||
            targetState.Models.Count == 0 ||
            string.IsNullOrWhiteSpace(tab.ModelId) ||
            targetState.Models.Any(model => string.Equals(model.Id, tab.ModelId, StringComparison.Ordinal)))
        {
            return;
        }

        tab.ModelId = ChatBackendPresentation.ResolvePreferredModelId(targetState.Models, targetState.SelectedModelId);
        var selectedModel = ModelProviderPreferenceCoordinator.FindModel(targetState.Models, tab.ModelId);
        tab.ReasoningEffort = ChatBackendPresentation.ResolvePreferredReasoningEffort(
            selectedModel,
            targetState.SelectedReasoningEffort);
    }

    private static bool IsNativeProvider(ModelProviderId providerId)
        => string.Equals(providerId.Value, ModelProviderIds.Codex.Value, StringComparison.OrdinalIgnoreCase) ||
           string.Equals(providerId.Value, ModelProviderIds.Copilot.Value, StringComparison.OrdinalIgnoreCase);

    private static bool TryMapProtocolFamily(string? providerType, out string? protocolFamily)
    {
        protocolFamily = providerType?.Trim().ToLowerInvariant() switch
        {
            "openai-chat" => "openai-chat",
            "openai-responses" => "openai-responses",
            "azure-openai" => "openai-chat",
            "codex" => "codex",
            "copilot" => "copilot",
            "xai" => "xai",
            "anthropic" => "anthropic-messages",
            "google-genai" => "google-genai",
            "vertex-ai" => "google-genai",
            _ => null,
        };

        return protocolFamily is not null;
    }

    private sealed record LocalRuntimeBackendInfo(
        ModelProviderId ProviderId,
        string ProviderKey,
        string ProtocolFamily);
}
