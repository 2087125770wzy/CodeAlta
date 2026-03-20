using CodeAlta.App.State;
using CodeAlta.Catalog;
using CodeAlta.Models;

namespace CodeAlta.App.Context;

internal sealed class ThreadSelectionContext
{
    private readonly ShellThreadStateCoordinator _threadStateCoordinator;
    private readonly Func<WorkThreadDescriptor, CancellationToken, Task> _ensureThreadHistoryLoadedAsync;
    private readonly Func<string, bool> _isSelectedThread;

    public ThreadSelectionContext(
        ShellThreadStateCoordinator threadStateCoordinator,
        Func<WorkThreadDescriptor, CancellationToken, Task> ensureThreadHistoryLoadedAsync,
        Func<string, bool> isSelectedThread)
    {
        ArgumentNullException.ThrowIfNull(threadStateCoordinator);
        ArgumentNullException.ThrowIfNull(ensureThreadHistoryLoadedAsync);
        ArgumentNullException.ThrowIfNull(isSelectedThread);

        _threadStateCoordinator = threadStateCoordinator;
        _ensureThreadHistoryLoadedAsync = ensureThreadHistoryLoadedAsync;
        _isSelectedThread = isSelectedThread;
    }

    public IReadOnlyList<ProjectDescriptor> Projects => _threadStateCoordinator.Projects;

    public IReadOnlyList<WorkThreadDescriptor> Threads => _threadStateCoordinator.Threads;

    public IReadOnlyList<string> OpenThreadIds => _threadStateCoordinator.ViewState.OpenThreadIds;

    public bool DraftTabOpen => _threadStateCoordinator.DraftTabOpen;

    public bool GlobalScopeSelected => _threadStateCoordinator.GlobalScopeSelected;

    public string? SelectedProjectId => _threadStateCoordinator.SelectedProjectId;

    public string? SelectedThreadId => _threadStateCoordinator.SelectedThreadId;

    public OpenThreadState EnsureThreadTab(WorkThreadDescriptor thread)
        => _threadStateCoordinator.EnsureThreadTab(thread);

    public OpenThreadState RegisterDelegatedThread(WorkThreadDescriptor child, OpenThreadState sourceTab)
        => _threadStateCoordinator.RegisterDelegatedThread(child, sourceTab);

    public OpenThreadState? FindOpenThread(string threadId)
        => _threadStateCoordinator.FindOpenThread(threadId);

    public ProjectDescriptor? GetSelectedProject()
        => _threadStateCoordinator.GetSelectedProject();

    public ProjectDescriptor? GetProjectById(string? projectId)
        => _threadStateCoordinator.GetProjectById(projectId);

    public WorkThreadDescriptor? GetSelectedThread()
        => _threadStateCoordinator.GetSelectedThread();

    public WorkThreadDescriptor? FindThread(string? threadId)
        => _threadStateCoordinator.FindThread(threadId);

    public Task EnsureThreadHistoryLoadedAsync(
        WorkThreadDescriptor thread,
        CancellationToken cancellationToken = default)
        => _ensureThreadHistoryLoadedAsync(thread, cancellationToken);

    public bool IsSelectedThread(string threadId)
        => _isSelectedThread(threadId);
}
