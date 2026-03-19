using CodeAlta.Agent;

namespace CodeAlta.Tests;

[TestClass]
public sealed class CodeAltaAppStatusTests
{
    [TestMethod]
    public void ResolveSelectionStatus_PrefersThreadSpecificStatus()
    {
        var snapshot = CodeAltaApp.ResolveSelectionStatus(
            readyMessage: "Prompt ready",
            hasThreadStatus: true,
            threadStatusMessage: "Thinking...",
            threadStatusBusy: true,
            threadStatusTone: CodeAltaApp.StatusTone.Info,
            promptUnavailable: true,
            promptUnavailableMessage: "Codex is unavailable.",
            promptUnavailableTone: CodeAltaApp.StatusTone.Warning);

        Assert.AreEqual("Thinking...", snapshot.Message);
        Assert.IsTrue(snapshot.Busy);
        Assert.AreEqual(CodeAltaApp.StatusTone.Info, snapshot.Tone);
    }

    [TestMethod]
    public void ResolveSelectionStatus_FallsBackToPromptUnavailableWhenThreadHasNoCustomStatus()
    {
        var snapshot = CodeAltaApp.ResolveSelectionStatus(
            readyMessage: "Prompt ready",
            hasThreadStatus: false,
            threadStatusMessage: "Stopped",
            threadStatusBusy: false,
            threadStatusTone: CodeAltaApp.StatusTone.Warning,
            promptUnavailable: true,
            promptUnavailableMessage: "Codex is unavailable.",
            promptUnavailableTone: CodeAltaApp.StatusTone.Warning);

        Assert.AreEqual("Codex is unavailable.", snapshot.Message);
        Assert.IsFalse(snapshot.Busy);
        Assert.AreEqual(CodeAltaApp.StatusTone.Warning, snapshot.Tone);
    }

    [TestMethod]
    public void ResolveSelectionStatus_UsesReadyMessageWhenNothingOverridesIt()
    {
        var snapshot = CodeAltaApp.ResolveSelectionStatus(
            readyMessage: "Prompt ready",
            hasThreadStatus: false,
            threadStatusMessage: null,
            threadStatusBusy: false,
            threadStatusTone: CodeAltaApp.StatusTone.Info,
            promptUnavailable: false,
            promptUnavailableMessage: null,
            promptUnavailableTone: CodeAltaApp.StatusTone.Warning);

        Assert.AreEqual("Prompt ready", snapshot.Message);
        Assert.IsFalse(snapshot.Busy);
        Assert.AreEqual(CodeAltaApp.StatusTone.Ready, snapshot.Tone);
    }

    [TestMethod]
    public void BuildStatusTextStyle_UsesGradientBrushForThinking()
    {
        var style = StatusVisualFormatter.BuildStatusTextStyle(
            StatusVisualFormatter.BuildThinkingStatusText(),
            busy: true,
            CodeAltaApp.StatusTone.Info);

        Assert.IsNotNull(style.ForegroundBrush);
        Assert.IsNull(style.Foreground);
    }

    [TestMethod]
    public void BuildStatusTextStyle_UsesSolidToneColorWhenIdle()
    {
        var style = StatusVisualFormatter.BuildStatusTextStyle(
            "Prompt ready",
            busy: false,
            CodeAltaApp.StatusTone.Ready);

        Assert.IsNull(style.ForegroundBrush);
        Assert.IsNotNull(style.Foreground);
    }
}
