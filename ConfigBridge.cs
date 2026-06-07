using System.Runtime.InteropServices;

namespace MatrixSaver;

/// <summary>
/// COM-visible bridge exposed to JavaScript via WebView2 host objects.
/// Registered as <c>chrome.webview.hostObjects.configBridge</c> in the settings page.
/// All methods are called asynchronously from JS and return synchronously from C#.
/// </summary>
[ComVisible(true)]
[ClassInterface(ClassInterfaceType.AutoDual)]
public class ConfigBridge
{
    public string GetConfig()   => ConfigManager.ToJson(ConfigManager.Load());
    public string GetDefaults() => ConfigManager.DefaultsJson();

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
