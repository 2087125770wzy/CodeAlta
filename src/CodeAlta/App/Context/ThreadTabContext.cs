using CodeAlta.Threading;
using CodeAlta.Views;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Controls;

namespace CodeAlta.App.Context;

internal sealed class ThreadTabContext
{
    private readonly Func<TabControl?> _getTabControl;
    private readonly Func<ThreadWorkspaceView?> _getWorkspaceView;
    private readonly Func<Func<Visual>, ComputedVisual> _createComputedVisual;
    private readonly Func<IUiDispatcher> _getUiDispatcher;
    private readonly Action _activateDraftTab;
    private readonly Action<string> _closeThread;
    private readonly Action _closeDraftTab;
    private readonly Action<string> _openThread;

    public ThreadTabContext(
        Func<TabControl?> getTabControl,
        Func<ThreadWorkspaceView?> getWorkspaceView,
        Func<Func<Visual>, ComputedVisual> createComputedVisual,
        Func<IUiDispatcher> getUiDispatcher,
        Action activateDraftTab,
        Action<string> closeThread,
        Action closeDraftTab,
        Action<string> openThread)
    {
        ArgumentNullException.ThrowIfNull(getTabControl);
        ArgumentNullException.ThrowIfNull(getWorkspaceView);
        ArgumentNullException.ThrowIfNull(createComputedVisual);
        ArgumentNullException.ThrowIfNull(getUiDispatcher);
        ArgumentNullException.ThrowIfNull(activateDraftTab);
        ArgumentNullException.ThrowIfNull(closeThread);
        ArgumentNullException.ThrowIfNull(closeDraftTab);
        ArgumentNullException.ThrowIfNull(openThread);

        _getTabControl = getTabControl;
        _getWorkspaceView = getWorkspaceView;
        _createComputedVisual = createComputedVisual;
        _getUiDispatcher = getUiDispatcher;
        _activateDraftTab = activateDraftTab;
        _closeThread = closeThread;
        _closeDraftTab = closeDraftTab;
        _openThread = openThread;
    }

    public TabControl? GetTabControl()
        => _getTabControl();

    public ThreadWorkspaceView? GetWorkspaceView()
        => _getWorkspaceView();

    public ComputedVisual CreateComputedVisual(Func<Visual> build)
    {
        ArgumentNullException.ThrowIfNull(build);
        return _createComputedVisual(build);
    }

    public IUiDispatcher GetUiDispatcher()
        => _getUiDispatcher();

    public void ActivateDraftTab()
        => _activateDraftTab();

    public void CloseThread(string threadId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(threadId);
        _closeThread(threadId);
    }

    public void CloseDraftTab()
        => _closeDraftTab();

    public void OpenThread(string threadId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(threadId);
        _openThread(threadId);
    }
}
