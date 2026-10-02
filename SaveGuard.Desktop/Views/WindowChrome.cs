using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Avalonia.Controls;

namespace SaveGuard.Desktop.Views;

/// <summary>
/// Tints the native Windows title bar to match the app palette via the DWM window attributes, so we
/// keep fully functional native window controls while losing the stock light title bar. No-ops on
/// non-Windows platforms and on Windows versions that predate the relevant attributes.
/// </summary>
internal static class WindowChrome
{
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_BORDER_COLOR = 34;
    private const int DWMWA_CAPTION_COLOR = 35;
    private const int DWMWA_TEXT_COLOR = 36;

    // COLORREF is 0x00BBGGRR, matching the Inklog-derived app palette.
    private const uint CaptionColor = 0x00151110; // #0F1115 (AppBackground)
    private const uint TextColor = 0x00FAF7F5;     // #F5F7FA (PrimaryText)
    private const uint BorderColor = 0x00241F1B;   // #1B1F24 (Surface)

    public static void Apply(Window window)
    {
        if (!OperatingSystem.IsWindows())
            return;

        // The platform handle isn't available until the window has a native peer, so apply on open.
        window.Opened += (_, _) =>
        {
            if (OperatingSystem.IsWindows())
                TryApply(window);
        };
        if (window.IsLoaded)
            TryApply(window);
    }

    [SupportedOSPlatform("windows")]
    private static void TryApply(Window window)
    {
        var handle = window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        if (handle == IntPtr.Zero)
            return;

        SetBool(handle, DWMWA_USE_IMMERSIVE_DARK_MODE, true);
        SetColor(handle, DWMWA_CAPTION_COLOR, CaptionColor);
        SetColor(handle, DWMWA_TEXT_COLOR, TextColor);
        SetColor(handle, DWMWA_BORDER_COLOR, BorderColor);
    }

    [SupportedOSPlatform("windows")]
    private static void SetBool(IntPtr handle, int attribute, bool value)
    {
        var data = value ? 1 : 0;
        _ = DwmSetWindowAttribute(handle, attribute, ref data, sizeof(int));
    }

    [SupportedOSPlatform("windows")]
    private static void SetColor(IntPtr handle, int attribute, uint colorRef)
    {
        var data = colorRef;
        // Older Windows builds return a failure HRESULT for the color attributes; ignore it.
        _ = DwmSetWindowAttribute(handle, attribute, ref data, sizeof(uint));
    }

    [DllImport("dwmapi.dll", SetLastError = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int pvAttribute, int cbAttribute);

    [DllImport("dwmapi.dll", SetLastError = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref uint pvAttribute, int cbAttribute);
}
