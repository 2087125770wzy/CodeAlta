using System.Text;
using CodeAlta.App;
using CodeAlta.Catalog.Skills;
using XenoAtom.Ansi;
using XenoAtom.Terminal;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Commands;
using XenoAtom.Terminal.UI.Controls;
using XenoAtom.Terminal.UI.Geometry;
using XenoAtom.Terminal.UI.Input;
using XenoAtom.Terminal.UI.Styling;
using XenoAtom.Terminal.UI.Text;

namespace CodeAlta.Views;

internal sealed class SkillsManagementDialog
{
    private readonly SkillsManagementService _service;
    private readonly Func<string, Task> _openFileAsync;
    private readonly Func<string, Task> _activateSkillAsync;
    private readonly Func<Rectangle?> _getBounds;
    private readonly Func<Visual?> _getFocusTarget;
    private readonly Dialog _dialog;
    private readonly EnumSelect<SkillsManagementScope> _scopeSelect;
    private readonly TextBox _filterBox;
    private readonly Select<SkillRow> _skillSelect;
    private readonly Select<SkillRelatedFileRow> _relatedFileSelect;
    private readonly Markup _summaryMarkup;
    private readonly TextBlock _detailText;
    private IReadOnlyList<SkillRow> _allRows = [];
    private IReadOnlyList<SkillRow> _rows = [];
    private string _summaryText = "Loading skills...";

    public SkillsManagementDialog(
        SkillsManagementService service,
        Func<string, Task> openFileAsync,
        Func<string, Task> activateSkillAsync,
        Func<Rectangle?> getBounds,
        Func<Visual?> getFocusTarget)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(openFileAsync);
        ArgumentNullException.ThrowIfNull(activateSkillAsync);
        ArgumentNullException.ThrowIfNull(getBounds);
        ArgumentNullException.ThrowIfNull(getFocusTarget);

        _service = service;
        _openFileAsync = openFileAsync;
        _activateSkillAsync = activateSkillAsync;
        _getBounds = getBounds;
        _getFocusTarget = getFocusTarget;

        var closeButton = new Button(new TextBlock($"{NerdFont.MdClose} Close"))
        {
            HorizontalAlignment = Align.End,
            VerticalAlignment = Align.Start,
        };
        closeButton.Click(Close);

        _scopeSelect = new EnumSelect<SkillsManagementScope>()
            .Value(SkillsManagementScope.Combined)
            .MinWidth(18);
        _scopeSelect.SelectionChanged((_, _) => _ = ReloadAsync());

        _filterBox = new TextBox()
            .Placeholder("Filter by name, description, source, or path")
            .HorizontalAlignment(Align.Stretch);
        _filterBox.TextDocument.Changed += OnFilterTextChanged;

        _skillSelect = new Select<SkillRow>()
            .HorizontalAlignment(Align.Stretch)
            .VerticalAlignment(Align.Stretch)
            .MinWidth(38);
        _skillSelect.SelectionChanged((_, _) => UpdateDetail());

        _relatedFileSelect = new Select<SkillRelatedFileRow>()
            .HorizontalAlignment(Align.Stretch)
            .VerticalAlignment(Align.Stretch)
            .MinHeight(4);

        _summaryMarkup = new Markup(() => _summaryText)
        {
            Wrap = true,
        };

        _detailText = new TextBlock(BuildDetailText)
        {
            Wrap = true,
            HorizontalAlignment = Align.Stretch,
            VerticalAlignment = Align.Stretch,
        };

        var refreshButton = new Button("Refresh")
            .Tone(ControlTone.Primary)
            .Click(() => _ = ReloadAsync());
        var newSkillButton = new Button("New skill")
            .Tone(ControlTone.Primary)
            .Click(ShowNewSkillDialog);
        var activateButton = new Button("Activate")
            .Tone(ControlTone.Success)
            .Click(() => _ = ActivateSelectedSkillAsync());
        var openSkillButton = new Button("Open SKILL.md")
            .Click(() => _ = OpenSelectedSkillAsync());
        var openRelatedButton = new Button("Open related")
            .Click(() => _ = OpenSelectedRelatedFileAsync());

        var toolbar = new Grid
            {
                HorizontalAlignment = Align.Stretch,
            }
            .Rows(new RowDefinition { Height = GridLength.Auto })
            .Columns(
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star(1) },
                new ColumnDefinition { Width = GridLength.Star(1) });
        toolbar.Cell(new TextBlock("Scope") { VerticalAlignment = Align.Center }, 0, 0);
        toolbar.Cell(_scopeSelect, 0, 1);
        toolbar.Cell(new TextBlock("Filter") { VerticalAlignment = Align.Center }, 0, 2);
        toolbar.Cell(_filterBox, 0, 3);
        toolbar.Cell(
            new HStack(newSkillButton, activateButton, openSkillButton, openRelatedButton, refreshButton)
            {
                HorizontalAlignment = Align.End,
                Spacing = 1,
            },
            0,
            4);

        var introText = new Markup("[dim]Browse discovered filesystem skills, validation state, source precedence, and provenance. New skill creates a template in project .alta/skills when a project is selected, otherwise user .alta/skills; Open edits SKILL.md or related scripts/references/assets.[/]")
        {
            Wrap = true,
        };

        var rightPane = new Grid
            {
                HorizontalAlignment = Align.Stretch,
                VerticalAlignment = Align.Stretch,
            }
            .Rows(
                new RowDefinition { Height = GridLength.Star(3) },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Star(1) })
            .Columns(new ColumnDefinition { Width = GridLength.Star(1) });
        rightPane.Cell(
            new Border(new ScrollViewer(_detailText).Stretch())
                .Style(BorderStyle.Rounded)
                .Padding(new Thickness(1, 0, 1, 0))
                .HorizontalAlignment(Align.Stretch)
                .VerticalAlignment(Align.Stretch),
            0,
            0);
        rightPane.Cell(new Markup("[dim]Related files (scripts, references, assets)[/]") { Wrap = false }, 1, 0);
        rightPane.Cell(
            new Border(new ScrollViewer(_relatedFileSelect.Stretch()).Stretch())
                .Style(BorderStyle.Rounded)
                .Padding(new Thickness(1, 0, 1, 0))
                .HorizontalAlignment(Align.Stretch)
                .VerticalAlignment(Align.Stretch),
            2,
            0);

        var contentGrid = new Grid
            {
                HorizontalAlignment = Align.Stretch,
                VerticalAlignment = Align.Stretch,
            }
            .Rows(
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Star(1) })
            .Columns(
                new ColumnDefinition { Width = GridLength.Star(2) },
                new ColumnDefinition { Width = GridLength.Star(3) });

        contentGrid.Cell(toolbar, 0, 0, columnSpan: 2);
        contentGrid.Cell(introText, 1, 0, columnSpan: 2);
        contentGrid.Cell(_summaryMarkup, 2, 0, columnSpan: 2);
        contentGrid.Cell(
            new Border(new ScrollViewer(_skillSelect.Stretch()).Stretch())
                .Style(BorderStyle.Rounded)
                .Padding(new Thickness(1, 0, 1, 0))
                .HorizontalAlignment(Align.Stretch)
                .VerticalAlignment(Align.Stretch),
            3,
            0);
        contentGrid.Cell(rightPane, 3, 1);

        _dialog = new Dialog()
            .Title("Skills")
            .TopRightText(closeButton)
            .BottomRightText(new Markup("[dim]Esc Close[/]"))
            .IsModal(true)
            .Padding(1)
            .Content(contentGrid);
        ResponsiveDialogSize.Apply(_dialog, getBounds(), minWidth: 104, minHeight: 24, widthFactor: 0.86, heightFactor: 0.76);
        _dialog.AddCommand(new Command
        {
            Id = "CodeAlta.Skills.Manage.Close",
            LabelMarkup = "Close",
            DescriptionMarkup = "Close the skills browser.",
            Gesture = new KeyGesture(TerminalKey.Escape),
            Importance = CommandImportance.Primary,
            Execute = _ => Close(),
        });
    }

    public void Show()
    {
        _dialog.Show();
        _ = ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        _summaryText = "[primary]Loading skills...[/]";
        try
        {
            var descriptors = await _service.LoadAsync(_scopeSelect.Value);
            _allRows = descriptors
                .OrderBy(static descriptor => descriptor.Precedence)
                .ThenBy(static descriptor => descriptor.Name, StringComparer.OrdinalIgnoreCase)
                .Select(static descriptor => new SkillRow(descriptor))
                .ToArray();

            ApplyFilter(selectFirst: true);
        }
        catch (Exception ex)
        {
            _allRows = [];
            _rows = [];
            _skillSelect.Items.Clear();
            _summaryText = $"[error]Failed to load skills: {AnsiMarkup.Escape(ex.Message)}[/]";
            UpdateDetail();
        }
    }

    private void OnFilterTextChanged(object? sender, TextDocumentChangedEventArgs e)
    {
        _ = sender;
        _ = e;
        ApplyFilter(selectFirst: true);
    }

    private void ApplyFilter(bool selectFirst)
    {
        var filterText = (_filterBox.Text ?? string.Empty).Trim();
        _rows = string.IsNullOrWhiteSpace(filterText)
            ? _allRows
            : _allRows
                .Where(row => row.Matches(filterText))
                .ToArray();

        _skillSelect.Items.Clear();
        foreach (var row in _rows)
        {
            _skillSelect.Items.Add(row);
        }

        if (selectFirst)
        {
            _skillSelect.SelectedIndex = _rows.Count > 0 ? 0 : -1;
        }
        else if (_skillSelect.SelectedIndex >= _rows.Count)
        {
            _skillSelect.SelectedIndex = _rows.Count > 0 ? _rows.Count - 1 : -1;
        }

        _summaryText = BuildSummaryMarkup(_allRows.Select(static row => row.Descriptor).ToArray(), _rows.Count, filterText);
        UpdateDetail();
    }

    private void UpdateDetail()
    {
        _relatedFileSelect.Items.Clear();
        if (GetSelectedDescriptor() is not { } descriptor)
        {
            return;
        }

        foreach (var file in SkillsManagementService.ListRelatedFiles(descriptor))
        {
            _relatedFileSelect.Items.Add(new SkillRelatedFileRow(file));
        }

        _relatedFileSelect.SelectedIndex = _relatedFileSelect.Items.Count > 0 ? 0 : -1;
    }

    private async Task OpenSelectedSkillAsync()
    {
        if (GetSelectedDescriptor() is not { } descriptor)
        {
            return;
        }

        await _openFileAsync(descriptor.SkillFilePath);
    }

    private async Task OpenSelectedRelatedFileAsync()
    {
        var index = _relatedFileSelect.SelectedIndex;
        if ((uint)index >= (uint)_relatedFileSelect.Items.Count)
        {
            _summaryText = "[warning]Select a related file before opening.[/]";
            return;
        }

        await _openFileAsync(_relatedFileSelect.Items[index].File.FullPath);
    }

    private void ShowNewSkillDialog()
    {
        var nameBox = new TextBox()
            .Placeholder("lowercase-name");
        var descriptionBox = new TextBox()
            .Placeholder("Describe when this skill should be used")
            .HorizontalAlignment(Align.Stretch);
        var validationText = new TextBlock(string.Empty)
        {
            Wrap = true,
        }.Style(TextBlockStyle.Default with { Foreground = Colors.OrangeRed });

        var form = new Grid
            {
                HorizontalAlignment = Align.Stretch,
                VerticalAlignment = Align.Stretch,
            }
            .Rows(
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto })
            .Columns(
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star(1) });
        form.Cell(new TextBlock("Name"), 0, 0);
        form.Cell(nameBox.Stretch(), 0, 1);
        form.Cell(new TextBlock("Description"), 1, 0);
        form.Cell(descriptionBox.Stretch(), 1, 1);
        form.Cell(validationText, 2, 0, columnSpan: 2);

        Dialog? createDialog = null;
        var createButton = new Button("Create")
            .Tone(ControlTone.Success)
            .Click(() => _ = CreateSkillFromDialogAsync(createDialog, nameBox, descriptionBox, validationText));
        var cancelButton = new Button("Cancel")
            .Click(() => createDialog?.Close());
        var content = new VStack(
            new Markup(BuildNewSkillTargetHint()) { Wrap = true },
            form,
            new HStack(cancelButton, createButton)
            {
                HorizontalAlignment = Align.End,
                Spacing = 2,
            })
        {
            HorizontalAlignment = Align.Stretch,
            VerticalAlignment = Align.Stretch,
            Spacing = 1,
        };

        createDialog = new Dialog()
            .Title("New Skill")
            .BottomRightText(new Markup("[dim]Esc Cancel[/]"))
            .IsModal(true)
            .Padding(1)
            .Content(content);
        ResponsiveDialogSize.Apply(createDialog, _getBounds(), minWidth: 76, minHeight: 12, widthFactor: 0.48, heightFactor: 0.32);
        createDialog.AddCommand(new Command
        {
            Id = "CodeAlta.Skills.New.Close",
            LabelMarkup = "Cancel",
            DescriptionMarkup = "Close the new skill dialog.",
            Gesture = new KeyGesture(TerminalKey.Escape),
            Importance = CommandImportance.Primary,
            Execute = _ => createDialog.Close(),
        });
        createDialog.Show();
        createDialog.App?.Focus(nameBox);
    }

    private async Task CreateSkillFromDialogAsync(
        Dialog? createDialog,
        TextBox nameBox,
        TextBox descriptionBox,
        TextBlock validationText)
    {
        validationText.Text = string.Empty;
        try
        {
            var result = await _service.CreateSkillAsync(_scopeSelect.Value, nameBox.Text, descriptionBox.Text);
            createDialog?.Close();
            await ReloadAsync();
            _summaryText = $"[success]Created skill '{AnsiMarkup.Escape(result.Name)}' at {AnsiMarkup.Escape(result.SkillRootPath)}.[/]";
            await _openFileAsync(result.SkillFilePath);
        }
        catch (Exception ex)
        {
            validationText.Text = ex.Message;
        }
    }

    private string BuildNewSkillTargetHint()
    {
        var target = _scopeSelect.Value == SkillsManagementScope.User
            ? "user CodeAlta skills (`~/.alta/skills/`)"
            : "project CodeAlta skills (`<project>/.alta/skills/`) when a project is selected, otherwise user CodeAlta skills";
        return $"[dim]Creates a scaffolded Agent Skills-compatible `SKILL.md` plus `scripts/`, `references/`, and `assets/` folders under {target}.[/]";
    }

    private async Task ActivateSelectedSkillAsync()
    {
        if (GetSelectedDescriptor() is not { } descriptor)
        {
            return;
        }

        if (!descriptor.IsValid || descriptor.IsShadowed)
        {
            _summaryText = "[warning]Select a valid, unshadowed skill before activating.[/]";
            return;
        }

        try
        {
            _summaryText = $"[primary]Activating skill '{AnsiMarkup.Escape(descriptor.Name)}'...[/]";
            await _activateSkillAsync(descriptor.Name);
            _summaryText = $"[success]Activation requested for skill '{AnsiMarkup.Escape(descriptor.Name)}'.[/]";
        }
        catch (Exception ex)
        {
            _summaryText = $"[error]Failed to activate skill '{AnsiMarkup.Escape(descriptor.Name)}': {AnsiMarkup.Escape(ex.Message)}[/]";
        }
    }

    private SkillDescriptor? GetSelectedDescriptor()
    {
        var index = _skillSelect.SelectedIndex;
        return (uint)index < (uint)_rows.Count
            ? _rows[index].Descriptor
            : null;
    }

    private string BuildDetailText()
    {
        var descriptor = GetSelectedDescriptor();
        if (descriptor is null)
        {
            return "No skills were discovered for the selected scope.";
        }

        var builder = new StringBuilder();
        builder.AppendLine($"{descriptor.Name}");
        builder.AppendLine(new string('-', descriptor.Name.Length));
        builder.AppendLine(descriptor.Description);
        builder.AppendLine();
        builder.AppendLine($"Status: {FormatStatus(descriptor)}");
        builder.AppendLine($"Source: {FormatSource(descriptor.SourceKind)}");
        builder.AppendLine($"Scope: {descriptor.Scope}");
        builder.AppendLine($"Trusted for model advertisement: {(descriptor.IsTrusted ? "yes" : "no")}");
        builder.AppendLine($"Model visible: {(descriptor.IsModelVisible ? "yes" : "no")}");
        if (descriptor.IsShadowed)
        {
            builder.AppendLine($"Shadowed by: {descriptor.ShadowedBySkillFilePath}");
        }

        builder.AppendLine();
        builder.AppendLine($"SKILL.md: {descriptor.SkillFilePath}");
        builder.AppendLine($"Root: {descriptor.SkillRootPath}");
        builder.AppendLine($"Source id: {descriptor.SourceId}");

        var relatedFiles = SkillsManagementService.ListRelatedFiles(descriptor);
        builder.AppendLine($"Related files: {relatedFiles.Count}");

        if (descriptor.Diagnostics.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("Diagnostics:");
            foreach (var diagnostic in descriptor.Diagnostics)
            {
                builder
                    .Append("  - ")
                    .Append(diagnostic.Severity)
                    .Append(" ")
                    .Append(diagnostic.Code)
                    .Append(": ")
                    .AppendLine(diagnostic.Message);
            }
        }

        return builder.ToString();
    }

    private static string BuildSummaryMarkup(
        IReadOnlyList<SkillDescriptor> descriptors,
        int shownCount,
        string filterText)
    {
        var valid = descriptors.Count(static descriptor => descriptor.IsValid);
        var invalid = descriptors.Count(static descriptor => !descriptor.IsValid);
        var shadowed = descriptors.Count(static descriptor => descriptor.IsShadowed);
        var visible = descriptors.Count(static descriptor => descriptor.IsModelVisible);
        var filterSuffix = string.IsNullOrWhiteSpace(filterText)
            ? string.Empty
            : $"   [muted]{shownCount} shown for '{AnsiMarkup.Escape(filterText)}'[/]";
        return $"[primary]{descriptors.Count} discovered[/]   [success]{valid} valid[/]   [warning]{invalid} invalid[/]   [muted]{shadowed} shadowed[/]   [accent]{visible} model-visible[/]{filterSuffix}";
    }

    private static string FormatStatus(SkillDescriptor descriptor)
    {
        if (descriptor.IsShadowed)
        {
            return "shadowed";
        }

        return descriptor.IsValid ? "valid" : "invalid";
    }

    private static string FormatSource(SkillSourceKind sourceKind)
    {
        return sourceKind switch
        {
            SkillSourceKind.ProjectAlta => "project .alta/skills",
            SkillSourceKind.ProjectCommon => "project .agents/skills",
            SkillSourceKind.UserAlta => "user ~/.alta/skills",
            SkillSourceKind.UserCommon => "user ~/.agents/skills",
            SkillSourceKind.Plugin => "plugin",
            SkillSourceKind.Builtin => "builtin",
            _ => "temporary",
        };
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

    private sealed record SkillRow(SkillDescriptor Descriptor)
    {
        public bool Matches(string filterText)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filterText);

            return Descriptor.Name.Contains(filterText, StringComparison.OrdinalIgnoreCase) ||
                   Descriptor.Description.Contains(filterText, StringComparison.OrdinalIgnoreCase) ||
                   Descriptor.SkillFilePath.Contains(filterText, StringComparison.OrdinalIgnoreCase) ||
                   Descriptor.SkillRootPath.Contains(filterText, StringComparison.OrdinalIgnoreCase) ||
                   Descriptor.SourceKind.ToString().Contains(filterText, StringComparison.OrdinalIgnoreCase) ||
                   Descriptor.Scope.ToString().Contains(filterText, StringComparison.OrdinalIgnoreCase);
        }

        public override string ToString()
        {
            var status = Descriptor.IsShadowed
                ? "shadowed"
                : Descriptor.IsValid ? "valid" : "invalid";
            return $"{Descriptor.Name} · {status} · {FormatSource(Descriptor.SourceKind)}";
        }
    }

    private sealed record SkillRelatedFileRow(SkillRelatedFile File)
    {
        public override string ToString()
        {
            var prefix = File.Category + "/";
            var displayPath = File.RelativePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                ? File.RelativePath[prefix.Length..]
                : File.RelativePath;
            return $"{File.Category}/{displayPath}";
        }
    }
}
