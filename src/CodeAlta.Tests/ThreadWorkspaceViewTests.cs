using System.Reflection;
using CodeAlta.Models;
using CodeAlta.Presentation.Chat;
using CodeAlta.ViewModels;
using CodeAlta.Views;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Controls;

namespace CodeAlta.Tests;

[TestClass]
public sealed class ThreadWorkspaceViewTests
{
    [TestMethod]
    public void SyncChatSelectorItems_ReplacesSelectItems()
    {
        var shellViewModel = new CodeAltaShellViewModel();
        var workspaceViewModel = new ThreadWorkspaceViewModel
        {
            BackendOptions = [new ChatBackendOption(new("codex"), "Codex")],
            ModelOptions = [new ChatModelOption("gpt-5", "GPT-5")],
            ReasoningOptions = [new ChatReasoningOption(Agent.AgentReasoningEffort.High, "High")],
        };
        var promptComposerViewModel = new PromptComposerViewModel();
        var view = new ThreadWorkspaceView(
            shellViewModel,
            workspaceViewModel,
            promptComposerViewModel,
            [],
            static () => new TextBlock(string.Empty),
            static () => { },
            static _ => { },
            static () => { },
            static () => { },
            static _ => { },
            static () => { },
            static () => { },
            static () => { },
            static _ => { },
            static _ => { },
            static (_, _) => { },
            static (_, _) => { },
            static () => { },
            static () => { },
            static () => { },
            static () => { },
            static _ => { },
            static _ => { },
            static _ => { },
            static _ => { },
            new State<string?>(string.Empty),
            new State<float>(0),
            static () => { });

        view.SyncChatSelectorItems(workspaceViewModel);

        var backendSelect = GetPrivateField<Select<ChatBackendOption>>(view, "ChatBackendSelect");
        var modelSelect = GetPrivateField<Select<ChatModelOption>>(view, "ChatModelSelect");
        var reasoningSelect = GetPrivateField<Select<ChatReasoningOption>>(view, "ChatReasoningSelect");

        Assert.AreEqual(1, backendSelect.Items.Count);
        Assert.AreEqual("Codex", backendSelect.Items[0].Label);
        Assert.AreEqual(1, modelSelect.Items.Count);
        Assert.AreEqual("GPT-5", modelSelect.Items[0].Label);
        Assert.AreEqual(1, reasoningSelect.Items.Count);
        Assert.AreEqual("High", reasoningSelect.Items[0].Label);

        workspaceViewModel.BackendOptions =
        [
            new ChatBackendOption(new("codex"), "Codex"),
            new ChatBackendOption(new("copilot"), "Copilot"),
        ];
        workspaceViewModel.ModelOptions = [new ChatModelOption("gpt-5.1", "GPT-5.1")];
        workspaceViewModel.ReasoningOptions = [new ChatReasoningOption(Agent.AgentReasoningEffort.Low, "Low")];

        view.SyncChatSelectorItems(workspaceViewModel);

        Assert.AreEqual(2, backendSelect.Items.Count);
        Assert.AreEqual("Copilot", backendSelect.Items[1].Label);
        Assert.AreEqual("GPT-5.1", modelSelect.Items[0].Label);
        Assert.AreEqual("Low", reasoningSelect.Items[0].Label);
    }

    private static T GetPrivateField<T>(object instance, string fieldName)
        where T : class
    {
        var property = instance.GetType().GetProperty(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        if (property is not null)
        {
            return Assert.IsInstanceOfType<T>(property.GetValue(instance));
        }

        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field);
        return Assert.IsInstanceOfType<T>(field.GetValue(instance));
    }
}
