using CodeAlta.DotNet;
using CodeAlta.Mcp;
using CodeAlta.Persistence;
using CodeAlta.Search;
using CodeAlta.Workspaces;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Controls;
using XenoAtom.Terminal.UI.Extensions.Markdown;
using XenoAtom.Terminal.UI.Geometry;
using XenoAtom.Terminal.UI.Styling;
using XenoAtom.Terminal.UI.Text;

internal sealed class CodeAltaTerminalUi : IAsyncDisposable
{
    private readonly WorkspaceCatalog _workspaceCatalog;
    private readonly WorkspaceResolver _workspaceResolver;
    private readonly TaskRepository _taskRepository;
    private readonly SearchService _searchService;
    private readonly DotNetIndexService _dotNetIndexService;
    private readonly DotNetDiagnosticsService _dotNetDiagnosticsService;
    private readonly Indexer _indexer;
    private readonly CodeAltaMcpServerFactory _mcpFactory;

    private TerminalApp? _app;
    private DockLayout? _root;
    private TextBlock? _header;
    private TextBlock? _status;

    private TerminalScreen _screen = TerminalScreen.Home;
    private string? _activeWorkspaceKey;
    private string? _activeProjectKey;

    private TextBlock? _workspacesOutput;
    private TextBox? _workspaceKeyInput;
    private TextBlock? _tasksOutput;
    private TextBox? _taskTitleInput;
    private TextBlock? _searchOutput;
    private TextBox? _searchQueryInput;
    private TextBlock? _jobsOutput;
    private TextBlock? _mcpOutput;
    private DocumentFlow? _chatFlow;
    private ChatPromptEditor? _chatInput;
    private Visual? _chatInputView;
    private MarkdownMarkupConverter? _chatMarkdownConverter;

    public CodeAltaTerminalUi(
        WorkspaceCatalog workspaceCatalog,
        WorkspaceResolver workspaceResolver,
        TaskRepository taskRepository,
        SearchService searchService,
        DotNetIndexService dotNetIndexService,
        DotNetDiagnosticsService dotNetDiagnosticsService,
        Indexer indexer,
        CodeAltaMcpServerFactory mcpFactory)
    {
        ArgumentNullException.ThrowIfNull(workspaceCatalog);
        ArgumentNullException.ThrowIfNull(workspaceResolver);
        ArgumentNullException.ThrowIfNull(taskRepository);
        ArgumentNullException.ThrowIfNull(searchService);
        ArgumentNullException.ThrowIfNull(dotNetIndexService);
        ArgumentNullException.ThrowIfNull(dotNetDiagnosticsService);
        ArgumentNullException.ThrowIfNull(indexer);
        ArgumentNullException.ThrowIfNull(mcpFactory);

        _workspaceCatalog = workspaceCatalog;
        _workspaceResolver = workspaceResolver;
        _taskRepository = taskRepository;
        _searchService = searchService;
        _dotNetIndexService = dotNetIndexService;
        _dotNetDiagnosticsService = dotNetDiagnosticsService;
        _indexer = indexer;
        _mcpFactory = mcpFactory;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        _header = new TextBlock
        {
            Wrap = false,
            Text = BuildHeaderText(),
        };

        _status = new TextBlock
        {
            Wrap = true,
            Text = "Ready. Use the buttons below to navigate.",
        };

        var footer = BuildFooter();
        var bottom = new VStack(
            [
                footer,
                _status,
            ])
        {
            Spacing = 1,
        };

        _root = new DockLayout(
            top: _header,
            content: BuildHomeView(),
            bottom: bottom);

        _app = new TerminalApp(
            _root,
            terminal: null,
            options: new TerminalAppOptions());

        await _app.RunAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync().ConfigureAwait(false);
        }
    }

    private Visual BuildFooter()
    {
        return new WrapHStack(
            [
                new Button(new TextBlock("Home")).Click(() => Show(TerminalScreen.Home)),
                new Button(new TextBlock("Chat")).Click(() => Show(TerminalScreen.Chat)),
                new Button(new TextBlock("Workspaces")).Click(() => Show(TerminalScreen.Workspaces)),
                new Button(new TextBlock("Tasks")).Click(() => Show(TerminalScreen.Tasks)),
                new Button(new TextBlock("Search")).Click(() => Show(TerminalScreen.Search)),
                new Button(new TextBlock("Jobs")).Click(() => Show(TerminalScreen.Jobs)),
                new Button(new TextBlock("MCP")).Click(() => Show(TerminalScreen.Mcp)),
                new Button(new TextBlock("Quit")).Click(() => _app?.Stop()),
            ])
        {
            Spacing = 2,
            RunSpacing = 0,
        };
    }

    private void Show(TerminalScreen screen)
    {
        _screen = screen;
        PostToUi(
            () =>
            {
                if (_root is null || _header is null)
                {
                    return;
                }

                _header.Text = BuildHeaderText();
                _root.Content = screen switch
                {
                    TerminalScreen.Home => BuildHomeView(),
                    TerminalScreen.Chat => BuildChatView(),
                    TerminalScreen.Workspaces => BuildWorkspacesView(),
                    TerminalScreen.Tasks => BuildTasksView(),
                    TerminalScreen.Search => BuildSearchView(),
                    TerminalScreen.Jobs => BuildJobsView(),
                    TerminalScreen.Mcp => BuildMcpView(),
                    _ => BuildHomeView(),
                };
            });
    }

    private Visual BuildHomeView()
    {
        return new VStack(
            [
                new TextBlock("CodeAlta (preview)"),
                new TextBlock("This is a minimal headless TUI host over durable state (SQLite + artifacts) and MCP tools."),
                new TextBlock("Use Workspaces/Tasks/Search/Jobs/MCP to exercise services without relying on a full agent runtime."),
                new TextBlock("Note: Some capabilities depend on optional local machine configuration (e.g., sqlite-vec extension path)."),
            ])
        {
            Spacing = 1,
        };
    }

    private Visual BuildChatView()
    {
        _chatMarkdownConverter ??= new MarkdownMarkupConverter();

        _chatFlow = new DocumentFlow
        {
            HorizontalAlignment = Align.Stretch,
            VerticalAlignment = Align.Stretch,
            ItemPadding = new Thickness(1),
            ItemSpacing = 1,
        };
        _chatFlow.ScrollToTail(false);

        var chatInput = new ChatPromptEditor(
                text => _ = SendChatMessageAsync(text))
            .PromptMarkup("[primary]you[/] ")
            .ContinuationPromptMarkup("[muted]·[/] ")
            .EnableWordHints(false)
            .Highlighter(HighlightMarkdown)
            .MinHeight(3)
            .MaxHeight(9)
            ;

        _chatInput = chatInput;
        _chatInputView = chatInput.Scrollable();

        var help = new Markup(
            "[dim]Enter accepts the prompt. Use markdown for formatting; assistant messages will render markdown.[/]");

        var controls = new WrapHStack(
            [
                new Button(new TextBlock("Clear")).Click(ClearChat),
                new Button(new TextBlock("Send")).Click(() => _chatInput?.Accept()),
            ])
        {
            Spacing = 2,
            RunSpacing = 0,
        };

        return new DockLayout(
            top: new VStack([new TextBlock("Chat"), help, controls]) { Spacing = 1 },
            content: _chatFlow,
            bottom: _chatInputView);

        void HighlightMarkdown(in PromptEditorHighlightRequest request, List<StyledRun> runs)
        {
            var converter = _chatMarkdownConverter;
            if (converter is null)
            {
                return;
            }

            converter.Theme = request.Theme;
            converter.Highlight(SnapshotToString(request.Snapshot), runs);
        }

        static string SnapshotToString(ITextSnapshot snapshot)
        {
            if (snapshot.Length == 0)
            {
                return string.Empty;
            }

            return string.Create(snapshot.Length, snapshot, static (span, s) => s.CopyTo(0, span));
        }
    }

    private Visual BuildWorkspacesView()
    {
        _workspacesOutput = new TextBlock { Wrap = true, Text = "Press Refresh to load workspaces." };
        _workspaceKeyInput = new TextBox { Text = _activeWorkspaceKey ?? string.Empty };

        return new VStack(
            [
                new TextBlock("Workspaces"),
                new HStack(
                    [
                        new Button(new TextBlock("Refresh")).Click(() => _ = RefreshWorkspacesAsync()),
                        new TextBlock("Workspace key:"),
                        _workspaceKeyInput,
                        new Button(new TextBlock("Set Active")).Click(SetActiveWorkspaceFromInput),
                        new Button(new TextBlock("Resolve Scope")).Click(() => _ = ResolveActiveScopeAsync()),
                    ])
                    {
                        Spacing = 2,
                    },
                _workspacesOutput,
            ])
        {
            Spacing = 1,
        };
    }

    private Visual BuildTasksView()
    {
        _tasksOutput = new TextBlock { Wrap = true, Text = "Press Refresh to list tasks." };
        _taskTitleInput = new TextBox { Text = string.Empty };

        return new VStack(
            [
                new TextBlock("Tasks"),
                new HStack(
                    [
                        new Button(new TextBlock("Refresh")).Click(() => _ = RefreshTasksAsync()),
                        new TextBlock("New task title:"),
                        _taskTitleInput,
                        new Button(new TextBlock("Create")).Click(() => _ = CreateTaskFromInputAsync()),
                    ])
                    {
                        Spacing = 2,
                    },
                _tasksOutput,
            ])
        {
            Spacing = 1,
        };
    }

    private Visual BuildSearchView()
    {
        _searchOutput = new TextBlock { Wrap = true, Text = "Enter a query and press Run." };
        _searchQueryInput = new TextBox { Text = string.Empty };

        return new VStack(
            [
                new TextBlock("Search"),
                new HStack(
                    [
                        new TextBlock("Query:"),
                        _searchQueryInput,
                        new Button(new TextBlock("Run")).Click(() => _ = RunSearchFromInputAsync()),
                    ])
                    {
                        Spacing = 2,
                    },
                _searchOutput,
            ])
        {
            Spacing = 1,
        };
    }

    private Visual BuildJobsView()
    {
        _jobsOutput = new TextBlock { Wrap = true, Text = "Press Refresh to read background job status." };
        return new VStack(
            [
                new TextBlock("Jobs"),
                new HStack(
                    [
                        new Button(new TextBlock("Refresh")).Click(() => RefreshJobs()),
                        new Button(new TextBlock("Process Index Queue")).Click(() => _ = ProcessIndexQueueAsync()),
                    ])
                    {
                        Spacing = 2,
                    },
                _jobsOutput,
            ])
        {
            Spacing = 1,
        };
    }

    private Visual BuildMcpView()
    {
        _mcpOutput = new TextBlock { Wrap = true, Text = "Press Health Check to list MCP tools." };
        return new VStack(
            [
                new TextBlock("MCP"),
                new HStack(
                    [
                        new Button(new TextBlock("Health Check")).Click(() => _ = McpHealthCheckAsync()),
                    ])
                    {
                        Spacing = 2,
                    },
                _mcpOutput,
            ])
        {
            Spacing = 1,
        };
    }

    private async Task RefreshWorkspacesAsync()
    {
        SetStatus("Loading workspaces...");
        try
        {
            var workspaces = await _workspaceCatalog.LoadAsync().ConfigureAwait(false);
            var text = workspaces.Count == 0
                ? "No workspaces were discovered."
                : string.Join(
                    "\n",
                    workspaces.Select(x => $"- {x.Key} ({x.DisplayName}) projects={x.Projects.Count}"));

            PostToUi(() =>
            {
                if (_workspacesOutput is not null)
                {
                    _workspacesOutput.Text = text;
                }
            });
            SetStatus("Workspaces loaded.");
        }
        catch (Exception ex)
        {
            SetStatus($"Failed to load workspaces: {ex.Message}");
        }
    }

    private void SetActiveWorkspaceFromInput()
    {
        var key = _workspaceKeyInput?.Text?.Trim();
        if (string.IsNullOrWhiteSpace(key))
        {
            SetStatus("Workspace key is required.");
            return;
        }

        _activeWorkspaceKey = key;
        _activeProjectKey = null;
        SetStatus($"Active workspace set: {key}");
        PostToUi(() => _header!.Text = BuildHeaderText());
    }

    private async Task ResolveActiveScopeAsync()
    {
        var key = _activeWorkspaceKey;
        if (string.IsNullOrWhiteSpace(key))
        {
            SetStatus("Set an active workspace key first.");
            return;
        }

        SetStatus("Resolving scope...");
        try
        {
            var resolutions = await _workspaceResolver.ResolveAsync(
                ScopeSelector.Workspace(key),
                cancellationToken: CancellationToken.None).ConfigureAwait(false);

            var text = resolutions.Count == 0
                ? "No matching workspace scope resolutions."
                : string.Join(
                    "\n",
                    resolutions.SelectMany(x =>
                        new[]
                        {
                            $"Workspace: {x.Workspace.Key} ({x.Workspace.DisplayName})",
                        }.Concat(
                            x.Projects.Select(p => $"  - {p.Project.Key} => {p.CheckoutPath}"))));

            PostToUi(() =>
            {
                if (_workspacesOutput is not null)
                {
                    _workspacesOutput.Text = text;
                }
            });

            SetStatus("Scope resolved.");
        }
        catch (Exception ex)
        {
            SetStatus($"Failed to resolve scope: {ex.Message}");
        }
    }

    private async Task RefreshTasksAsync()
    {
        SetStatus("Loading tasks...");
        try
        {
            var tasks = await _taskRepository.ListAsync(limit: 20).ConfigureAwait(false);
            var text = tasks.Count == 0
                ? "No tasks."
                : string.Join("\n", tasks.Select(x => $"- {x.TaskId} [{x.Status}] {x.Title}"));

            PostToUi(() =>
            {
                if (_tasksOutput is not null)
                {
                    _tasksOutput.Text = text;
                }
            });

            SetStatus("Tasks loaded.");
        }
        catch (Exception ex)
        {
            SetStatus($"Failed to load tasks: {ex.Message}");
        }
    }

    private async Task CreateTaskFromInputAsync()
    {
        var title = _taskTitleInput?.Text?.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            SetStatus("Task title is required.");
            return;
        }

        SetStatus("Creating task...");
        try
        {
            await _taskRepository.CreateAsync(new CreateTaskRequest { Title = title }).ConfigureAwait(false);
            PostToUi(() =>
            {
                if (_taskTitleInput is not null)
                {
                    _taskTitleInput.Text = string.Empty;
                }
            });
            await RefreshTasksAsync().ConfigureAwait(false);
            SetStatus("Task created.");
        }
        catch (Exception ex)
        {
            SetStatus($"Failed to create task: {ex.Message}");
        }
    }

    private async Task RunSearchFromInputAsync()
    {
        var queryText = _searchQueryInput?.Text?.Trim();
        if (string.IsNullOrWhiteSpace(queryText))
        {
            SetStatus("Search text is required.");
            return;
        }

        SetStatus("Running search...");
        try
        {
            var results = await _searchService.QueryHybridAsync(
                new CodeAlta.Search.SearchQuery
                {
                    Text = queryText,
                    Limit = 10,
                    PrefilterLimit = 50,
                }).ConfigureAwait(false);

            var text = results.Count == 0
                ? "No results."
                : string.Join(
                    "\n",
                    results.Select(x => $"- {x.Title ?? x.SourceId} ({x.LinkUri})"));

            PostToUi(() =>
            {
                if (_searchOutput is not null)
                {
                    _searchOutput.Text = text;
                }
            });

            SetStatus("Search complete.");
        }
        catch (Exception ex)
        {
            SetStatus($"Search failed: {ex.Message}");
        }
    }

    private void RefreshJobs()
    {
        var status = _indexer.Status;
        var text =
            $"Index queue depth: {status.QueueDepth}\n" +
            $"Last completed: {(status.LastCompletedAt is null ? "(never)" : status.LastCompletedAt.Value.ToString("O"))}";

        PostToUi(() =>
        {
            if (_jobsOutput is not null)
            {
                _jobsOutput.Text = text;
            }
        });

        SetStatus("Job status updated.");
    }

    private async Task ProcessIndexQueueAsync()
    {
        SetStatus("Processing index queue...");
        try
        {
            await _indexer.ProcessNextAsync().ConfigureAwait(false);
            RefreshJobs();
            SetStatus("Index queue processed.");
        }
        catch (Exception ex)
        {
            SetStatus($"Index processing failed: {ex.Message}");
        }
    }

    private async Task McpHealthCheckAsync()
    {
        SetStatus("Checking MCP tools...");
        try
        {
            await using var connection = await InProcessMcpConnection.CreateAsync(_mcpFactory).ConfigureAwait(false);
            var tools = await connection.Client.ListToolsAsync().ConfigureAwait(false);
            var text = $"MCP tools: {tools.Count}\n" + string.Join("\n", tools.Take(30).Select(x => $"- {x.Name}"));

            PostToUi(() =>
            {
                if (_mcpOutput is not null)
                {
                    _mcpOutput.Text = text;
                }
            });

            SetStatus("MCP ready.");
        }
        catch (Exception ex)
        {
            SetStatus($"MCP check failed: {ex.Message}");
        }
    }

    private string BuildHeaderText()
    {
        var scope = _activeWorkspaceKey is null
            ? "global"
            : _activeProjectKey is null
                ? $"workspace:{_activeWorkspaceKey}"
                : $"project:{_activeWorkspaceKey}/{_activeProjectKey}";

        return $"CodeAlta | scope={scope} | screen={_screen.ToString().ToLowerInvariant()} | indexQueue={_indexer.Status.QueueDepth}";
    }

    private void ClearChat()
    {
        PostToUi(() =>
        {
            _chatFlow?.Items.Clear();
            if (_chatInput is not null)
            {
                _chatInput.Text = string.Empty;
            }
        });
    }

    private Task SendChatMessageAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Task.CompletedTask;
        }

        PostToUi(() =>
        {
            if (_chatInput is not null)
            {
                _chatInput.Text = string.Empty;
            }

            var flow = _chatFlow;
            if (flow is null)
            {
                return;
            }

            flow.Items.Add(CreateUserChatItem(text));
            flow.Items.Add(CreateAssistantChatItem(
                "Chat UI is wired (PromptEditor + DocumentFlow + MarkdownControl), but agent runtime integration is not enabled yet."));
            flow.ScrollToTail();
        });

        return Task.CompletedTask;
    }

    private static DocumentFlowItem CreateUserChatItem(string markdown)
    {
        var content = new FlowDocument()
            .Add(new MarkdownControl(markdown)
            {
                HorizontalAlignment = Align.Stretch,
                VerticalAlignment = Align.Stretch,
                Options = MarkdownRenderOptions.Default with
                {
                    WrapCodeBlocks = true,
                    MaxCodeBlockHeight = 10,
                },
            });

        return new DocumentFlowItem
        {
            Content = content,
            Alignment = DocumentFlowAlignment.Right,
            MaxWidth = 80,
            BackgroundStyle = Style.None.WithBackground(Colors.DarkSlateBlue),
            BorderStyle = Style.None.WithForeground(Colors.SlateBlue),
        };
    }

    private static DocumentFlowItem CreateAssistantChatItem(string markdown)
    {
        var content = new FlowDocument()
            .Add(new MarkdownControl(markdown)
            {
                HorizontalAlignment = Align.Stretch,
                VerticalAlignment = Align.Stretch,
                Options = MarkdownRenderOptions.Default with
                {
                    WrapCodeBlocks = true,
                    MaxCodeBlockHeight = 14,
                },
            });

        return new DocumentFlowItem
        {
            Content = content,
            Alignment = DocumentFlowAlignment.Left,
            MaxWidth = 84,
            BackgroundStyle = Style.None.WithBackground(Colors.DarkSlateGray),
            BorderStyle = Style.None.WithForeground(Colors.SlateGray),
        };
    }

    private void SetStatus(string message)
    {
        PostToUi(() =>
        {
            if (_status is not null)
            {
                _status.Text = message;
            }
        });
    }

    private void PostToUi(Action action)
    {
        var app = _app;
        if (app is null)
        {
            return;
        }

        app.Post(action);
    }

    private enum TerminalScreen
    {
        Home,
        Chat,
        Workspaces,
        Tasks,
        Search,
        Jobs,
        Mcp,
    }

    private sealed class ChatPromptEditor : PromptEditor
    {
        private readonly Action<string> _onAccepted;

        public ChatPromptEditor(Action<string> onAccepted)
        {
            ArgumentNullException.ThrowIfNull(onAccepted);
            _onAccepted = onAccepted;
        }

        protected override void OnAccepted(PromptEditorAcceptedEventArgs e)
        {
            ArgumentNullException.ThrowIfNull(e);
            _onAccepted(e.Text);
            base.OnAccepted(e);
        }
    }
}
