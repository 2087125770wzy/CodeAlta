using XenoAtom.Ansi;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Controls;
using XenoAtom.Terminal.UI.Styling;

internal static class ThreadTabVisualFactory
{
    private const int MaxTabTitleLength = 18;

    public static string CompactTitle(string title)
    {
        var normalized = title.Trim();
        return normalized.Length <= MaxTabTitleLength
            ? normalized
            : normalized[..Math.Max(1, MaxTabTitleLength - 1)].TrimEnd() + "…";
    }

    public static CodeAltaApp.OpenTabIndicatorKind ResolveIndicatorKind(bool isBusy, CodeAltaApp.StatusTone tone)
    {
        if (isBusy)
        {
            return CodeAltaApp.OpenTabIndicatorKind.Running;
        }

        return tone switch
        {
            CodeAltaApp.StatusTone.Warning => CodeAltaApp.OpenTabIndicatorKind.Warning,
            CodeAltaApp.StatusTone.Error => CodeAltaApp.OpenTabIndicatorKind.Error,
            CodeAltaApp.StatusTone.Info => CodeAltaApp.OpenTabIndicatorKind.Info,
            _ => CodeAltaApp.OpenTabIndicatorKind.Ready,
        };
    }

    public static Visual CreateIndicator(bool isBusy, CodeAltaApp.StatusTone tone)
    {
        var kind = ResolveIndicatorKind(isBusy, tone);
        if (kind == CodeAltaApp.OpenTabIndicatorKind.Running)
        {
            var spinner = new Spinner().Style(SpinnerStyles.Arc);
            spinner.IsActive(() => true);
            spinner.IsVisible(() => true);
            return spinner;
        }

        var statusTone = kind switch
        {
            CodeAltaApp.OpenTabIndicatorKind.Warning => CodeAltaApp.StatusTone.Warning,
            CodeAltaApp.OpenTabIndicatorKind.Error => CodeAltaApp.StatusTone.Error,
            CodeAltaApp.OpenTabIndicatorKind.Info => CodeAltaApp.StatusTone.Info,
            _ => CodeAltaApp.StatusTone.Ready,
        };
        return new Markup(StatusVisualFormatter.BuildStatusIconMarkup(statusTone))
        {
            Wrap = false,
        };
    }

    public static Visual CreateTitle(string title)
    {
        return new Markup(AnsiMarkup.Escape(title))
        {
            Wrap = false,
        };
    }
}
