using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MatrixSaver;

// ── Config model ──────────────────────────────────────────────────────────────
// Property names match the camelCase keys used in config.json and by settings.js.
public class Config
{
    public string  Version          { get; set; } = "classic";
    public string  Effect           { get; set; } = "";
    public string  Font             { get; set; } = "";
    public int?    NumColumns       { get; set; }
    public double? BloomSize        { get; set; }
    public double? BloomStrength    { get; set; }
    public double? AnimationSpeed   { get; set; }
    public double? FallSpeed        { get; set; }
    public double? CycleSpeed       { get; set; }
    public double? RaindropLength   { get; set; }
    public double? DitherMagnitude  { get; set; }
    public double  Resolution       { get; set; } = 1.0;
    public int     Fps              { get; set; } = 60;
    public double? CursorIntensity  { get; set; }
    public double? GlintIntensity   { get; set; }
    public double? Density          { get; set; }
    public double? ForwardSpeed     { get; set; }
    public double  Slant            { get; set; } = 0;
    public bool    SkipIntro        { get; set; } = true;
    public bool    GlyphFlip        { get; set; } = false;
    public bool?   Volumetric       { get; set; }
    public bool    Loops            { get; set; } = false;
    public string  BackgroundColor  { get; set; } = "#000000";
    public string  CursorColor      { get; set; } = "#00ff41";
    public string  GlintColor       { get; set; } = "#ffffff";
    public string  StripeColors     { get; set; } = "";
    public string  Palette          { get; set; } = "";
    public string  ImageUrl         { get; set; } = "";
}

// ── ConfigManager ─────────────────────────────────────────────────────────────
public static class ConfigManager
{
    private static readonly JsonSerializerOptions _opts = new()
    {
        PropertyNamingPolicy        = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented               = true,
        DefaultIgnoreCondition      = JsonIgnoreCondition.Never,
    };

    private static string ConfigDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                     "MatrixSaver");

    private static string ConfigPath => Path.Combine(ConfigDir, "config.json");

    public static Config Load()
    {
        try
        {
            if (!File.Exists(ConfigPath)) return new Config();
            var json = File.ReadAllText(ConfigPath);
            return JsonSerializer.Deserialize<Config>(json, _opts) ?? new Config();
        }
        catch { return new Config(); }
    }

    public static bool Save(Config config)
    {
        try
        {
            Directory.CreateDirectory(ConfigDir);
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(config, _opts));
            return true;
        }
        catch { return false; }
    }

    public static string ToJson(Config config) => JsonSerializer.Serialize(config, _opts);
    public static string DefaultsJson()        => JsonSerializer.Serialize(new Config(), _opts);

    public static Config? FromJson(string json)
    {
        try { return JsonSerializer.Deserialize<Config>(json, _opts); }
        catch { return null; }
    }
}

// ── UrlBuilder ────────────────────────────────────────────────────────────────
public static class UrlBuilder
{
    // Virtual host "matrix.local" maps to the matrix/ folder next to the exe.
    // SettingsForm and ScreensaverForm both register this mapping in WebView2.
    private const string MatrixHost = "http://matrix.local/index.html";

    public static string Build(Config cfg, string? overrideResolution = null, int? overrideFps = null)
    {
        var sb = new System.Text.StringBuilder(MatrixHost + "?suppressWarnings=true");

        void Add(string key, string? val)
        {
            if (!string.IsNullOrEmpty(val))
                sb.Append($"&{key}={Uri.EscapeDataString(val)}");
        }
        void AddNum(string key, double? val)
        {
            if (val.HasValue) sb.Append($"&{key}={val.Value:G6}");
        }
        void AddBool(string key, bool val)
        {
            if (val) sb.Append($"&{key}=true");
        }

        if (cfg.Version != "classic") Add("version", cfg.Version);
        Add("effect", cfg.Effect);
        Add("font",   cfg.Font);

        if (cfg.NumColumns.HasValue)  sb.Append($"&numColumns={cfg.NumColumns}");
        AddNum("bloomSize",       cfg.BloomSize);
        AddNum("bloomStrength",   cfg.BloomStrength);
        AddNum("animationSpeed",  cfg.AnimationSpeed);
        AddNum("fallSpeed",       cfg.FallSpeed);
        AddNum("cycleSpeed",      cfg.CycleSpeed);
        AddNum("raindropLength",  cfg.RaindropLength);
        AddNum("ditherMagnitude", cfg.DitherMagnitude);
        AddNum("cursorIntensity", cfg.CursorIntensity);
        AddNum("glyphIntensity",  cfg.GlintIntensity);  // Plex URL param is glyphIntensity
        AddNum("density",         cfg.Density);
        AddNum("forwardSpeed",    cfg.ForwardSpeed);
        if (cfg.Slant != 0) AddNum("slant", cfg.Slant);

        // Resolution / FPS — allow caller overrides for preview mode
        double res = double.TryParse(overrideResolution, out var r) ? r : cfg.Resolution;
        int    fps = overrideFps ?? cfg.Fps;
        if (res != 1.0) sb.Append($"&resolution={res:G4}");
        if (fps != 60)  sb.Append($"&fps={fps}");

        AddBool("skipIntro",  cfg.SkipIntro);
        AddBool("glyphFlip",  cfg.GlyphFlip);
        if (cfg.Volumetric == true) sb.Append("&volumetric=true");
        AddBool("loops", cfg.Loops);

        // Colors — hex → normalized RGB (0-1 floats, comma-separated)
        string? bg = HexToRgb(cfg.BackgroundColor);
        string? cc = HexToRgb(cfg.CursorColor);
        string? gc = HexToRgb(cfg.GlintColor);

        if (bg != null && bg != "0.00000,0.00000,0.00000") Add("backgroundColor", bg);
        if (cc != null) Add("cursorColor", cc);
        if (gc != null && gc != "1.00000,1.00000,1.00000") Add("glintColor", gc);

        Add("stripeColors", cfg.StripeColors);
        Add("palette",      cfg.Palette);
        if (!string.IsNullOrEmpty(cfg.ImageUrl)) Add("url", cfg.ImageUrl);

        return sb.ToString();
    }

    private static string? HexToRgb(string? hex)
    {
        if (string.IsNullOrEmpty(hex) || hex.Length < 7) return null;
        try
        {
            double r = Convert.ToInt32(hex.Substring(1, 2), 16) / 255.0;
            double g = Convert.ToInt32(hex.Substring(3, 2), 16) / 255.0;
            double b = Convert.ToInt32(hex.Substring(5, 2), 16) / 255.0;
            return $"{r:F5},{g:F5},{b:F5}";
        }
        catch { return null; }
    }
}
