using XenoAtom.Terminal.UI.Input;

namespace CodeAlta.Frontend.Commands;

internal enum ShellCommandScope
{
    AnyShell,
    DraftOrThread,
    ThreadOnly,
}

internal enum ShellCommandAvailability
{
    Always,
    PromptEnabled,
    CanSend,
    CanSteer,
    CanDelegate,
    CanAbort,
    CanClearQueue,
    CanCompact,
    CanCloseTab,
    CanShowThreadInfo,
}

internal enum ShellCommandHelpCategory
{
    General,
    Prompt,
    Thread,
    Inspection,
}

internal sealed record ShellCommandMetadata(
    string Id,
    string Label,
    string Description,
    ShellCommandHelpCategory HelpCategory,
    ShellCommandScope Scope,
    ShellCommandAvailability Availability,
    KeyGesture? Gesture = null,
    KeySequence? Sequence = null,
    IReadOnlyList<string>? Aliases = null,
    bool ShowInCommandBar = true,
    bool ShowInHelp = true)
{
    public IReadOnlyList<string> Aliases { get; } = Aliases ?? [];
}
