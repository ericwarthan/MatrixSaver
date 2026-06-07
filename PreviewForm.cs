using System;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Web.WebView2.WinForms;

namespace MatrixSaver;

/// <summary>
/// Screensaver preview window embedded inside the Windows Display Properties
/// monitor thumbnail via Win32 SetParent.
/// </summary>
internal sealed class PreviewForm : Form
{
    private readonly IntPtr  _parentHwnd;
    private readonly WebView2 _webView = new();
    private readonly string  _appDir;
    private readonly string  _url;
    private readonly System.Windows.Forms.Timer _parentWatcher = new() { Interval = 500 };

    public PreviewForm(IntPtr parentHwnd, string appDir, string url)
    {
        _parentHwnd = parentHwnd;
        _appDir     = appDir;
        _url        = url;

        FormBorderStyle = FormBorderStyle.None;
        BackColor       = Color.Black;
        ShowInTaskbar   = false;
        Width           = 160;
        Height          = 120;

        _webView.Dock = DockStyle.Fill;
        Controls.Add(_webView);

        Load   += OnLoad;
        Closed += (_, _) => _parentWatcher.Stop();
    }

    private async void OnLoad(object? sender, EventArgs e)
    {
        // ── Embed into Windows' screensaver preview panel ─────────────────
        if (_parentHwnd != IntPtr.Zero)
        {
            // Get the size of the parent preview panel
            NativeMethods.GetClientRect(_parentHwnd, out var rect);
            int w = rect.Width  > 0 ? rect.Width  : 160;
            int h = rect.Height > 0 ? rect.Height : 120;

            // Convert to WS_CHILD, reparent, resize
            int style = NativeMethods.GetWindowLong(Handle, NativeMethods.GWL_STYLE);
            style = (style & ~NativeMethods.WS_POPUP) | NativeMethods.WS_CHILD;
            NativeMethods.SetWindowLong(Handle, NativeMethods.GWL_STYLE, style);

            NativeMethods.SetParent(Handle, _parentHwnd);
            NativeMethods.MoveWindow(Handle, 0, 0, w, h, true);

            // Watch for parent window destruction — exit when settings dialog closes
            _parentWatcher.Tick += (_, _) =>
            {
                if (!NativeMethods.IsWindow(_parentHwnd))
                    Application.Exit();
            };
            _parentWatcher.Start();
        }
        else
        {
            // No parent hwnd — show a standalone small preview window
            Show();
        }

        // ── Initialise WebView2 ───────────────────────────────────────────
        try
        {
            var userDataDir = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MatrixSaver", "WebView2-Preview");

            var env = await Microsoft.Web.WebView2.Core.CoreWebView2Environment
                .CreateAsync(null, userDataDir);
            await _webView.EnsureCoreWebView2Async(env);

            _webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                "matrix.local",
                System.IO.Path.Combine(_appDir, "matrix"),
                Microsoft.Web.WebView2.Core.CoreWebView2HostResourceAccessKind.Allow);

            _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            _webView.CoreWebView2.Settings.AreDevToolsEnabled            = false;

            _webView.Source = new Uri(_url);
        }
        catch { /* Preview failing is non-fatal */ }
    }
}
