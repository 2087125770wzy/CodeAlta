using CodeAlta.App;
using CodeAlta.App.State;
using CodeAlta.Catalog;
using CodeAlta.Models;
using CodeAlta.Presentation.Sidebar;
using CodeAlta.Presentation.Threads;

namespace CodeAlta.Views;

internal static class SidebarUiStateHelpers
{
    public static string? GetExpandedProjectId(WorkThreadDescriptor? selectedThread)
        => SidebarSelectionResolver.ResolvePreferredExpandedProjectId(selectedThread?.ProjectRef);

    public static void ToggleNavigator(SidebarView sidebarView, Action focusPromptTarget)
    {
        ArgumentNullException.ThrowIfNull(sidebarView);
        ArgumentNullException.ThrowIfNull(focusPromptTarget);

        var collapse = !sidebarView.IsCollapsed;
        var hadSidebarFocus = sidebarView.Tree.HasFocusWithin;
        sidebarView.SetCollapsed(collapse);
        if (collapse && hadSidebarFocus)
        {
            focusPromptTarget();
        }
    }

    public static SidebarSelectionTarget ResolveCurrentTarget(ShellThreadStateCoordinator threadStateCoordinator)
    {
        ArgumentNullException.ThrowIfNull(threadStateCoordinator);
        return SidebarSelectionResolver.ResolveCurrentTarget(
            threadStateCoordinator.Selection.SelectedThreadId,
            threadStateCoordinator.Selection.SelectedProjectId,
            threadStateCoordinator.Selection.Target is WorkspaceTarget.Draft { IsGlobal: true });
    }

    public static void RefreshProjection(
        SidebarCoordinator sidebarCoordinator,
        ShellThreadStateCoordinator threadStateCoordinator,
        PromptDraftUiCoordinator promptDraftUiCoordinator,
        Func<string, OpenThreadState?> findOpenThread,
        Func<string, bool> isRuntimeThreadRunning,
        Action verifyBindableAccess)
    {
        ArgumentNullException.ThrowIfNull(sidebarCoordinator);
        ArgumentNullException.ThrowIfNull(threadStateCoordinator);
        ArgumentNullException.ThrowIfNull(promptDraftUiCoordinator);
        ArgumentNullException.ThrowIfNull(findOpenThread);
        ArgumentNullException.ThrowIfNull(isRuntimeThreadRunning);
        ArgumentNullException.ThrowIfNull(verifyBindableAccess);

        sidebarCoordinator.RefreshProjection(
            threadStateCoordinator.Projects,
            threadStateCoordinator.Threads,
            GetExpandedProjectId(threadStateCoordinator.GetSelectedThread()),
            ResolveCurrentTarget(threadStateCoordinator),
            threadStateCoordinator.NavigatorSettings,
            threadId => findOpenThread(threadId) is { } tab
                ? new ThreadVisualState(tab.StatusBusy || isRuntimeThreadRunning(threadId), tab.HasPromptDraft)
                : new ThreadVisualState(isRuntimeThreadRunning(threadId), promptDraftUiCoordinator.HasPersistedPromptDraft(threadId)),
            promptDraftUiCoordinator.HasDraftPrompt,
            verifyBindableAccess);
    }
}
