using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Web.WebView2.WinForms;

namespace MatrixSaver;

/// <summary>
/// Fullscreen screensaver window for one monitor.
/// Exits on any keyboard or mouse input (after a 1.5 s grace period).
/// </summary>
internal sealed class ScreensaverForm : Form
{
    // Low-level hook handles — static so the delegate won't be GC'd
    private static NativeMethods.HookProc? _kbProc;
    private static NativeMethods.HookProc? _mouseProc;
    private static IntPtr _kbHook    = IntPtr.Zero;
    private static IntPtr _mouseHook = IntPtr.Zero;
    private static DateTime  _startTime;
    private static Point     _firstMousePos;
    private static bool      _firstMouseSeen = false;
    private static bool      _exiting        = false;
    private static readonly object _exitLock  = new();

    private readonly WebView2 _webView = new();
    private readonly string   _appDir;
    private readonly string   _url;
    private readonly bool     _isPrimary;

    /// <summary>True for the form on the primary display; used by Program to control show order.</summary>
    public bool IsPrimary => _isPrimary;

    public ScreensaverForm(Screen screen, bool isPrimary, string appDir, string url)
    {
        _appDir    = appDir;
        _url       = url;
        _isPrimary = isPrimary;

        // ── Window properties ─────────────────────────────────────────────
        FormBorderStyle  = FormBorderStyle.None;
        BackColor        = Color.Black;
        ShowInTaskbar    = false;
        // Position over the target monitor (set before handle creation)
        SetBounds(screen.Bounds.X, screen.Bounds.Y,
                  screen.Bounds.Width, screen.Bounds.Height);

        _webView.Dock = DockStyle.Fill;
        Controls.Add(_webView);

        Load   += OnLoad;
        Shown  += OnShown;
        Closed += OnClosed;
    }

    // Force WS_EX_TOPMOST and WS_EX_NOACTIVATE at window creation time —
    // the only reliable way to beat the taskbar's z-order.
    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= NativeMethods.WS_EX_TOPMOST
                        | NativeMethods.WS_EX_NOACTIVATE
                        | NativeMethods.WS_EX_TOOLWINDOW;
            return cp;
        }
    }

    // ── Initialise WebView2 ───────────────────────────────────────────────
    private async void OnLoad(object? sender, EventArgs e)
    {
        try
        {
            var userDataDir = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MatrixSaver", "WebView2");

            var env = await Microsoft.Web.WebView2.Core.CoreWebView2Environment
                .CreateAsync(null, userDataDir);
            await _webView.EnsureCoreWebView2Async(env);

            // Map matrix.local → <appdir>\matrix\  (serves ES modules without CORS issues)
            _webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                "matrix.local",
                System.IO.Path.Combine(_appDir, "matrix"),
                Microsoft.Web.WebView2.Core.CoreWebView2HostResourceAccessKind.Allow);

            // Suppress context menu and dev tools in screensaver mode
            _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled  = false;
            _webView.CoreWebView2.Settings.AreDevToolsEnabled             = false;
            _webView.CoreWebView2.Settings.IsStatusBarEnabled             = false;
            _webView.CoreWebView2.Settings.IsZoomControlEnabled           = false;

            _webView.Source = new Uri(_url);
        }
        catch (Exception ex)
        {
            // If WebView2 fails to initialise (e.g. runtime not installed),
            // show a minimal fallback and still respond to input to allow exit.
            _webView.Visible = false;
            var lbl = new Label {
                Text      = $"WebView2 unavailable:\n{ex.Message}\n\nPress any key to exit.",
                ForeColor = Color.FromArgb(0, 255, 65),
                BackColor = Color.Black,
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
            };
            Controls.Add(lbl);
        }
    }

    // ── Install global input hooks after all forms are shown ──────────────
    private void OnShown(object? sender, EventArgs e)
    {
        if (_isPrimary)
        {
            Cursor.Hide();   // hide system cursor for screensaver
            InstallHooks();
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (_isPrimary)
        {
            RemoveHooks();
            try { Cursor.Show(); } catch { }  // restore cursor on exit
        }
    }

    // ── Low-level keyboard hook ───────────────────────────────────────────
    private static void InstallHooks()
    {
        _startTime      = DateTime.UtcNow;
        _firstMouseSeen = false;
        _exiting        = false;

        var hMod = NativeMethods.GetModuleHandle(null);

        _kbProc    = KeyboardHookCallback;
        _mouseProc = MouseHookCallback;

        _kbHook    = NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, _kbProc,    hMod, 0);
        _mouseHook = NativeMethods.SetWindowsHookEx(NativeMethods.WH_MOUSE_LL,    _mouseProc, hMod, 0);
    }

    private static void RemoveHooks()
    {
        if (_kbHook    != IntPtr.Zero) { NativeMethods.UnhookWindowsHookEx(_kbHook);    _kbHook    = IntPtr.Zero; }
        if (_mouseHook != IntPtr.Zero) { NativeMethods.UnhookWindowsHookEx(_mouseHook); _mouseHook = IntPtr.Zero; }
    }

    private static void TryExit()
    {
        lock (_exitLock)
        {
            if (_exiting) return;
            if ((DateTime.UtcNow - _startTime).TotalMilliseconds < 1500) return;
            _exiting = true;
        }
        RemoveHooks();
        // Marshal back to UI thread
        Application.OpenForms[0]?.Invoke(Application.Exit);
    }

    private static IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= NativeMethods.HC_ACTION)
            TryExit();
        return NativeMethods.CallNextHookEx(_kbHook, nCode, wParam, lParam);
    }

    private static IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= NativeMethods.HC_ACTION)
        {
            var info = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);

            if (!_firstMouseSeen)
            {
                _firstMousePos  = info.pt;
                _firstMouseSeen = true;
            }
            else
            {
                int dx = Math.Abs(info.pt.X - _firstMousePos.X);
                int dy = Math.Abs(info.pt.Y - _firstMousePos.Y);
                if (dx > 6 || dy > 6 || (int)wParam != NativeMethods.WM_MOUSEMOVE)
                    TryExit();
            }
        }
        return NativeMethods.CallNextHookEx(_mouseHook, nCode, wParam, lParam);
    }
}
