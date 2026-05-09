using CodeAlta.Plugins.Abstractions;
using XenoAtom.Terminal.UI.Controls;

[Plugin("ui-all-regions", DisplayName = "UI All Regions", Description = "Contributes sample UI hooks for every PluginUiRegion.")]
public sealed class UiAllRegionsPlugin : PluginBase
{
    public override IEnumerable<PluginCommandContribution> GetCommands()
    {
        yield return Command.Prompt("ui-sample", "Show that the all-regions UI sample is active.", static async (context, cancellationToken) =>
        {
            if (context.Ui.HasInteractiveUi)
            {
                await context.Ui.NotifyAsync("UI all-regions sample command executed.", cancellationToken).ConfigureAwait(false);
            }

            return PluginCommandResult.Message("UI all-regions sample is active; check the command bar/thread footer for visible markers.");
        });
    }

    public override IEnumerable<PluginUiContribution> GetUiContributions()
    {
        foreach (var region in Enum.GetValues<PluginUiRegion>())
        {
            yield return PluginUi.Visual(region, static context => new Markup($"[dim]plugin-ui:{context.Region}[/]"), $"visual-{region}");
        }

        yield return new PluginStatusContribution
        {
            Region = PluginUiRegion.ThreadStatus,
            Name = "thread-status",
            GetStatus = static _ => new PluginStatusItem
            {
                Label = "UI sample status",
                Text = "thread status",
                IconMarkup = "[green]●[/]",
                Tone = PluginStatusTone.Success,
            },
        };

        yield return new PluginStatusContribution
        {
            Region = PluginUiRegion.ThreadFooter,
            Name = "thread-footer-status",
            GetStatus = static _ => new PluginStatusItem
            {
                Label = "UI sample footer",
                Text = "thread footer",
                IconMarkup = "[blue]●[/]",
                Tone = PluginStatusTone.Info,
            },
        };

        yield return PluginUi.Renderer(PluginUiRegion.PromptAttachmentRenderer, "ui-all-regions", RenderMarkdownAsync);
        yield return PluginUi.Renderer(PluginUiRegion.TimelineMessageRenderer, "ui-all-regions", RenderMarkdownAsync);
        yield return PluginUi.Renderer(PluginUiRegion.ToolCallRenderer, "ui-all-regions", RenderMarkdownAsync);
    }

    private static ValueTask<PluginRenderResult?> RenderMarkdownAsync(PluginRendererContext context, CancellationToken cancellationToken)
    {
        var payload = context.Payload?.ToString() ?? "no payload";
        return ValueTask.FromResult<PluginRenderResult?>(PluginRenderResult.FromMarkdown($"**plugin-ui renderer** `{context.Target ?? "default"}` payload: `{payload}`"));
    }
}
