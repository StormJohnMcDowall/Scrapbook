using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Scrapbook.Interop;

/// <summary>Excludes a window from screen capture (Teams, Zoom, OBS, screenshots).</summary>
public static class DisplayAffinity
{
    private const uint WDA_NONE = 0x00;
    private const uint WDA_EXCLUDEFROMCAPTURE = 0x11;

    /// <summary>Applies now if the window has a handle, otherwise as soon as it gets one (before it is shown).</summary>
    public static void Apply(Window window, bool hide)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd != IntPtr.Zero)
        {
            SetWindowDisplayAffinity(hwnd, hide ? WDA_EXCLUDEFROMCAPTURE : WDA_NONE);
            return;
        }

        void OnSourceInitialized(object? sender, EventArgs e)
        {
            window.SourceInitialized -= OnSourceInitialized;
            Apply(window, hide);
        }
        window.SourceInitialized += OnSourceInitialized;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint dwAffinity);
}
