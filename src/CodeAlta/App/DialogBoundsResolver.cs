using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Geometry;

namespace CodeAlta.App;

internal static class DialogBoundsResolver
{
    public static Rectangle? ResolveAppBounds(Visual? focusTarget)
    {
        if (focusTarget is null)
        {
            return null;
        }

        return focusTarget.App?.Root.GetAbsoluteBounds() ?? focusTarget.GetAbsoluteBounds();
    }
}
