using XenoAtom.Terminal.UI;

namespace CodeAlta.Views;

internal sealed record ThreadWorkspaceViewActions(
    Func<Visual> BuildSessionUsageIndicatorVisual,
    Action OpenSessionUsagePopup,
    Action<Visual> ToggleThreadInfoPopup,
    Action OpenHelp,
    Action OpenCommandPalette,
    Action OpenModelProviders,
    Action<string> AcceptPrompt,
    Action SendPrompt,
    Action SteerPrompt,
    Action ClearQueuedPrompts,
    Action<string> ConvertQueuedPromptToSteer,
    Action<string> DeletePendingSteer,
    Action<string> DeleteQueuedPrompt,
    Action<string, int> UpdateQueuedPromptCount,
    Action<string, string> UpdateQueuedPromptText,
    Action AbortThread,
    Action CompactThread,
    Action CloseTab,
    Action<int> OnChatBackendSelectionChanged,
    Action<int> OnChatModelSelectionChanged,
    Action<int> OnChatReasoningSelectionChanged,
    Action<int> OnSelectedTabChanged)
{
    public static ThreadWorkspaceViewActions Empty { get; } = new(
        static () => new XenoAtom.Terminal.UI.Controls.TextBlock(string.Empty),
        static () => { },
        static _ => { },
        static () => { },
        static () => { },
        static () => { },
        static _ => { },
        static () => { },
        static () => { },
        static () => { },
        static _ => { },
        static _ => { },
        static _ => { },
        static (_, _) => { },
        static (_, _) => { },
        static () => { },
        static () => { },
        static () => { },
        static _ => { },
        static _ => { },
        static _ => { },
        static _ => { });
}
