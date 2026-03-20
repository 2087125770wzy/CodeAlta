using CodeAlta.Threading;
using CodeAlta.Models;
using CodeAlta.Presentation.Chat;
using CodeAlta.Views;
using XenoAtom.Terminal.UI.Controls;

namespace CodeAlta.App.Context;

internal sealed class ChatSelectorUiContext
{
    private readonly Func<Select<ChatBackendOption>?> _getChatBackendSelect;
    private readonly Func<Select<ChatModelOption>?> _getChatModelSelect;
    private readonly Func<Select<ChatReasoningOption>?> _getChatReasoningSelect;
    private readonly Func<CheckBox?> _getChatAutoScrollCheckBox;
    private readonly Func<ChatPromptEditor?> _getThreadInput;
    private readonly Func<IUiDispatcher> _getUiDispatcher;
    private readonly Action _verifyBindableAccess;

    public ChatSelectorUiContext(
        Func<Select<ChatBackendOption>?> getChatBackendSelect,
        Func<Select<ChatModelOption>?> getChatModelSelect,
        Func<Select<ChatReasoningOption>?> getChatReasoningSelect,
        Func<CheckBox?> getChatAutoScrollCheckBox,
        Func<ChatPromptEditor?> getThreadInput,
        Func<IUiDispatcher> getUiDispatcher,
        Action verifyBindableAccess)
    {
        ArgumentNullException.ThrowIfNull(getChatBackendSelect);
        ArgumentNullException.ThrowIfNull(getChatModelSelect);
        ArgumentNullException.ThrowIfNull(getChatReasoningSelect);
        ArgumentNullException.ThrowIfNull(getChatAutoScrollCheckBox);
        ArgumentNullException.ThrowIfNull(getThreadInput);
        ArgumentNullException.ThrowIfNull(getUiDispatcher);
        ArgumentNullException.ThrowIfNull(verifyBindableAccess);

        _getChatBackendSelect = getChatBackendSelect;
        _getChatModelSelect = getChatModelSelect;
        _getChatReasoningSelect = getChatReasoningSelect;
        _getChatAutoScrollCheckBox = getChatAutoScrollCheckBox;
        _getThreadInput = getThreadInput;
        _getUiDispatcher = getUiDispatcher;
        _verifyBindableAccess = verifyBindableAccess;
    }

    public Select<ChatBackendOption>? GetChatBackendSelect()
        => _getChatBackendSelect();

    public Select<ChatModelOption>? GetChatModelSelect()
        => _getChatModelSelect();

    public Select<ChatReasoningOption>? GetChatReasoningSelect()
        => _getChatReasoningSelect();

    public CheckBox? GetChatAutoScrollCheckBox()
        => _getChatAutoScrollCheckBox();

    public ChatPromptEditor? GetThreadInput()
        => _getThreadInput();

    public IUiDispatcher GetUiDispatcher()
        => _getUiDispatcher();

    public void VerifyBindableAccess()
        => _verifyBindableAccess();
}
