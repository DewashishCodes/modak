using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Modak;

// Connecting to Claude Code and Codex, and starting with Windows: shared by the tray menu and the settings page.
static class Connections
{
    static string Exe => Environment.ProcessPath!;
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public static bool Claude => Hooks.IsClaudeInstalled();
    public static bool Codex => Hooks.IsCodexInstalled();

    public static void SetClaude(bool on, Prefs prefs)
    {
        try { if (on) Hooks.InstallClaude(Island.HookUrl); else Hooks.Uninstall(Hooks.ClaudeSettings); }
        catch (Exception x) { Dialogs.Error($"Could not update {Hooks.ClaudeSettings}.\nIt may not be valid JSON, so it was left untouched.\n\n{x.Message}"); }
        prefs.Declined = !on;
        prefs.Save();
    }

    public static void SetCodex(bool on)
    {
        try { if (on) Hooks.InstallCodex(Exe, Island.HookUrl); else Hooks.Uninstall(Hooks.CodexHooks); }
        catch (Exception x) { Dialogs.Error($"Could not update {Hooks.CodexHooks}.\nIt may not be valid JSON, so it was left untouched.\n\n{x.Message}"); }
    }

    public static bool Autostart
    {
        get => Registry.CurrentUser.OpenSubKey(RunKey)?.GetValue("Modak") is string v && v.Contains(Exe, StringComparison.OrdinalIgnoreCase);
        set
        {
            using var k = Registry.CurrentUser.CreateSubKey(RunKey);
            if (value) k.SetValue("Modak", $"\"{Exe}\""); else k.DeleteValue("Modak", false);
        }
    }

    // First start: refresh hooks that point somewhere old (the Peninsula name, the Electron build's node
    // forwarder, a moved exe) without asking; otherwise ask once whether to connect to Claude Code.
    public static void OnStartup(Prefs prefs)
    {
        if (Hooks.IsClaudeStale(Island.HookUrl)) { SetClaude(true, prefs); }
        if (Hooks.IsCodexStale(Exe, Island.HookUrl)) SetCodex(true);
        if (Claude || prefs.Declined) return;
        var yes = Dialogs.Ask($"Connect Modak to Claude Code?\n\nThis adds Modak's hooks to {Hooks.ClaudeSettings} (a backup is kept next to it). Your other settings and hooks stay as they are. You can disconnect any time from the tray icon.");
        SetClaude(yes, prefs);
    }
}

// The tray icon: a soft pink dot drawn in code, with the connect, autostart and quit menu.
sealed class Tray : IDisposable
{
    const uint WM_TRAY = 0x8000 + 1, NIM_ADD = 0, NIM_MODIFY = 1, NIM_DELETE = 2, NIF_MESSAGE = 1, NIF_ICON = 2, NIF_TIP = 4;
    const uint MF_STRING = 0, MF_CHECKED = 8, MF_SEPARATOR = 0x800, TPM_RETURNCMD = 0x100, TPM_RIGHTBUTTON = 2;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct NotifyIconData
    {
        public int cbSize; public IntPtr hWnd; public uint uID, uFlags, uCallbackMessage; public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip;
        public uint dwState, dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szInfo;
        public uint uVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string szInfoTitle;
        public uint dwInfoFlags; public Guid guidItem; public IntPtr hBalloonIcon;
    }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] static extern bool Shell_NotifyIconW(uint msg, ref NotifyIconData data);
    [DllImport("user32.dll")] static extern IntPtr CreateIcon(IntPtr inst, int w, int h, byte planes, byte bpp, byte[] and, byte[] xor);
    [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr icon);
    [DllImport("user32.dll")] static extern IntPtr CreatePopupMenu();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool AppendMenuW(IntPtr menu, uint flags, UIntPtr id, string? text);
    [DllImport("user32.dll")] static extern int TrackPopupMenu(IntPtr menu, uint flags, int x, int y, int reserved, IntPtr hwnd, IntPtr rect);
    [DllImport("user32.dll")] static extern bool DestroyMenu(IntPtr menu);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hwnd);
    [DllImport("user32.dll")] static extern bool PostMessageW(IntPtr hwnd, uint msg, IntPtr w, IntPtr l);

    readonly IntPtr hwnd, icon;
    readonly Island island;
    NotifyIconData data;

    public Tray(IntPtr hwnd, Island island)
    {
        this.hwnd = hwnd;
        this.island = island;
        icon = Dot();
        data = new NotifyIconData { cbSize = Marshal.SizeOf<NotifyIconData>(), hWnd = hwnd, uID = 1, uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP, uCallbackMessage = WM_TRAY, hIcon = icon, szTip = "Modak", szInfo = "", szInfoTitle = "" };
        Shell_NotifyIconW(NIM_ADD, ref data);
        Native.OnMessage(hwnd, (msg, w, l) => { if (msg == WM_TRAY && ((uint)l is 0x205 or 0x202)) Menu(); }); // right or left button up
        Refresh();
    }

    // 16x16 pink dot with a soft edge, as raw 32-bit BGRA, so the tray needs no image file.
    static IntPtr Dot()
    {
        const int size = 16;
        var xor = new byte[size * size * 4];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                double d = Math.Sqrt((x - 7.5) * (x - 7.5) + (y - 8) * (y - 8)), a = Math.Clamp(7 - d, 0, 1);
                int i = (y * size + x) * 4;
                (xor[i], xor[i + 1], xor[i + 2], xor[i + 3]) = (0xf4, 0xd9, 0xe4, (byte)(a * 255));
            }
        return CreateIcon(IntPtr.Zero, size, size, 1, 32, new byte[size * size / 8], xor);
    }

    public void Refresh(string status = "Watching Claude Code")
    {
        data.szTip = $"Modak: {(Connections.Claude ? status : "not connected")}";
        data.uFlags = NIF_TIP;
        Shell_NotifyIconW(NIM_MODIFY, ref data);
    }

    void Menu()
    {
        var m = CreatePopupMenu();
        AppendMenuW(m, MF_STRING | (Connections.Claude ? MF_CHECKED : 0), 1, "Connected to Claude Code");
        AppendMenuW(m, MF_STRING | (Connections.Codex ? MF_CHECKED : 0), 2, "Connected to Codex");
        AppendMenuW(m, MF_STRING | (Connections.Autostart ? MF_CHECKED : 0), 3, "Start with Windows");
        AppendMenuW(m, MF_SEPARATOR, 0, null);
        AppendMenuW(m, MF_STRING, 4, "Quit Modak");
        Native.GetCursorPos(out var pt);
        SetForegroundWindow(hwnd); // without it the menu does not close when clicking elsewhere
        var cmd = TrackPopupMenu(m, TPM_RETURNCMD | TPM_RIGHTBUTTON, pt.X, pt.Y, 0, hwnd, IntPtr.Zero);
        PostMessageW(hwnd, 0, 0, 0);
        DestroyMenu(m);
        switch (cmd)
        {
            case 1: Connections.SetClaude(!Connections.Claude, island.Prefs); break;
            case 2: Connections.SetCodex(!Connections.Codex); break;
            case 3: Connections.Autostart = !Connections.Autostart; break;
            case 4: island.Quit(); return;
        }
        Refresh();
        island.Rebuild();
    }

    public void Dispose()
    {
        Shell_NotifyIconW(NIM_DELETE, ref data);
        DestroyIcon(icon);
    }
}
