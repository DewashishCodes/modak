using System.Runtime.InteropServices;
using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;

namespace Modak;

// Lets the window be fully transparent: WinUI paints this brush behind the content instead of a solid backdrop.
sealed class TransparentBackdrop : SystemBackdrop
{
    static Windows.UI.Composition.Compositor? system;
    protected override void OnTargetConnected(ICompositionSupportsSystemBackdrop target, XamlRoot root)
    {
        base.OnTargetConnected(target, root);
        if (Windows.System.DispatcherQueue.GetForCurrentThread() == null) Native.EnsureSystemDispatcher();
        system ??= new Windows.UI.Composition.Compositor();
        target.SystemBackdrop = system.CreateColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
    }
}

static class Native
{
    const int GWL_STYLE = -16, GWL_EXSTYLE = -20;
    const long WS_POPUP = 0x80000000L, WS_VISIBLE = 0x10000000L, WS_EX_TOOLWINDOW = 0x80, WS_EX_NOACTIVATE = 0x08000000;

    [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern bool GetCursorPos(out PointInt32 pt);
    [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] static extern long GetWindowLongPtr(IntPtr h, int i);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] static extern long SetWindowLongPtr(IntPtr h, int i, long v);
    [DllImport("user32.dll")] static extern int SetWindowRgn(IntPtr h, IntPtr region, bool redraw);
    [DllImport("gdi32.dll")] static extern IntPtr CreateRectRgn(int l, int t, int r, int b);
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int value, int size);
    [DllImport("dwmapi.dll")] static extern int DwmExtendFrameIntoClientArea(IntPtr h, ref Margins m);
    struct Margins { public int L, R, T, B; }
    [DllImport("CoreMessaging.dll")] static extern int CreateDispatcherQueueController(DispatcherQueueOptions o, out IntPtr controller);
    [StructLayout(LayoutKind.Sequential)] struct DispatcherQueueOptions { public int dwSize, threadType, apartmentType; }

    public static bool ShiftDown => (GetAsyncKeyState(0x10) & 0x8000) != 0;

    public static void EnsureSystemDispatcher() =>
        CreateDispatcherQueueController(new DispatcherQueueOptions { dwSize = Marshal.SizeOf<DispatcherQueueOptions>(), threadType = 2, apartmentType = 2 }, out _);

    // A borderless popup with no taskbar entry that never activates, composited with the content's alpha.
    public static void Chrome(IntPtr h)
    {
        // Style first: changing it resets the DWM frame attributes set below.
        SetWindowLongPtr(h, GWL_STYLE, WS_POPUP | WS_VISIBLE); // no caption buttons in the extended frame
        SetWindowLongPtr(h, GWL_EXSTYLE, GetWindowLongPtr(h, GWL_EXSTYLE) | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);
        SetWindowPos(h, IntPtr.Zero, 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0004 | 0x0010 | 0x0020); // NOSIZE | NOMOVE | NOZORDER | NOACTIVATE | FRAMECHANGED
        int round = 1;
        DwmSetWindowAttribute(h, 33, ref round, 4); // DWMWA_WINDOW_CORNER_PREFERENCE = DONOTROUND
        var glass = new Margins { L = -1, R = -1, T = -1, B = -1 };
        DwmExtendFrameIntoClientArea(h, ref glass); // let DWM use the content's alpha for the whole window
    }

    // The system owns the region after SetWindowRgn, so it is not deleted here.
    public static void Region(IntPtr h, int l, int t, int r, int b) => SetWindowRgn(h, CreateRectRgn(l, t, r, b), true);
}
