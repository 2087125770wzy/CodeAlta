using CodeAlta.App.State;
using CodeAlta.Catalog;
using CodeAlta.Models;

namespace CodeAlta.App.Context;

internal sealed class ThreadCommandContext
{
    private readonly Func<bool> _trySetPromptUnavailableStatus;
    private readonly Func<Task<WorkThreadDescriptor?>> _createGlobalThreadAsync;
    private readonly Func<Task<WorkThreadDescriptor?>> _createProjectThreadAsync;
    private readonly Func<Task> _persistViewStateAsync;
    private readonly Func<bool> _getAutoApproveEnabled;
    private readonly Action _setReadyStatusForCurrentSelection;
    private readonly Action _clearThreadInput;
    private readonly Action _refreshHeaderAndThreadWorkspace;
    private readonly Action _refreshCatalogAndThreadWorkspace;
    private readonly Action<string, bool, StatusTone> _setShellStatus;
    private readonly Action<OpenThreadState, string, bool, StatusTone> _setThreadStatus;
    private readonly Action<OpenThreadState, Action, string> _tryRenderInteraction;

    public ThreadCommandContext(
        Func<bool> trySetPromptUnavailableStatus,
        Func<Task<WorkThreadDescriptor?>> createGlobalThreadAsync,
        Func<Task<WorkThreadDescriptor?>> createProjectThreadAsync,
        Func<Task> persistViewStateAsync,
        Func<bool> getAutoApproveEnabled,
        Action setReadyStatusForCurrentSelection,
        Action clearThreadInput,
        Action refreshHeaderAndThreadWorkspace,
        Action refreshCatalogAndThreadWorkspace,
        Action<string, bool, StatusTone> setShellStatus,
        Action<OpenThreadState, string, bool, StatusTone> setThreadStatus,
        Action<OpenThreadState, Action, string> tryRenderInteraction)
    {
        ArgumentNullException.ThrowIfNull(trySetPromptUnavailableStatus);
        ArgumentNullException.ThrowIfNull(createGlobalThreadAsync);
        ArgumentNullException.ThrowIfNull(createProjectThreadAsync);
        ArgumentNullException.ThrowIfNull(persistViewStateAsync);
        ArgumentNullException.ThrowIfNull(getAutoApproveEnabled);
        ArgumentNullException.ThrowIfNull(setReadyStatusForCurrentSelection);
        ArgumentNullException.ThrowIfNull(clearThreadInput);
        ArgumentNullException.ThrowIfNull(refreshHeaderAndThreadWorkspace);
        ArgumentNullException.ThrowIfNull(refreshCatalogAndThreadWorkspace);
        ArgumentNullException.ThrowIfNull(setShellStatus);
        ArgumentNullException.ThrowIfNull(setThreadStatus);
        ArgumentNullException.ThrowIfNull(tryRenderInteraction);

        _trySetPromptUnavailableStatus = trySetPromptUnavailableStatus;
        _createGlobalThreadAsync = createGlobalThreadAsync;
        _createProjectThreadAsync = createProjectThreadAsync;
        _persistViewStateAsync = persistViewStateAsync;
        _getAutoApproveEnabled = getAutoApproveEnabled;
        _setReadyStatusForCurrentSelection = setReadyStatusForCurrentSelection;
        _clearThreadInput = clearThreadInput;
        _refreshHeaderAndThreadWorkspace = refreshHeaderAndThreadWorkspace;
        _refreshCatalogAndThreadWorkspace = refreshCatalogAndThreadWorkspace;
        _setShellStatus = setShellStatus;
        _setThreadStatus = setThreadStatus;
        _tryRenderInteraction = tryRenderInteraction;
    }

    public bool TrySetPromptUnavailableStatus()
        => _trySetPromptUnavailableStatus();

    public Task<WorkThreadDescriptor?> CreateGlobalThreadAsync()
        => _createGlobalThreadAsync();

    public Task<WorkThreadDescriptor?> CreateProjectThreadAsync()
        => _createProjectThreadAsync();

    public Task PersistViewStateAsync()
        => _persistViewStateAsync();

    public bool GetAutoApproveEnabled()
        => _getAutoApproveEnabled();

    public void SetReadyStatusForCurrentSelection()
        => _setReadyStatusForCurrentSelection();

    public void ClearThreadInput()
        => _clearThreadInput();

    public void RefreshHeaderAndThreadWorkspace()
        => _refreshHeaderAndThreadWorkspace();

    public void RefreshCatalogAndThreadWorkspace()
        => _refreshCatalogAndThreadWorkspace();

    public void SetShellStatus(string message, bool showSpinner, StatusTone tone)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        _setShellStatus(message, showSpinner, tone);
    }

    public void SetThreadStatus(OpenThreadState tab, string message, bool showSpinner, StatusTone tone)
    {
        ArgumentNullException.ThrowIfNull(tab);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        _setThreadStatus(tab, message, showSpinner, tone);
    }

    public void TryRenderInteraction(OpenThreadState tab, Action action, string context)
    {
        ArgumentNullException.ThrowIfNull(tab);
        ArgumentNullException.ThrowIfNull(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(context);
        _tryRenderInteraction(tab, action, context);
    }
}
