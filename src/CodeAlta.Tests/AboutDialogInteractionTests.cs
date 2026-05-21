using System.Reflection;
using CodeAlta.Views;
using XenoAtom.Terminal;
using XenoAtom.Terminal.Backends;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Controls;
using XenoAtom.Terminal.UI.Geometry;
using XenoAtom.Terminal.UI.Hosting;
using XenoAtom.Terminal.UI.Input;

namespace CodeAlta.Tests;

[TestClass]
public sealed class AboutDialogInteractionTests
{
    [TestMethod]
    public void AboutDialog_EscapeClosesDialog()
    {
        using var session = Terminal.Open(new InMemoryTerminalBackend(new TerminalSize(120, 40)), new TerminalOptions { ImplicitStartInput = true }, force: true);
        var root = new Button("Root");
        var app = new TerminalApp(
            root,
            session.Instance,
            new TerminalAppOptions
            {
                HostKind = TerminalHostKind.Fullscreen,
            });
        var dialog = new AboutDialog(
            () => new Rectangle(0, 0, 120, 40),
            () => root,
            new State<float>(0f));

        InvokeTerminalApp(app, "BeginRun");
        try
        {
            app.Focus(root);
            dialog.Show();
            TickTerminalApp(app);

            Assert.IsTrue(IsDialogOpen(dialog));

            var backend = (InMemoryTerminalBackend)session.Instance.Backend;
            backend.PushEvent(new TerminalKeyEvent { Key = TerminalKey.Escape });
            TickTerminalApp(app);

            Assert.IsFalse(IsDialogOpen(dialog), "The about dialog should close when Escape is pressed.");
        }
        finally
        {
            InvokeTerminalApp(app, "EndRun");
        }
    }

    [TestMethod]
    public void AboutDialog_HasCloseButton()
    {
        var dialog = new AboutDialog(
            () => new Rectangle(0, 0, 120, 40),
            () => null,
            new State<float>(0f));

        Assert.IsInstanceOfType<Button>(GetDialog(dialog).TopRightText);
    }

    private static bool IsDialogOpen(AboutDialog dialog)
        => GetDialog(dialog).App is not null;

    private static Dialog GetDialog(AboutDialog dialog)
        => (Dialog)typeof(AboutDialog)
            .GetField("_dialog", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(dialog)!;

    private static void TickTerminalApp(TerminalApp app)
        => typeof(TerminalApp).GetMethod("Tick", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(app, [null]);

    private static void InvokeTerminalApp(TerminalApp app, string methodName)
        => typeof(TerminalApp).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(app, null);
}
