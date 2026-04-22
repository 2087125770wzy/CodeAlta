using CodeAlta.App;
using CodeAlta.Catalog;
using CodeAlta.ViewModels;
using XenoAtom.Ansi;
using XenoAtom.Terminal;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Commands;
using XenoAtom.Terminal.UI.Controls;
using XenoAtom.Terminal.UI.Geometry;
using XenoAtom.Terminal.UI.Input;
using XenoAtom.Terminal.UI.Styling;
using XenoAtom.Terminal.UI.Templating;

namespace CodeAlta.Views;

internal sealed class ModelProvidersDialog
{
    private static readonly ProviderTypeOption[] ProviderTypes =
    [
        new("codex", "Codex"),
        new("copilot", "GitHub Copilot"),
        new("openai-chat", "OpenAI Chat"),
        new("openai-responses", "OpenAI Responses"),
        new("anthropic", "Anthropic"),
        new("google-genai", "Google GenAI"),
        new("vertex-ai", "Vertex AI"),
    ];

    private static readonly ReasoningOption[] ReasoningOptions =
    [
        new("none", "None"),
        new("minimal", "Minimal"),
        new("low", "Low"),
        new("medium", "Medium"),
        new("high", "High"),
        new("xhigh", "XHigh"),
    ];

    private readonly Func<IReadOnlyList<CodeAltaProviderDocument>> _loadDefinitions;
    private readonly Func<IReadOnlyList<CodeAltaProviderDocument>, Task> _saveDefinitionsAsync;
    private readonly Func<CodeAltaProviderDocument, Task<ProviderTestResult>> _testProviderAsync;
    private readonly Func<Rectangle?> _getBounds;
    private readonly Func<Visual?> _getFocusTarget;
    private readonly Dialog _dialog;
    private readonly ListBox<ModelProviderEditorItemViewModel> _providerList;
    private readonly State<int> _detailVersion = new(0);
    private readonly Markup _statusMarkup;
    private readonly Visual _detailHost;
    private readonly List<ModelProviderEditorItemViewModel> _providers = [];
    private Visual? _detailContent;
    private bool _dirty;
    private int _activeOperationCount;
    private int _selectedIndex = -1;
    private string _statusText = "[dim]Configure model providers and save to refresh the runtime.[/]";

    public ModelProvidersDialog(
        Func<IReadOnlyList<CodeAltaProviderDocument>> loadDefinitions,
        Func<IReadOnlyList<CodeAltaProviderDocument>, Task> saveDefinitionsAsync,
        Func<CodeAltaProviderDocument, Task<ProviderTestResult>> testProviderAsync,
        Func<Rectangle?> getBounds,
        Func<Visual?> getFocusTarget)
    {
        ArgumentNullException.ThrowIfNull(loadDefinitions);
        ArgumentNullException.ThrowIfNull(saveDefinitionsAsync);
        ArgumentNullException.ThrowIfNull(testProviderAsync);
        ArgumentNullException.ThrowIfNull(getBounds);
        ArgumentNullException.ThrowIfNull(getFocusTarget);

        _loadDefinitions = loadDefinitions;
        _saveDefinitionsAsync = saveDefinitionsAsync;
        _testProviderAsync = testProviderAsync;
        _getBounds = getBounds;
        _getFocusTarget = getFocusTarget;

        var closeButton = new Button(new TextBlock($"{NerdFont.MdClose} Close"))
        {
            HorizontalAlignment = Align.End,
            VerticalAlignment = Align.Start,
            Tone = ControlTone.Default,
        };
        closeButton.Click(RequestClose);

        _providerList = new ListBox<ModelProviderEditorItemViewModel>()
            .MinWidth(28)
            .Stretch();
        _providerList.ItemTemplate = new DataTemplate<ModelProviderEditorItemViewModel>(
            static (DataTemplateValue<ModelProviderEditorItemViewModel> value, in DataTemplateContext _) => BuildProviderListItem(value.GetValue()),
            null);
        _providerList.KeyDown((_, _) => ScheduleProviderSelectionSync());
        _providerList.PointerReleased((_, _) => ScheduleProviderSelectionSync());
        _providerList.PointerWheel((_, _) => ScheduleProviderSelectionSync());

        _statusMarkup = new Markup(() => _statusText)
        {
            Wrap = true,
        };

        _detailHost = new ComputedVisual(
            () =>
            {
                _ = _detailVersion.Value;
                return _detailContent ?? BuildEmptyState();
            });

        var addButton = new Button($"{NerdFont.MdPlus} Add")
            .Tone(ControlTone.Primary)
            .Click(AddProvider);
        var deleteButton = new Button($"{NerdFont.MdDelete} Delete")
            .Tone(ControlTone.Error)
            .Click(DeleteSelectedProvider);
        var reloadButton = new Button("Reload")
            .Tone(ControlTone.Warning)
            .Click(() => _ = ReloadAsync(confirmWhenDirty: true));
        var saveButton = new Button("Save")
            .Tone(ControlTone.Success)
            .Click(() => _ = SaveAsync());

        var toolbar = new HStack(addButton, deleteButton, reloadButton, saveButton)
        {
            HorizontalAlignment = Align.End,
            Spacing = 1,
        };

        var leftPane = new VStack(
            new TextBlock("Providers") { Wrap = false },
            new Border(_providerList.Stretch())
                .Style(BorderStyle.Rounded)
                .Padding(new Thickness(1, 0, 1, 0))
                .HorizontalAlignment(Align.Stretch)
                .VerticalAlignment(Align.Stretch),
            new Markup("[dim]Tip: reserved providers stay in the list even when disabled.[/]") { Wrap = true })
        {
            HorizontalAlignment = Align.Stretch,
            VerticalAlignment = Align.Stretch,
            Spacing = 1,
        };

        var splitter = new HSplitter(
            leftPane,
            new Border(new ScrollViewer(_detailHost).Stretch())
                .Style(BorderStyle.Rounded)
                .Padding(1)
                .HorizontalAlignment(Align.Stretch)
                .VerticalAlignment(Align.Stretch))
        {
            Ratio = 0.28,
            MinFirst = 26,
            MinSecond = 50,
        };

        var intro = new Markup("[dim]Enable the providers you want available in CodeAlta. Save applies the configuration and refreshes the runtime immediately.[/]")
        {
            Wrap = true,
        };

        var content = new Grid
            {
                HorizontalAlignment = Align.Stretch,
                VerticalAlignment = Align.Stretch,
            }
            .Rows(
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Star(1) },
                new RowDefinition { Height = GridLength.Auto })
            .Columns(new ColumnDefinition { Width = GridLength.Star(1) });
        content.Cell(toolbar, 0, 0);
        content.Cell(intro, 1, 0);
        content.Cell(splitter, 2, 0);
        content.Cell(_statusMarkup, 3, 0);

        _dialog = new Dialog()
            .Title("Model Providers")
            .TopRightText(closeButton)
            .BottomRightText(new Markup("[dim]Ctrl+G Ctrl+M reopen · Esc close[/]"))
            .IsModal(true)
            .Padding(1)
            .Content(content);
        ResponsiveDialogSize.Apply(_dialog, getBounds(), minWidth: 110, minHeight: 28, widthFactor: 0.60, heightFactor: 0.82);
        _dialog.AddCommand(new Command
        {
            Id = "CodeAlta.Providers.Manage.Close",
            LabelMarkup = "Close",
            DescriptionMarkup = "Close the model providers dialog.",
            Gesture = new KeyGesture(TerminalKey.Escape),
            Importance = CommandImportance.Primary,
            Execute = _ => RequestClose(),
        });
    }

    public void Show()
    {
        if (_dialog.App is not null)
        {
            return;
        }

        _dialog.Show();
        _ = ReloadAsync(confirmWhenDirty: false);
    }

    private async Task ReloadAsync(bool confirmWhenDirty)
    {
        if (confirmWhenDirty && _dirty)
        {
            new ConfirmationDialog(
                "Reload Provider Configuration?",
                ["Discard unsaved provider changes and reload from disk?"],
                "Reload",
                ControlTone.Warning,
                async () => await ReloadAsync(confirmWhenDirty: false),
                _getBounds,
                _getFocusTarget)
                .Show();
            return;
        }

        if (!TryBeginDialogOperation("reload provider configuration"))
        {
            return;
        }

        try
        {
            LoadDefinitionsIntoDialog(
                _loadDefinitions(),
                emptyStatusText: "[warning]No providers are configured yet. Add one, or enable Codex/Copilot.[/]",
                loadedStatusText: "[dim]Provider configuration loaded from disk.[/]");
        }
        finally
        {
            EndDialogOperation();
        }
    }

    private void AddProvider()
    {
        var nextIndex = 1;
        string providerKey;
        do
        {
            providerKey = $"provider-{nextIndex++}";
        }
        while (_providers.Any(item => string.Equals(item.ProviderKey, providerKey, StringComparison.OrdinalIgnoreCase)));

        var item = CreateEditorItem(ModelProviderEditorItemViewModel.Create(providerKey));
        _providers.Add(item);
        _providerList.Items.Add(item);
        MarkDirty();
        SelectProvider(_providers.Count - 1);
    }

    private void DeleteSelectedProvider()
    {
        var item = GetSelectedItem();
        if (item is null)
        {
            _statusText = "[warning]Select a provider to delete.[/]";
            return;
        }

        if (item.IsReserved)
        {
            _statusText = "[warning]Reserved providers cannot be deleted. Disable them instead.[/]";
            return;
        }

        _providers.Remove(item);
        _providerList.Items.Remove(item);
        MarkDirty();
        SelectProvider(Math.Clamp(_selectedIndex, 0, _providers.Count - 1));
    }

    private async Task SaveAsync()
    {
        if (!TryBuildDefinitions(out var definitions, out var errorMessage))
        {
            _statusText = $"[warning]{AnsiMarkup.Escape(errorMessage)}[/]";
            return;
        }

        if (!TryBeginDialogOperation("save provider changes"))
        {
            return;
        }

        try
        {
            _statusText = "[primary]Saving provider configuration...[/]";
            await _saveDefinitionsAsync(definitions);
            LoadDefinitionsIntoDialog(
                _loadDefinitions(),
                emptyStatusText: "[warning]No providers are configured yet. Add one, or enable Codex/Copilot.[/]",
                loadedStatusText: "[success]Provider configuration saved and runtime refreshed.[/]");
        }
        catch (Exception ex)
        {
            _statusText = $"[error]{AnsiMarkup.Escape(ex.GetBaseException().Message)}[/]";
        }
        finally
        {
            EndDialogOperation();
        }
    }

    private async Task TestSelectedAsync()
    {
        var item = GetSelectedItem();
        if (item is null)
        {
            _statusText = "[warning]Select a provider to test.[/]";
            return;
        }

        if (!TryBuildDefinition(item, out var definition, out var errorMessage))
        {
            _statusText = $"[warning]{AnsiMarkup.Escape(errorMessage)}[/]";
            return;
        }

        if (!TryBeginDialogOperation("test a provider"))
        {
            return;
        }

        try
        {
            _statusText = $"[primary]Testing {AnsiMarkup.Escape(item.Label)}...[/]";
            var result = await _testProviderAsync(definition);
            _statusText = result.Success
                ? $"[success]{AnsiMarkup.Escape(result.Message)}[/]"
                : $"[warning]{AnsiMarkup.Escape(result.Message)}[/]";
        }
        catch (Exception ex)
        {
            _statusText = $"[error]{AnsiMarkup.Escape(ex.GetBaseException().Message)}[/]";
        }
        finally
        {
            EndDialogOperation();
        }
    }

    private void SelectProvider(int index, bool force = false)
    {
        if (!force && _selectedIndex == index && _detailContent is not null)
        {
            _providerList.SelectedIndex = index;
            return;
        }

        _selectedIndex = index;
        _providerList.SelectedIndex = index;
        _detailContent = index >= 0 && index < _providers.Count
            ? BuildDetailPane(_providers[index])
            : BuildEmptyState();
        _detailVersion.Value++;
    }

    private Visual BuildDetailPane(ModelProviderEditorItemViewModel item)
    {
        var bindings = GetBindings(item);
        var title = new Markup(() => BuildSelectedTitleMarkup(item))
        {
            Wrap = true,
        };
        var summary = new TextBlock(() => BuildSelectedSummary(item))
        {
            Wrap = true,
        };

        var testButton = new Button("Test Provider")
            .Tone(ControlTone.Primary)
            .Click(() => _ = TestSelectedAsync());

        var form = new Grid
            {
                HorizontalAlignment = Align.Stretch,
                VerticalAlignment = Align.Stretch,
                RowGap = 1,
            }
            .Columns(
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star(1) },
                new ColumnDefinition { Width = GridLength.Auto });

        var row = 0;
        AddTextRow(form, ref row, "Provider Key", CreateKeyField(item), CreateSpacer());
        AddSelectRow(form, ref row, "Type", CreateTypeSelect(item), CreateSpacer());
        AddCheckRow(form, ref row, "Enabled", CreateEnabledCheckBox(item), CreateSpacer());
        AddTextRow(form, ref row, "Display Name", CreateDefaultTextField(bindings.DisplayName, () => item.UseDefaultDisplayName), CreateDefaultCheckBox("Default", bindings.UseDefaultDisplayName));
        AddTextRow(form, ref row, "Model", CreateDefaultTextField(bindings.Model, () => item.UseDefaultModel), CreateDefaultCheckBox("Default", bindings.UseDefaultModel));
        AddSelectRow(form, ref row, "Reasoning", CreateReasoningSelect(item), CreateDefaultCheckBox("Default", bindings.UseDefaultReasoningEffort));

        if (item.ProviderType is "openai-chat" or "openai-responses" or "anthropic" or "google-genai")
        {
            AddTextRow(form, ref row, "API Key", CreateApiKeyBox(item), CreateDefaultCheckBox("Default", bindings.UseDefaultApiKey));
            AddTextRow(form, ref row, "API Key Env", CreateDefaultTextField(bindings.ApiKeyEnv, () => item.UseDefaultApiKeyEnv), CreateDefaultCheckBox("Default", bindings.UseDefaultApiKeyEnv));
        }

        if (item.ProviderType is "openai-chat" or "openai-responses" or "anthropic" or "google-genai" or "vertex-ai")
        {
            AddTextRow(form, ref row, "API URL", CreateApiUrlField(item), CreateDefaultCheckBox("Default", bindings.UseDefaultApiUrl));
        }

        if (item.ProviderType is "openai-chat" or "openai-responses")
        {
            AddTextRow(form, ref row, "Organization", CreateDefaultTextField(bindings.OrganizationId, () => item.UseDefaultOrganizationId), CreateDefaultCheckBox("Default", bindings.UseDefaultOrganizationId));
            AddTextRow(form, ref row, "Project Id", CreateDefaultTextField(bindings.ProjectId, () => item.UseDefaultProjectId), CreateDefaultCheckBox("Default", bindings.UseDefaultProjectId));
        }

        if (item.ProviderType == "vertex-ai")
        {
            AddTextRow(form, ref row, "Project", CreateDefaultTextField(bindings.Project, () => item.UseDefaultProject), CreateDefaultCheckBox("Default", bindings.UseDefaultProject));
            AddTextRow(form, ref row, "Location", CreateDefaultTextField(bindings.Location, () => item.UseDefaultLocation), CreateDefaultCheckBox("Default", bindings.UseDefaultLocation));
        }

        if (item.ProviderType is "openai-chat" or "openai-responses" or "anthropic" or "google-genai" or "vertex-ai")
        {
            AddTextRow(form, ref row, "Models.dev Id", CreateDefaultTextField(bindings.ModelsDevProviderId, () => item.UseDefaultModelsDevProviderId), CreateDefaultCheckBox("Default", bindings.UseDefaultModelsDevProviderId));
            AddTextRow(form, ref row, "Single Model Id", CreateDefaultTextField(bindings.SingleModelId, () => item.UseDefaultSingleModelId), CreateDefaultCheckBox("Default", bindings.UseDefaultSingleModelId));
        }

        var advancedNotice = new Markup("[dim]Advanced provider TOML sections such as profile, compaction, extra_body, and model_overrides are preserved unchanged when you save from this dialog.[/]")
        {
            Wrap = true,
        };

        return new VStack(
            title,
            summary,
            testButton,
            form,
            advancedNotice)
        {
            HorizontalAlignment = Align.Stretch,
            VerticalAlignment = Align.Stretch,
            Spacing = 1,
        };
    }

    private Visual CreateKeyField(ModelProviderEditorItemViewModel item)
    {
        var binding = GetBindings(item).ProviderKey;
        return new TextBox(binding)
            .IsEnabled(!item.IsReserved)
            .Validate(binding, _ => ValidateProviderKey(item));
    }

    private Select<ProviderTypeOption> CreateTypeSelect(ModelProviderEditorItemViewModel item)
    {
        var select = new Select<ProviderTypeOption>()
            .MinWidth(22)
            .IsEnabled(!item.IsReserved);
        foreach (var option in ProviderTypes)
        {
            select.Items.Add(option);
        }

        select.SelectedIndex = Math.Max(0, Array.FindIndex(ProviderTypes, option => string.Equals(option.Id, item.ProviderType, StringComparison.Ordinal)));
        select.SelectionChanged(
            (_, e) =>
            {
                if ((uint)e.NewIndex >= (uint)ProviderTypes.Length)
                {
                    return;
                }

                item.ProviderType = ProviderTypes[e.NewIndex].Id;
                MarkDirty(rebuildEditor: true);
            });
        return select;
    }

    private CheckBox CreateEnabledCheckBox(ModelProviderEditorItemViewModel item)
        => new CheckBox("Enabled").IsChecked(GetBindings(item).Enabled);

    private Visual CreateReasoningSelect(ModelProviderEditorItemViewModel item)
    {
        var select = new Select<ReasoningOption>()
            .MinWidth(16)
            .IsEnabled(() => !item.UseDefaultReasoningEffort);
        foreach (var option in ReasoningOptions)
        {
            select.Items.Add(option);
        }

        select.SelectedIndex = Math.Max(0, Array.FindIndex(ReasoningOptions, option => string.Equals(option.Value, item.ReasoningEffort, StringComparison.Ordinal)));
        select.SelectionChanged(
            (_, e) =>
            {
                if ((uint)e.NewIndex >= (uint)ReasoningOptions.Length)
                {
                    return;
                }

                item.ReasoningEffort = ReasoningOptions[e.NewIndex].Value;
                MarkDirty();
            });
        return select;
    }

    private TextBox CreateApiKeyBox(ModelProviderEditorItemViewModel item)
    {
        var binding = GetBindings(item).ApiKey;
        return new TextBox(binding)
            .IsPassword(true)
            .PasswordRevealMode(PasswordRevealMode.WhileFocused)
            .IsEnabled(() => !item.UseDefaultApiKey);
    }

    private Visual CreateApiUrlField(ModelProviderEditorItemViewModel item)
    {
        var binding = GetBindings(item).ApiUrl;
        return new TextBox(binding)
            .IsEnabled(() => !item.UseDefaultApiUrl)
            .Validate(binding, _ => ValidateApiUrl(item));
    }

    private static Visual CreateDefaultTextField(Binding<string?> binding, Func<bool> getUseDefault)
        => new TextBox(binding)
            .IsEnabled(() => !getUseDefault())
            .Stretch();

    private static CheckBox CreateDefaultCheckBox(string label, Binding<bool> binding)
        => new CheckBox(label).IsChecked(binding);

    private static Visual CreateSpacer()
        => new TextBlock(string.Empty);

    private static void AddTextRow(Grid form, ref int row, string label, Visual content, Visual trailing)
    {
        EnsureRow(form, row);
        form.Cell(new TextBlock(label) { VerticalAlignment = Align.Center }, row, 0);
        form.Cell(content.Stretch(), row, 1);
        form.Cell(trailing, row, 2);
        row++;
    }

    private static void AddSelectRow(Grid form, ref int row, string label, Visual content, Visual trailing)
        => AddTextRow(form, ref row, label, content, trailing);

    private static void AddCheckRow(Grid form, ref int row, string label, Visual content, Visual trailing)
        => AddTextRow(form, ref row, label, content, trailing);

    private static void EnsureRow(Grid form, int row)
    {
        while (form.RowDefinitions.Count <= row)
        {
            form.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }
    }

    private string BuildSelectedTitleMarkup(ModelProviderEditorItemViewModel item)
    {
        var tone = item.Enabled ? "success" : "muted";
        var icon = item.Enabled ? $"{NerdFont.MdCheckCircleOutline}" : $"{NerdFont.MdPauseCircleOutline}";
        return $"[{tone}]{icon} {AnsiMarkup.Escape(item.Label)}[/]";
    }

    private string BuildSelectedSummary(ModelProviderEditorItemViewModel item)
    {
        var errors = BuildValidationErrors(item);
        if (errors.Count == 0)
        {
            return item.Enabled
                ? "This provider is enabled. Use Test Provider to validate the current settings before saving."
                : "This provider is disabled. Enable it when you are ready to use it.";
        }

        return string.Join(Environment.NewLine, errors.Select(static error => $"• {error}"));
    }

    private ValidationMessage? ValidateProviderKey(ModelProviderEditorItemViewModel item)
    {
        if (string.IsNullOrWhiteSpace(item.ProviderKey))
        {
            return new ValidationMessage(ValidationSeverity.Error, "Provider key is required.");
        }

        var normalized = item.ProviderKey.Trim().ToLowerInvariant();
        if (normalized.Any(ch => !(char.IsAsciiLetterOrDigit(ch) || ch is '-' or '_')))
        {
            return new ValidationMessage(ValidationSeverity.Error, "Use lowercase letters, numbers, '-' or '_'.");
        }

        if (_providers.Any(other => !ReferenceEquals(other, item) && string.Equals(other.ProviderKey, normalized, StringComparison.OrdinalIgnoreCase)))
        {
            return new ValidationMessage(ValidationSeverity.Error, "Provider key is already used.");
        }

        return null;
    }

    private static ValidationMessage? ValidateApiUrl(ModelProviderEditorItemViewModel item)
    {
        if (item.UseDefaultApiUrl || string.IsNullOrWhiteSpace(item.ApiUrl))
        {
            return null;
        }

        return Uri.TryCreate(item.ApiUrl.Trim(), UriKind.Absolute, out _)
            ? null
            : new ValidationMessage(ValidationSeverity.Error, "Use an absolute URL.");
    }

    private List<string> BuildValidationErrors(ModelProviderEditorItemViewModel item)
    {
        var errors = new List<string>();
        if (ValidateProviderKey(item) is { } keyMessage)
        {
            errors.Add(GetValidationText(keyMessage));
        }

        if (ValidateApiUrl(item) is { } urlMessage)
        {
            errors.Add(GetValidationText(urlMessage));
        }

        if (!item.IsReserved && item.ProviderType is "codex" or "copilot")
        {
            errors.Add("Only the reserved codex/copilot entries can use built-in provider types.");
        }

        if (!item.Enabled)
        {
            return errors;
        }

        if (item.ProviderType is "openai-chat" or "openai-responses" or "anthropic" or "google-genai")
        {
            var hasApiKey = !item.UseDefaultApiKey && !string.IsNullOrWhiteSpace(item.ApiKey);
            var hasApiKeyEnv = !item.UseDefaultApiKeyEnv && !string.IsNullOrWhiteSpace(item.ApiKeyEnv);
            if (!hasApiKey && !hasApiKeyEnv)
            {
                errors.Add("Provide either API Key or API Key Env when the provider is enabled.");
            }
        }

        if (item.ProviderType == "vertex-ai")
        {
            if (string.IsNullOrWhiteSpace(item.Project))
            {
                errors.Add("Project is required for enabled Vertex AI providers.");
            }

            if (string.IsNullOrWhiteSpace(item.Location))
            {
                errors.Add("Location is required for enabled Vertex AI providers.");
            }
        }

        return errors.Distinct(StringComparer.Ordinal).ToList();
    }

    private bool TryBuildDefinitions(
        out IReadOnlyList<CodeAltaProviderDocument> definitions,
        out string errorMessage)
    {
        var result = new List<CodeAltaProviderDocument>(_providers.Count);
        foreach (var item in _providers)
        {
            if (!TryBuildDefinition(item, out var definition, out errorMessage))
            {
                definitions = [];
                return false;
            }

            result.Add(definition);
        }

        definitions = result;
        errorMessage = string.Empty;
        return true;
    }

    private bool TryBuildDefinition(
        ModelProviderEditorItemViewModel item,
        out CodeAltaProviderDocument definition,
        out string errorMessage)
    {
        var errors = BuildValidationErrors(item);
        if (errors.Count > 0)
        {
            definition = null!;
            errorMessage = $"{item.Label}: {errors[0]}";
            return false;
        }

        definition = item.ToDocument();
        errorMessage = string.Empty;
        return true;
    }

    private void MarkDirty(bool rebuildEditor = false)
    {
        _dirty = true;
        if (rebuildEditor)
        {
            SelectProvider(_selectedIndex, force: true);
        }
    }

    private ModelProviderEditorItemViewModel CreateEditorItem(CodeAltaProviderDocument definition)
        => CreateEditorItem(ModelProviderEditorItemViewModel.FromDocument(definition));

    private ModelProviderEditorItemViewModel CreateEditorItem(ModelProviderEditorItemViewModel item)
    {
        ArgumentNullException.ThrowIfNull(item);

        item.Changed += OnProviderItemChanged;
        return item;
    }

    private void OnProviderItemChanged(ModelProviderEditorItemViewModel item, bool rebuildEditor)
    {
        ArgumentNullException.ThrowIfNull(item);

        MarkDirty(rebuildEditor);
    }

    private void ScheduleProviderSelectionSync()
        => BindingManager.Current.RunAfterTracking(() => SelectProvider(_providerList.SelectedIndex));

    private void RequestClose()
    {
        if (_activeOperationCount > 0)
        {
            _statusText = "[warning]Please wait for the current provider operation to complete before closing this dialog.[/]";
            return;
        }

        if (!_dirty)
        {
            Close();
            return;
        }

        new ConfirmationDialog(
            "Discard Provider Changes?",
            ["You have unsaved model provider changes.", "Close the dialog without saving?"],
            "Discard",
            ControlTone.Error,
            () =>
            {
                Close();
                return Task.CompletedTask;
            },
            _getBounds,
            _getFocusTarget)
            .Show();
    }

    private void Close()
    {
        var app = _dialog.App;
        _dialog.Close();
        if (_getFocusTarget() is { } focusTarget)
        {
            app?.Focus(focusTarget);
        }
    }

    private ModelProviderEditorItemViewModel? GetSelectedItem()
        => _selectedIndex >= 0 && _selectedIndex < _providers.Count ? _providers[_selectedIndex] : null;

    private static Visual BuildEmptyState()
        => new TextBlock("Add a provider on the left, or enable one of the reserved providers to get started.")
        {
            Wrap = true,
        };

    private static string GetValidationText(ValidationMessage message)
        => message.Content is TextBlock textBlock
            ? textBlock.Text ?? string.Empty
            : message.Content.ToString() ?? string.Empty;

    private void LoadDefinitionsIntoDialog(
        IReadOnlyList<CodeAltaProviderDocument> definitions,
        string emptyStatusText,
        string loadedStatusText)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentException.ThrowIfNullOrWhiteSpace(emptyStatusText);
        ArgumentException.ThrowIfNullOrWhiteSpace(loadedStatusText);

        var selectedProviderKey = GetSelectedItem()?.ProviderKey;

        _providers.Clear();
        _providers.AddRange(definitions.Select(CreateEditorItem));
        _providerList.Items.Clear();
        foreach (var provider in _providers)
        {
            _providerList.Items.Add(provider);
        }

        _dirty = false;
        _statusText = _providers.Count == 0 ? emptyStatusText : loadedStatusText;

        var selectedIndex = selectedProviderKey is null
            ? (_providers.Count == 0 ? -1 : 0)
            : _providers.FindIndex(item => string.Equals(item.ProviderKey, selectedProviderKey, StringComparison.OrdinalIgnoreCase));
        if (selectedIndex < 0 && _providers.Count > 0)
        {
            selectedIndex = 0;
        }

        SelectProvider(selectedIndex);
    }

    private bool TryBeginDialogOperation(string operationDescription)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationDescription);

        if (_activeOperationCount > 0)
        {
            _statusText = $"[warning]Please wait for the current provider operation to complete before trying to {AnsiMarkup.Escape(operationDescription)}.[/]";
            return false;
        }

        _activeOperationCount++;
        return true;
    }

    private void EndDialogOperation()
    {
        if (_activeOperationCount > 0)
        {
            _activeOperationCount--;
        }
    }

    private static ModelProviderEditorItemViewModel.IBindings GetBindings(ModelProviderEditorItemViewModel item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return (ModelProviderEditorItemViewModel.IBindings)item;
    }

    private readonly record struct ProviderTypeOption(string Id, string DisplayName)
    {
        public override string ToString() => DisplayName;
    }

    private readonly record struct ReasoningOption(string? Value, string DisplayName)
    {
        public override string ToString() => DisplayName;
    }


    private static Visual BuildProviderListItem(ModelProviderEditorItemViewModel item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return new Markup(() => BuildProviderListItemMarkup(item))
        {
            Wrap = false,
        };
    }

    private static string BuildProviderListItemMarkup(ModelProviderEditorItemViewModel item)
    {
        var icon = item.Enabled ? $"{NerdFont.MdCheckCircleOutline}" : $"{NerdFont.MdPauseCircleOutline}";
        var tone = item.Enabled ? "success" : "muted";
        return $"[{tone}]{icon} {AnsiMarkup.Escape(item.Label)}[/]";
    }
}
