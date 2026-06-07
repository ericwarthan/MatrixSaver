using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Web.WebView2.WinForms;

namespace MatrixSaver;

internal sealed class SettingsForm : Form
{
    private readonly WebView2      _webView = new();
    private readonly string        _appDir;
    private readonly ConfigBridge  _bridge;

    public SettingsForm(string appDir)
    {
        _appDir = appDir;
        _bridge = new ConfigBridge(appDir);

        Text            = "Matrix Screensaver — Settings";
        Width           = 1080;
        Height          = 760;
        MinimumSize     = new Size(820, 600);
        BackColor       = Color.FromArgb(10, 10, 10);
        StartPosition   = FormStartPosition.CenterScreen;

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

        // Map matrix files — used by the live preview iframe in settings.html
        _webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
            "matrix.local",
            System.IO.Path.Combine(_appDir, "matrix"),
            Microsoft.Web.WebView2.Core.CoreWebView2HostResourceAccessKind.Allow);

        // Map app files (settings.html, settings.js) to a virtual host
        _webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
            "app.local",
            _appDir,
            Microsoft.Web.WebView2.Core.CoreWebView2HostResourceAccessKind.Allow);

        // Register C# bridge — called from JavaScript as chrome.webview.hostObjects.configBridge
        _webView.CoreWebView2.AddHostObjectToScript("configBridge", _bridge);

        // Inject adapter before any page script runs.
        // Replaces the Electron contextBridge configAPI with equivalent WebView2 calls.
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

// ── COM-visible bridge: C# methods callable from JavaScript via host objects ──
[ComVisible(true)]
[ClassInterface(ClassInterfaceType.AutoDual)]
public class ConfigBridge
{
    private readonly string _appDir;

    public ConfigBridge(string appDir) => _appDir = appDir;

    public string GetConfig()    => ConfigManager.ToJson(ConfigManager.Load());
    public string GetDefaults()  => ConfigManager.DefaultsJson();

    public bool SaveConfig(string json)
    {
        var cfg = ConfigManager.FromJson(json);
        return cfg != null && ConfigManager.Save(cfg);
    }

    public string GetMatrixURL(string json)
    {
        var cfg = ConfigManager.FromJson(json) ?? ConfigManager.Load();
        return UrlBuilder.Build(cfg);
    }
}
