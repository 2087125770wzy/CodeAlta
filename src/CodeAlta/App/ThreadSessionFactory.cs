using CodeAlta.Agent;
using CodeAlta.App.State;
using CodeAlta.Catalog;
using CodeAlta.Models;
using CodeAlta.Presentation.Shell;
using CodeAlta.Presentation.Timeline;
using CodeAlta.Threading;
using CodeAlta.Views;
using XenoAtom.Terminal.UI.Geometry;

namespace CodeAlta.App;

internal interface IThreadTimelineSurface
{
    Rectangle? GetTimelineBounds();
}

internal interface IThreadPromptDraftService
{
    string? LoadPromptDraft(string threadId);

    void DeletePromptDraft(string threadId);
}

internal interface IThreadModelProviderPreferenceService
{
    void ApplyThreadPreference(OpenThreadState thread);

    void RememberThreadPreference(string threadId, string? modelId, AgentReasoningEffort? reasoningEffort, bool persistNow);
}

internal interface IThreadProjectRootResolver
{
    ProjectDescriptor? GetSelectedProject();

    ProjectDescriptor? GetProjectById(string? projectId);

    string? ResolveProjectRoot(SessionViewDescriptor thread);
}

internal sealed class ThreadTimelineSurface : IThreadTimelineSurface
{
    private readonly Func<Rectangle?> _getTimelineBounds;

    public ThreadTimelineSurface(Func<Rectangle?> getTimelineBounds)
    {
        ArgumentNullException.ThrowIfNull(getTimelineBounds);
        _getTimelineBounds = getTimelineBounds;
    }

    public Rectangle? GetTimelineBounds()
        => _getTimelineBounds();
}

internal sealed class ThreadPromptDraftService : IThreadPromptDraftService
{
    private readonly Func<string, string?> _loadPromptDraft;
    private readonly Action<string> _deletePromptDraft;

    public ThreadPromptDraftService(Func<string, string?> loadPromptDraft, Action<string> deletePromptDraft)
    {
        ArgumentNullException.ThrowIfNull(loadPromptDraft);
        ArgumentNullException.ThrowIfNull(deletePromptDraft);
        _loadPromptDraft = loadPromptDraft;
        _deletePromptDraft = deletePromptDraft;
    }

    public string? LoadPromptDraft(string threadId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(threadId);
        return _loadPromptDraft(threadId);
    }

    public void DeletePromptDraft(string threadId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(threadId);
        _deletePromptDraft(threadId);
    }
}

internal sealed class ThreadModelProviderPreferenceService : IThreadModelProviderPreferenceService
{
    private readonly Action<OpenThreadState> _applyThreadPreference;
    private readonly Action<string, string?, AgentReasoningEffort?, bool> _rememberThreadPreference;

    public ThreadModelProviderPreferenceService(
        Action<OpenThreadState> applyThreadPreference,
        Action<string, string?, AgentReasoningEffort?, bool> rememberThreadPreference)
    {
        ArgumentNullException.ThrowIfNull(applyThreadPreference);
        ArgumentNullException.ThrowIfNull(rememberThreadPreference);
        _applyThreadPreference = applyThreadPreference;
        _rememberThreadPreference = rememberThreadPreference;
    }

    public void ApplyThreadPreference(OpenThreadState thread)
    {
        ArgumentNullException.ThrowIfNull(thread);
        _applyThreadPreference(thread);
    }

    public void RememberThreadPreference(string threadId, string? modelId, AgentReasoningEffort? reasoningEffort, bool persistNow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(threadId);
        _rememberThreadPreference(threadId, modelId, reasoningEffort, persistNow);
    }
}

internal sealed class ThreadProjectRootResolver : IThreadProjectRootResolver
{
    private readonly Func<ProjectDescriptor?> _getSelectedProject;
    private readonly Func<string?, ProjectDescriptor?> _getProjectById;

    public ThreadProjectRootResolver(
        Func<ProjectDescriptor?> getSelectedProject,
        Func<string?, ProjectDescriptor?> getProjectById)
    {
        ArgumentNullException.ThrowIfNull(getSelectedProject);
        ArgumentNullException.ThrowIfNull(getProjectById);
        _getSelectedProject = getSelectedProject;
        _getProjectById = getProjectById;
    }

    public ProjectDescriptor? GetSelectedProject()
        => _getSelectedProject();

    public ProjectDescriptor? GetProjectById(string? projectId)
        => _getProjectById(projectId);

    public string? ResolveProjectRoot(SessionViewDescriptor thread)
    {
        ArgumentNullException.ThrowIfNull(thread);
        return PromptReferenceProjectRootResolver.Resolve(thread, GetProjectById, GetSelectedProject);
    }
}

internal sealed class ThreadSessionFactory
{
    private readonly IUiDispatcher _uiDispatcher;
    private readonly IThreadTimelineSurface _timelineSurface;
    private readonly IThreadPromptDraftService _promptDrafts;
    private readonly IThreadModelProviderPreferenceService _modelProviderPreferences;
    private readonly IThreadProjectRootResolver _projectRootResolver;

    public ThreadSessionFactory(
        IUiDispatcher uiDispatcher,
        IThreadTimelineSurface timelineSurface,
        IThreadPromptDraftService promptDrafts,
        IThreadModelProviderPreferenceService modelProviderPreferences,
        IThreadProjectRootResolver projectRootResolver)
    {
        ArgumentNullException.ThrowIfNull(uiDispatcher);
        ArgumentNullException.ThrowIfNull(timelineSurface);
        ArgumentNullException.ThrowIfNull(promptDrafts);
        ArgumentNullException.ThrowIfNull(modelProviderPreferences);
        ArgumentNullException.ThrowIfNull(projectRootResolver);

        _uiDispatcher = uiDispatcher;
        _timelineSurface = timelineSurface;
        _promptDrafts = promptDrafts;
        _modelProviderPreferences = modelProviderPreferences;
        _projectRootResolver = projectRootResolver;
    }

    public OpenThreadState CreateThreadSession(SessionViewDescriptor thread)
    {
        ArgumentNullException.ThrowIfNull(thread);

        var timeline = new ThreadTimelinePresenter(
            _uiDispatcher,
            _timelineSurface.GetTimelineBounds,
            _projectRootResolver.ResolveProjectRoot(thread));
        var state = new OpenThreadState(thread, timeline)
        {
            BackendId = new AgentBackendId(thread.BackendId),
            StatusMessage = ShellTextFormatter.BuildReadyStatusText(thread, _projectRootResolver.GetSelectedProject(), globalScopeSelected: false),
        };
        state.Session.PromptDraftText = _promptDrafts.LoadPromptDraft(thread.ThreadId) ?? string.Empty;
        state.ViewModel.Title = thread.Title;

        _modelProviderPreferences.ApplyThreadPreference(state);
        _modelProviderPreferences.RememberThreadPreference(thread.ThreadId, state.ModelId, state.ReasoningEffort, false);

        return state;
    }

    public void UpdateThreadSession(OpenThreadState state, SessionViewDescriptor thread)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(thread);

        state.Thread = thread;
        state.Timeline.SetLocalFileRootPath(_projectRootResolver.ResolveProjectRoot(thread));
        state.ViewModel.ThreadId = thread.ThreadId;
        state.ViewModel.Title = thread.Title;
    }
}
