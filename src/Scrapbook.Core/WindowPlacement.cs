namespace Scrapbook.Core;

public static class WindowPlacement
{
    private const double MinVisibleWidth = 100;
    private const double TitleBarHeight = 30;

    /// <summary>
    /// True when at least a 100×30 strip of the window's top edge (its title bar) lies inside
    /// the screen rectangle, so the user can still grab and move the window.
    /// </summary>
    public static bool IsOnScreen(double left, double top, double width,
        double screenLeft, double screenTop, double screenWidth, double screenHeight)
    {
        if (top < screenTop || top + TitleBarHeight > screenTop + screenHeight) return false;

        var visibleWidth = Math.Min(left + width, screenLeft + screenWidth) - Math.Max(left, screenLeft);
        return visibleWidth >= MinVisibleWidth;
    }
}
