using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace MatrixSaver;

static class Program
{
    [STAThread]
    static void Main(string[] rawArgs)
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        // When running as a single-file publish the exe extracts to a temp dir;
        // AppContext.BaseDirectory points there. The matrix/ folder and
        // settings.html are placed alongside the exe at build time.
        string appDir = Path.GetDirectoryName(
                            Environment.ProcessPath ?? AppContext.BaseDirectory)
                        ?? AppContext.BaseDirectory;

        // ── Parse screensaver CLI args ────────────────────────────────────
        // Windows screensaver protocol:
        //   <name>.scr /s        → run screensaver
        //   <name>.scr /p <hwnd> → show preview in parent window
        //   <name>.scr /c        → open settings (also /c:<hwnd> on some Windows)
        string mode        = "screensaver";
        nint   previewHwnd = 0;

        for (int i = 0; i < rawArgs.Length; i++)
        {
            string raw = rawArgs[i];
            string key = raw.TrimStart('/', '-').Split(':')[0].ToLowerInvariant();

            if (key == "s") { mode = "screensaver"; }
            else if (key == "p")
            {
                mode = "preview";
                string? hwndStr = raw.Contains(':') ? raw.Split(':')[1]
                                : (i + 1 < rawArgs.Length ? rawArgs[++i] : null);
                if (hwndStr != null && long.TryParse(hwndStr, out long h))
                    previewHwnd = (nint)h;
            }
            else if (key == "c") { mode = "settings"; }
        }

        switch (mode)
        {
            case "screensaver": RunScreensaver(appDir); break;
            case "preview":     RunPreview(appDir, previewHwnd); break;
            case "settings":    Application.Run(new SettingsForm(appDir)); break;
        }
    }

    // ── /s — fullscreen on every monitor ─────────────────────────────────
    static void RunScreensaver(string appDir)
    {
        var config = ConfigManager.Load();
        string url = UrlBuilder.Build(config);

        // Tell Windows a screensaver is running. This suppresses the taskbar
        // auto-raise timer and yields z-order to our TOPMOST windows.
        NativeMethods.SetScreensaverRunning(true);
        try
        {
            Screen primary = Screen.PrimaryScreen ?? Screen.AllScreens[0];

            var forms = Screen.AllScreens
                .Select(s => new ScreensaverForm(s, s.DeviceName == primary.DeviceName, appDir, url))
                .ToList();

            // Show non-primary forms first so the primary gets focus last.
            foreach (var f in forms.Where(f => !f.IsPrimary))
                f.Show();
            foreach (var f in forms.Where(f =>  f.IsPrimary))
                f.Show();

            Application.Run(new MultiFormContext(forms));
        }
        finally
        {
            NativeMethods.SetScreensaverRunning(false);
        }
    }

    // ── /p — small embedded preview ──────────────────────────────────────
    static void RunPreview(string appDir, nint parentHwnd)
    {
        var config = ConfigManager.Load();
        string url = UrlBuilder.Build(config, overrideResolution: "0.5", overrideFps: 24);
        Application.Run(new PreviewForm(parentHwnd, appDir, url));
    }
}

// ── ApplicationContext that keeps the app alive while ANY form is open ────────
internal sealed class MultiFormContext : ApplicationContext
{
    private readonly List<Form> _forms;

    public MultiFormContext(IEnumerable<Form> forms)
    {
        _forms = forms.ToList();
        foreach (var f in _forms)
            f.FormClosed += OnFormClosed;
    }

    private void OnFormClosed(object? sender, FormClosedEventArgs e)
    {
        // One form closed (input detected) — close all remaining and exit cleanly.
        foreach (var f in _forms)
        {
            if (f != sender && !f.IsDisposed)
                try { f.Close(); } catch { }
        }
        // Application.Exit() terminates the message loop and signals all
        // remaining forms to close, ensuring a clean process exit.
        Application.Exit();
    }
}
