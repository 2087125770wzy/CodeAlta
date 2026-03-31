using CodeAlta.Frontend.Commands;

namespace CodeAlta.Tests;

[TestClass]
public sealed class ShellInputRouterTests
{
    private readonly ShellInputRouter _router = new();

    [TestMethod]
    public void Route_PlainPrompt_ReturnsSendPromptIntent()
    {
        var intent = _router.Route("Investigate startup regression", steerRequested: false);

        Assert.IsInstanceOfType<SendPromptIntent>(intent);
        Assert.AreEqual("Investigate startup regression", ((SendPromptIntent)intent).PromptText);
    }

    [TestMethod]
    public void Route_BlankSteer_ReturnsSteerIntent()
    {
        var intent = _router.Route("   ", steerRequested: true);

        Assert.IsInstanceOfType<SteerPromptIntent>(intent);
        Assert.AreEqual(string.Empty, ((SteerPromptIntent)intent).PromptText);
    }

    [TestMethod]
    public void Route_QuestionMark_OpensHelp()
    {
        var intent = _router.Route("?", steerRequested: false);

        Assert.IsInstanceOfType<OpenHelpIntent>(intent);
    }

    [TestMethod]
    public void Route_TextCommands_ReturnTypedIntents()
    {
        Assert.IsInstanceOfType<OpenHelpIntent>(_router.Route("/help", steerRequested: false));
        Assert.IsInstanceOfType<AbortThreadIntent>(_router.Route("/abort", steerRequested: false));
        Assert.IsInstanceOfType<CompactThreadIntent>(_router.Route("/compact", steerRequested: false));
        Assert.IsInstanceOfType<CloseTabIntent>(_router.Route("/close", steerRequested: false));
        Assert.IsInstanceOfType<QueueStatusIntent>(_router.Route("/queue", steerRequested: false));
    }

    [TestMethod]
    public void Route_DelegateCommand_UsesRemainingTextAsPrompt()
    {
        var intent = _router.Route("/delegate review the test failures", steerRequested: false);

        Assert.IsInstanceOfType<DelegateThreadIntent>(intent);
        Assert.AreEqual("review the test failures", ((DelegateThreadIntent)intent).PromptText);
    }
}
