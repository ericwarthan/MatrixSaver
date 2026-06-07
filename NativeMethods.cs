using System;
using System.Runtime.InteropServices;

namespace MatrixSaver;

internal static class NativeMethods
{
    // ── SystemParametersInfo ──────────────────────────────────────────────────
    private const uint SPI_SETSCREENSAVERRUNNING = 0x0061;
    private const uint SPIF_SENDWININICHANGE      = 0x0002;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SystemParametersInfo(
        uint uiAction, uint uiParam, IntPtr pvParam, uint fWinIni);

    public static void SetScreensaverRunning(bool running)
    {
        SystemParametersInfo(SPI_SETSCREENSAVERRUNNING,
                             running ? 1u : 0u,
                             IntPtr.Zero,
                             SPIF_SENDWININICHANGE);
    }

    // ── Window embedding ─────────────────────────────────────────────────────
    public const int GWL_STYLE    = -16;
    public const int GWL_EXSTYLE  = -20;
    public const int WS_CHILD     = 0x40000000;
    public const int WS_POPUP     = unchecked((int)0x80000000);
    public const int WS_VISIBLE   = 0x10000000;
    public const int WS_EX_TOPMOST    = 0x00000008;
    public const int WS_EX_NOACTIVATE = 0x08000000;
    public const int WS_EX_TOOLWINDOW = 0x00000080;

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left, Top, Right, Bottom;
        public int Width  => Right  - Left;
        public int Height => Bottom - Top;
    }

    [DllImport("user32.dll")] public static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);
    [DllImport("user32.dll")] public static extern int    SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
    [DllImport("user32.dll")] public static extern int    GetWindowLong(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll")] public static extern bool   MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);
    [DllImport("user32.dll")] public static extern bool   GetClientRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32.dll")] public static extern bool   IsWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool   ShowWindow(IntPtr hWnd, int nCmdShow);

    // ── Low-level global input hooks ─────────────────────────────────────────
    public const int WH_KEYBOARD_LL = 13;
    public const int WH_MOUSE_LL    = 14;
    public const int WM_MOUSEMOVE   = 0x0200;
    public const int HC_ACTION      = 0;

    public delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SetWindowsHookEx(
        int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    public static extern IntPtr CallNextHookEx(
        IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr GetModuleHandle(string? lpModuleName);

    [StructLayout(LayoutKind.Sequential)]
    public struct MSLLHOOKSTRUCT
    {
        public System.Drawing.Point pt;
        public uint   mouseData;
        public uint   flags;
        public uint   time;
        public IntPtr dwExtraInfo;
    }
}
