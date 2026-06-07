using System;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Web.WebView2.WinForms;

namespace MatrixSaver;

internal sealed class SettingsForm : Form
{
    private readonly WebView2      _webView = new();
    private readonly string        _appDir;
    private readonly ConfigBridge  _bridge  = new();

    public SettingsForm(string appDir)
    {
        _appDir = appDir;

        Text          = "Matrix Screensaver — Settings";
        Width         = 1080;
        Height        = 760;
        MinimumSize   = new Size(820, 600);
        BackColor     = Color.FromArgb(10, 10, 10);
        StartPosition = FormStartPosition.CenterScreen;

        _webView.Dock = DockStyle.Fill;
        Controls.Add(_webView);

        Load += OnLoad;
    }

    private async void OnLoad(object? sender, EventArgs e)
    {
        var userDataDir = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MatrixSaver", "WebView2-Settings");

        var env = await Microsoft.Web.WebView2.Core.CoreWebView2Environment
            .CreateAsync(null, userDataDir);
        await _webView.EnsureCoreWebView2Async(env);

        // matrix.local → matrix engine files (used by the live preview iframe)
        _webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
            "matrix.local",
            System.IO.Path.Combine(_appDir, "matrix"),
            Microsoft.Web.WebView2.Core.CoreWebView2HostResourceAccessKind.Allow);

        // app.local → settings.html + settings.js
        _webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
            "app.local",
            _appDir,
            Microsoft.Web.WebView2.Core.CoreWebView2HostResourceAccessKind.Allow);

        // Register C# bridge as chrome.webview.hostObjects.configBridge
        _webView.CoreWebView2.AddHostObjectToScript("configBridge", _bridge);

        // Inject adapter before any page script runs.
        // Creates window.configAPI matching the interface settings.js expects,
        // delegating to the C# ConfigBridge via WebView2 host objects.
        const string adapter = """
            (function() {
                const b = chrome.webview.hostObjects.configBridge;
                window.configAPI = {
                    getConfig:    ()    => b.GetConfig().then(s => JSON.parse(s)),
                    getDefaults:  ()    => b.GetDefaults().then(s => JSON.parse(s)),
                    saveConfig:   (cfg) => b.SaveConfig(JSON.stringify(cfg)),
                    getMatrixURL: (cfg) => b.GetMatrixURL(JSON.stringify(cfg)),
                };
            })();
            """;
        await _webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(adapter);

        _webView.Source = new Uri("http://app.local/settings.html");
    }
}
