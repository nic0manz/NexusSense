using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace NexusSense;

/// <summary>
/// Defines a color threshold for value-based color selection in a sensor panel.
/// Colors are applied in ascending <see cref="UpTo"/> order; the first range whose
/// <see cref="UpTo"/> is greater than or equal to the current value is used.
/// </summary>
public class ColorRange
{
    /// <summary>Upper bound (inclusive) of the sensor value that triggers this color.</summary>
    public float  UpTo     { get; set; } = 999;

    /// <summary>HTML hex color string for lit LEDs / value text in this range.</summary>
    public string Color    { get; set; } = "#ffffff";

    /// <summary>HTML hex color string for unlit LEDs in this range (used when DynamicColor affects the graph).</summary>
    public string OffColor { get; set; } = "#1a1a1a";
}

/// <summary>
/// Configuration for a single sensor panel rendered on the LCD display.
/// Panels are arranged in a 2-row grid; column position is derived from the panel's index on the current page.
/// </summary>
public class MonitorItem
{
    /// <summary>MSI Afterburner sensor name (e.g. <c>"GPU temperature"</c>). Takes priority over <see cref="SensorId"/> when both are set.</summary>
    public string           SensorName  { get; set; } = "";

    /// <summary>Zero-based sensor index from the MAHMSharedMemory entry list. Used when <see cref="SensorName"/> is empty.</summary>
    public int?             SensorId    { get; set; } = null;

    /// <summary>Short label shown in the top-left of the panel.</summary>
    public string           Label       { get; set; } = "";

    /// <summary>Left pixel offset of the panel within the 640-px display (legacy field; computed automatically in the monitor).</summary>
    public int              X           { get; set; } = 0;

    /// <summary>Panel width in pixels.</summary>
    public int              Width       { get; set; } = 106;

    /// <summary>Label X offset in pixels relative to panel left edge.</summary>
    public int              LabelX      { get; set; } = 2;
    /// <summary>Label Y offset in pixels relative to panel top edge.</summary>
    public int              LabelY      { get; set; } = 1;
    /// <summary>Value right-edge X position in pixels from the panel left edge. Text is right-aligned and grows leftward.</summary>
    public int              ValueX      { get; set; } = 104;
    /// <summary>Value Y offset in pixels relative to panel top edge.</summary>
    public int              ValueY      { get; set; } = 1;
    /// <summary>Graph (LED bar) X offset in pixels relative to panel left edge.</summary>
    public int              GraphX      { get; set; } = 0;
    /// <summary>Graph (LED bar) Y offset in pixels relative to panel top edge.</summary>
    public int              GraphY      { get; set; } = 16;

    /// <summary>Font point size for the sensor value text.</summary>
    public int              ValueSize   { get; set; } = 10;

    /// <summary>Font point size for the label text.</summary>
    public int              LabelSize   { get; set; } = 9;

    /// <summary>Default value/LED bar color as an HTML hex string. Overridden by <see cref="ColorRanges"/> when populated.</summary>
    public string           Color       { get; set; } = "#00ff00";

    /// <summary>
    /// .NET composite format string for the displayed value (e.g. <c>"{0:F1}GB"</c>).
    /// The sole argument is the sensor value after applying <see cref="Divisor"/>.
    /// </summary>
    public string           Format      { get; set; } = "{0:F0}";

    /// <summary>Sensor value that maps to zero filled LEDs on the bar graph.</summary>
    public float            GraphMin    { get; set; } = 0;

    /// <summary>Sensor value that maps to all LEDs filled on the bar graph.</summary>
    public float            GraphMax    { get; set; } = 100;

    /// <summary>Divide the raw sensor value by this before display and graph calculation (e.g. 1024 to convert MB → GB).</summary>
    public float            Divisor     { get; set; } = 1;

    /// <summary>When true, the label is rendered with the bold font defined in <see cref="MonitorConfig.BoldFont"/>.</summary>
    public bool             LabelBold   { get; set; } = false;

    /// <summary>When true, the value is rendered with the bold font defined in <see cref="MonitorConfig.BoldFont"/>.</summary>
    public bool             ValueBold   { get; set; } = false;

    /// <summary>
    /// Controls which elements change color according to <see cref="ColorRanges"/>:
    /// <list type="bullet">
    ///   <item><c>"none"</c>  — neither value text nor LED bar changes color (default).</item>
    ///   <item><c>"value"</c> — only the value text changes color.</item>
    ///   <item><c>"graph"</c> — only the LED bar changes color.</item>
    ///   <item><c>"both"</c>  — both value text and LED bar change color.</item>
    /// </list>
    /// </summary>
    public string           DynamicColor      { get; set; } = "none";

    /// <summary>
    /// Optional list of value thresholds mapped to colors, evaluated in ascending <see cref="ColorRange.UpTo"/> order.
    /// When empty, <see cref="Color"/> is used for all values.
    /// </summary>
    public List<ColorRange> ColorRanges { get; set; } = [];
}

/// <summary>
/// Per-page visual overrides. Any null field falls back to the global <see cref="MonitorConfig"/> value.
/// </summary>
public class PageConfig
{
    public int     Page             { get; set; } = 0;
    public string? Background       { get; set; }
    public string? LabelColor       { get; set; }
    public string? ValueColor       { get; set; }
    public string? LedColor         { get; set; }
    public string? PanelBorder      { get; set; }
    public string? LedOffColor      { get; set; }
    public int?    LedCount         { get; set; }
    public int?    LedWidth         { get; set; }
    public int?    LedGap           { get; set; }
    public int?    LedHeight        { get; set; }
    public string? LedStyle         { get; set; }
    public bool?   LedGlow          { get; set; }
    public bool?   LedHighlight     { get; set; }
    public float?  LedGradientTop   { get; set; }
    public float?  LedGradientMid   { get; set; }
    public float?  LedGradientBottom{ get; set; }

    /// <summary>Sensor panels displayed on this page.</summary>
    public List<MonitorItem> Items   { get; set; } = [];
}

/// <summary>
/// Top-level configuration for <see cref="NexusMonitor"/>, loaded from <c>nexus_config.json</c>.
/// </summary>
public class MonitorConfig
{
    /// <summary>Sensor poll interval in milliseconds.</summary>
    public int               IntervalMs  { get; set; } = 1000;

    /// <summary>
    /// How page navigation gestures work on the LCD touchstrip.
    /// <list type="bullet">
    ///   <item><c>"swipe"</c>  — swipe left/right only.</item>
    ///   <item><c>"tap"</c>    — tap the left/right edge zones only.</item>
    ///   <item><c>"both"</c>   — swipe or tap (default).</item>
    /// </list>
    /// </summary>
    public string            PageNavigation  { get; set; } = "both";

    /// <summary>Width in pixels of the left/right tap zones used when <see cref="PageNavigation"/> is "tap" or "both". Default 128 (20% of 640).</summary>
    public int               TapZoneWidth    { get; set; } = 128;

    /// <summary>LCD brightness (0–100). Null = leave unchanged.</summary>
    public int?              Brightness  { get; set; } = null;

    /// <summary>Path to a regular-weight TrueType font file. Null uses the ImageMagick default font.</summary>
    public string?           Font        { get; set; } = null;

    /// <summary>Path to a bold TrueType font file used when <see cref="MonitorItem.LabelBold"/> or <see cref="MonitorItem.ValueBold"/> is set.</summary>
    public string?           BoldFont    { get; set; } = null;

    /// <summary>Pages, each with their own visual settings and sensor items.</summary>
    public List<PageConfig>  Pages       { get; set; } = [];
}

/// <summary>
/// Renders live MSI Afterburner sensor data onto the K100 LCD at a configurable interval.
/// The display is a 640×48 px frame split into a 2-row grid of sensor panels.
/// Touch/swipe gestures on the LCD strip navigate between pages of panels.
/// </summary>
public class NexusMonitor
{
    private readonly Nexus? _nexus;
    private MonitorConfig   _config;

    private static string CONFIG_FILE = "nexus_config.json";

    // Current page index; written by the touch thread, read by the render thread.
    private volatile int _currentPage = 0;

    // Reused 640×48×4 RGBA buffer — allocated once to avoid 120 KB of GC pressure per frame.
    private readonly byte[] _rawBuffer = new byte[640 * 48 * 4];

    // Cache parsed Color values so ParseColor() never re-parses the same hex string twice.
    private readonly Dictionary<string, Color> _colorCache = new();

    // Font cache: (ttf-path-or-empty, point-size, bold) → Font object.
    private readonly Dictionary<(string, float, bool), Font> _fontCache = new();

    // Loaded TTF families from config paths (PrivateFontCollection keeps them alive).
    private readonly PrivateFontCollection               _pfc         = new();
    private readonly Dictionary<string, FontFamily>      _fontFamilies = new();

    // Tracks the last-modified timestamp of the config file so we only re-parse when it changes.
    private DateTime _configLastWrite = DateTime.MinValue;

    /// <summary>Creates a <see cref="NexusMonitor"/> bound to an open <see cref="Nexus"/> device handle.</summary>
    public NexusMonitor(Nexus? nexus = null)
    {
        _nexus  = nexus;
        _config = LoadConfig();
    }

    /// <summary>Returns the effective visual settings for a given page, applying per-page overrides on top of globals.</summary>
    private PageConfig ResolvePageConfig(int page)
    {
        var p = _config.Pages.FirstOrDefault(p => p.Page == page);
        return new PageConfig
        {
            Page              = page,
            Background        = p?.Background        ?? "#000000",
            LabelColor        = p?.LabelColor        ?? "#aaaaaa",
            ValueColor        = p?.ValueColor        ?? "#aaaaaa",
            LedColor          = p?.LedColor          ?? "#ff3000",
            PanelBorder       = p?.PanelBorder       ?? "#111111",
            LedOffColor       = p?.LedOffColor       ?? "#1a1a1a",
            LedCount          = p?.LedCount          ?? 14,
            LedWidth          = p?.LedWidth          ?? 5,
            LedGap            = p?.LedGap            ?? 2,
            LedHeight         = p?.LedHeight         ?? 7,
            LedStyle          = p?.LedStyle          ?? "bar",
            LedGlow           = p?.LedGlow           ?? false,
            LedHighlight      = p?.LedHighlight      ?? false,
            LedGradientTop    = p?.LedGradientTop    ?? 0f,
            LedGradientMid    = p?.LedGradientMid    ?? 0f,
            LedGradientBottom = p?.LedGradientBottom ?? 0f,
            Items             = p?.Items             ?? [],
        };
    }

    // -------------------------------------------------------------------------
    // Configuration
    // -------------------------------------------------------------------------

    /// <summary>
    /// Loads <see cref="MonitorConfig"/> from <c>nexus_config.json</c>.
    /// If the file is missing or malformed, a default config is written and returned.
    /// </summary>
    private MonitorConfig LoadConfig()
    {
        if (File.Exists(CONFIG_FILE))
        {
            try
            {
                var json = File.ReadAllText(CONFIG_FILE);
                var cfg  = JsonSerializer.Deserialize<MonitorConfig>(json) ?? DefaultConfig();

                // Pre-sort ColorRanges once on load so GetColor() can iterate without OrderBy().
                foreach (var pg in cfg.Pages)
                    foreach (var item in pg.Items)
                        item.ColorRanges.Sort((a, b) => a.UpTo.CompareTo(b.UpTo));

                return cfg;
            }
            catch
            {
                Console.WriteLine("[WARN] Invalid config JSON, using defaults.");
            }
        }

        var defaultCfg = DefaultConfig();
        SaveConfig(defaultCfg);
        Console.WriteLine($"[OK] Config created: {CONFIG_FILE}");
        return defaultCfg;
    }

    /// <summary>
    /// Reloads <see cref="_config"/> only when the config file has been modified since the last load.
    /// Invalidates <see cref="_colorCache"/> on reload so stale colors are not reused.
    /// </summary>
    private void ReloadConfigIfChanged()
    {
        if (!File.Exists(CONFIG_FILE)) return;
        DateTime lastWrite = File.GetLastWriteTimeUtc(CONFIG_FILE);
        if (lastWrite <= _configLastWrite) return;

        _configLastWrite = lastWrite;
        _config          = LoadConfig();
        _colorCache.Clear();                          // Colors may have changed.
        foreach (var f in _fontCache.Values) f.Dispose();
        _fontCache.Clear();                           // Font paths/sizes may have changed.
        if (App.Debug) Console.WriteLine("[Config] Reloaded.");
    }

    private void SaveConfig(MonitorConfig cfg)
    {
        var opts = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(CONFIG_FILE, JsonSerializer.Serialize(cfg, opts));
    }

    /// <summary>
    /// Returns a built-in default configuration with six common hardware sensors
    /// (GPU temp, GPU load, CPU temp, CPU load, RAM usage, VRAM usage).
    /// </summary>
    private MonitorConfig DefaultConfig() => new()
    {
        IntervalMs     = 500,
        PageNavigation = "both",
        TapZoneWidth   = 128,
        Brightness     = 100,
        Font           = @"C:\Windows\Fonts\segoeui.ttf",
        BoldFont       = @"C:\Windows\Fonts\segoeuib.ttf",
        Pages =
        [
            new()
            {
                Page = 0,
                Background="#000000", LabelColor="#aaaaaa", ValueColor="#aaaaaa",
                LedColor="#ffffff", PanelBorder="#111111", LedOffColor="#222222",
                LedCount=14, LedWidth=5, LedGap=2, LedHeight=16,
                LedStyle="bar", LedGlow=true, LedHighlight=false,
                LedGradientTop=0f, LedGradientMid=0f, LedGradientBottom=0f,
                Items =
                [
                    new() { SensorName="CPU temperature", Label="CPU °C", ValueSize=13, LabelSize=13, LabelBold=true, ValueBold=true,
                        Format="{0:F0}°", GraphMin=0, GraphMax=100, Divisor=1, DynamicColor="both",
                        LabelX=2, LabelY=2, ValueX=104, ValueY=2, GraphX=5, GraphY=47,
                        ColorRanges = [ new() { UpTo=60, Color="#00cc44", OffColor="#002210" }, new() { UpTo=80, Color="#ffcc00", OffColor="#1a1200" }, new() { UpTo=100, Color="#cc2200", OffColor="#1a0400" } ]
                    },
                    new() { SensorName="CPU usage",       Label="CPU %", ValueSize=13, LabelSize=13, LabelBold=true, ValueBold=true,
                        Format="{0:F0}%", GraphMin=0, GraphMax=100, Divisor=1, DynamicColor="both",
                        LabelX=2, LabelY=2, ValueX=104, ValueY=2, GraphX=5, GraphY=47,
                        ColorRanges = [ new() { UpTo=50, Color="#00cc44", OffColor="#002210" }, new() { UpTo=80, Color="#ffcc00", OffColor="#1a1200" }, new() { UpTo=100, Color="#cc2200", OffColor="#1a0400" } ]
                    },
                    new() { SensorName="GPU temperature", Label="GPU °C", ValueSize=13, LabelSize=13, LabelBold=true, ValueBold=true,
                        Format="{0:F0}°", GraphMin=0, GraphMax=100, Divisor=1, DynamicColor="both",
                        LabelX=2, LabelY=2, ValueX=104, ValueY=2, GraphX=5, GraphY=47,
                        ColorRanges = [ new() { UpTo=60, Color="#00cc44", OffColor="#002210" }, new() { UpTo=80, Color="#ffcc00", OffColor="#1a1200" }, new() { UpTo=100, Color="#cc2200", OffColor="#1a0400" } ]
                    },
                    new() { SensorName="GPU usage",       Label="GPU %", ValueSize=13, LabelSize=13, LabelBold=true, ValueBold=true,
                        Format="{0:F0}%", GraphMin=0, GraphMax=100, Divisor=1, DynamicColor="both",
                        LabelX=2, LabelY=2, ValueX=104, ValueY=2, GraphX=5, GraphY=47,
                        ColorRanges = [ new() { UpTo=50, Color="#00cc44", OffColor="#002210" }, new() { UpTo=80, Color="#ffcc00", OffColor="#1a1200" }, new() { UpTo=100, Color="#cc2200", OffColor="#1a0400" } ]
                    },
                    new() { SensorName="RAM usage",    Label="RAM",  ValueSize=13, LabelSize=13, LabelBold=true, ValueBold=true,
                        Format="{0:F1}GB", GraphMin=0, GraphMax=32, Divisor=1024, DynamicColor="both",
                        LabelX=2, LabelY=2, ValueX=104, ValueY=2, GraphX=5, GraphY=47,
                        ColorRanges = [ new() { UpTo=16, Color="#00cc44", OffColor="#002210" }, new() { UpTo=24, Color="#ffcc00", OffColor="#1a1200" }, new() { UpTo=32, Color="#cc2200", OffColor="#1a0400" } ]
                    },
                    new() { SensorName="Memory usage", Label="VRAM", ValueSize=13, LabelSize=13, LabelBold=true, ValueBold=true,
                        Format="{0:F1}GB", GraphMin=0, GraphMax=16, Divisor=1024, DynamicColor="both",
                        LabelX=2, LabelY=2, ValueX=104, ValueY=2, GraphX=5, GraphY=47,
                        ColorRanges = [ new() { UpTo=8, Color="#00cc44", OffColor="#002210" }, new() { UpTo=12, Color="#ffcc00", OffColor="#1a1200" }, new() { UpTo=16, Color="#cc2200", OffColor="#1a0400" } ]
                    },
                ]
            }
        ]
    };

    // -------------------------------------------------------------------------
    // Main loop
    // -------------------------------------------------------------------------

    /// <summary>
    /// Starts the live monitor loop. Reloads config, polls Afterburner, renders a frame, and uploads
    /// it to the LCD every <see cref="MonitorConfig.IntervalMs"/> milliseconds.
    /// A background thread concurrently polls the touchstrip for page-navigation gestures.
    /// Blocks until <paramref name="ct"/> is cancelled (e.g. via Ctrl+C).
    /// </summary>
    public void RunLive(int intervalMs = 1000, CancellationToken ct = default)
    {
        if (App.Debug) Console.WriteLine("Live monitor started.");

        // Start touch-input handling on a daemon thread so it does not block shutdown.
        var touchThread = new Thread(() => TouchLoop(ct)) { IsBackground = true };
        touchThread.Start();

        int? lastBrightness = null;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                // Reload config only when the file has changed on disk.
                ReloadConfigIfChanged();

                // Apply brightness only when it changes to avoid redundant HID writes.
                if (_config.Brightness.HasValue && _config.Brightness != lastBrightness)
                {
                    _nexus.SetBrightness(_config.Brightness.Value);
                    lastBrightness = _config.Brightness;
                }

                var sensors = AfterburnerReader.ReadAll();
                // Render directly to raw RGBA and upload — no temp file involved.
                byte[] raw = RenderFrame(sensors, _currentPage);
                _nexus.UploadImage(raw);
            }
            catch (Exception ex)
            {
                if (App.Debug) Console.WriteLine($"Frame error: {ex.Message}");
            }

            Thread.Sleep(_config.IntervalMs);
        }

        if (App.Debug) Console.WriteLine("Monitor stopped.");
    }

    // -------------------------------------------------------------------------
    // Touch input
    // -------------------------------------------------------------------------

    /// <summary>
    /// Background loop that polls the LCD touchstrip and updates <see cref="_currentPage"/>
    /// in response to swipe gestures or left/right tap zones.
    /// <list type="bullet">
    ///   <item>Swipe right (<c>"-&gt;"</c>) or tap on the left 20% → previous page.</item>
    ///   <item>Swipe left (<c>"&lt;-"</c>) or tap on the right 20% → next page.</item>
    /// </list>
    /// </summary>
    private void TouchLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                string touch   = _nexus!.WaitForTouchResult(1);
                int    maxPage = _config.Pages.Select(p => p.Page).DefaultIfEmpty(0).Max();

                bool allowSwipe = _config.PageNavigation is "swipe" or "both";
                bool allowTap   = _config.PageNavigation is "tap"   or "both";
                int  zoneW      = _config.TapZoneWidth;

                bool prevPage = allowSwipe && touch == "->";
                bool nextPage = allowSwipe && touch == "<-";

                // Tap zones: left edge → previous page, right edge → next page.
                // Only "++ X" (stationary tap, displacement ≤50px) is accepted to avoid
                // confusing swipe gestures with zone taps.
                if (allowTap && touch.StartsWith("++ ") &&
                    int.TryParse(touch[3..], out int tapX))
                {
                    if      (tapX < zoneW)           prevPage = true;   // left zone
                    else if (tapX > (640 - zoneW))   nextPage = true;   // right zone
                }

                if (prevPage)
                {
                    _currentPage = Math.Max(_currentPage - 1, 0);
                    if (App.Debug) Console.WriteLine($"Page {_currentPage}");
                }
                else if (nextPage)
                {
                    _currentPage = Math.Min(_currentPage + 1, maxPage);
                    if (App.Debug) Console.WriteLine($"Page {_currentPage}");
                }
            }
            catch
            {
                // Swallow all exceptions: a transient HID read error should not crash the touch thread.
            }
        }
    }

    // -------------------------------------------------------------------------
    // Rendering
    // -------------------------------------------------------------------------

    /// <summary>Renders a frame to a raw RGBA byte array for LCD upload.</summary>
    private byte[] RenderFrame(List<AfterburnerReader.SensorData> sensors, int page)
    {
        using var bmp = RenderToBitmap(sensors, page);
        int        totalBytes = 640 * 48 * 4;
        BitmapData bmpData    = bmp.LockBits(new Rectangle(0, 0, 640, 48), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try   { Marshal.Copy(bmpData.Scan0, _rawBuffer, 0, totalBytes); }
        finally { bmp.UnlockBits(bmpData); }
        return _rawBuffer;
    }

    /// <summary>
    /// Renders all sensor panels for the given <paramref name="page"/> into a 640×48 <see cref="Bitmap"/>.
    /// Caller is responsible for disposing the returned bitmap.
    /// </summary>
    private Bitmap RenderToBitmap(List<AfterburnerReader.SensorData> sensors, int page)
    {
        var bmp = new Bitmap(640, 48, PixelFormat.Format32bppArgb);
        using var g   = Graphics.FromImage(bmp);

        g.SmoothingMode       = SmoothingMode.None;
        g.TextRenderingHint   = TextRenderingHint.ClearTypeGridFit;
        g.CompositingQuality  = CompositingQuality.HighQuality;
        g.InterpolationMode   = InterpolationMode.HighQualityBicubic;
        // Resolve per-page settings (falls back to globals if not overridden).
        var pc = ResolvePageConfig(page);

        g.CompositingMode     = CompositingMode.SourceCopy;
        g.Clear(ParseColor(pc.Background!));
        g.CompositingMode     = CompositingMode.SourceOver;

        int ledW     = pc.LedWidth!.Value;
        int ledGap   = pc.LedGap!.Value;
        int ledH     = pc.LedHeight!.Value;
        int ledCount = pc.LedCount!.Value;
        bool isBar   = pc.LedStyle == "bar";

        var pageItems = pc.Items;

        // Auto layout: 1 row (full 48px) when ≤6 items, 2 rows of 24px otherwise.
        int numRows = pageItems.Count <= 6 ? 1 : 2;
        int rowH    = 48 / numRows;
        int numCols = (pageItems.Count + numRows - 1) / numRows;
        int colW    = numCols > 0 ? 640 / numCols : 640;

        // Text area height — fixed per row, independent of LED bar height.
        int textAreaH = rowH - 2;

        // Reuse StringFormat objects for every panel.
        using var leftFmt  = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Near, FormatFlags = StringFormatFlags.NoWrap };
        using var rightFmt = new StringFormat { Alignment = StringAlignment.Far,  LineAlignment = StringAlignment.Near, FormatFlags = StringFormatFlags.NoWrap };

        // Colors and brushes that are the same for every panel.
        using var borderPen  = new Pen(ParseColor(pc.PanelBorder!));
        using var labelBrush = new SolidBrush(ParseColor(pc.LabelColor!));

        for (int idx = 0; idx < pageItems.Count; idx++)
        {
            var item   = pageItems[idx];
            int col    = idx / numRows;
            int row    = idx % numRows;
            int panelX = col * colW;
            int panelY = row * rowH;
            int panelW = colW;

            // Resolve sensor value: SensorName takes priority; fall back to SensorId.
            float value = (!string.IsNullOrEmpty(item.SensorName)
                ? sensors.FirstOrDefault(s => s.Name == item.SensorName)?.Value
                : item.SensorId.HasValue
                    ? sensors.FirstOrDefault(s => s.Id == item.SensorId.Value)?.Value
                    : null) ?? 0f;

            if (item.Divisor > 1) value /= item.Divisor;

            // Values above GraphMax are treated as unavailable.
            bool isNA = value > item.GraphMax;
            if (isNA) value = 0;

            Color activeColor = GetColor(item, value);
            bool  dynValue   = item.DynamicColor is "value" or "both";
            bool  dynGraph   = item.DynamicColor is "graph" or "both";
            // When dynamic: use ColorRanges color. When fixed: use global ValueColor / LedColor.
            Color valueColor = dynValue ? activeColor : ParseColor(pc.ValueColor!);
            Color ledColor   = dynGraph ? Color.Empty  : ParseColor(pc.LedColor!);

            // Panel border.
            g.DrawRectangle(borderPen, panelX, panelY, panelW - 2, rowH - 1);

            // Measure the value string first so the label gets all remaining space.
            string display   = isNA ? "N/A" : string.Format(item.Format, value);
            Font   valueFont = GetFont(item.ValueSize, item.ValueBold);
            float  valueW    = g.MeasureString(display, valueFont).Width;
            float  labelW    = Math.Max(0, panelW - 4 - valueW);

            float lblX = panelX + item.LabelX;
            float lblY = panelY + item.LabelY;
            float valY = panelY + item.ValueY;

            var labelRect = new RectangleF(lblX,   lblY, Math.Max(0, panelW - item.LabelX - 2), textAreaH);
            // ValueX = right edge from panel left: rect spans from panel left to ValueX, right-aligned.
            var valueRect = new RectangleF(panelX, valY, Math.Max(0, item.ValueX), textAreaH);

            // Label (left-aligned, label color).
            Font labelFont = GetFont(item.LabelSize, item.LabelBold);
            g.DrawString(item.Label, labelFont, labelBrush, labelRect, leftFmt);

            // Value (right-aligned).
            using var valueBrush = new SolidBrush(valueColor);
            g.DrawString(display, valueFont, valueBrush, valueRect, rightFmt);

            // GraphY = bottom edge of the LED bar (so increasing LedHeight grows upward).
            int   ledStartY = panelY + item.GraphY - ledH;
            float range     = item.GraphMax - item.GraphMin;
            float pct       = range > 0 ? (value - item.GraphMin) / range : 0f;
            int   litLeds   = Math.Clamp((int)Math.Round(pct * ledCount), 0, ledCount);
            int   totalLedW = ledCount * (ledW + ledGap) - ledGap;
            int   startX    = panelX + item.GraphX;

            // LED bar — lit segments get a vertical gradient + optional visual effects.
            // dynGraph=false → lit = global LedColor, unlit = global LedOffColor
            // dynGraph=true  → lit = positional ColorRange.Color, unlit = positional ColorRange.OffColor

            // Precompute gradient params (shared by both styles).
            float lighten    = pc.LedGradientTop!.Value;
            float midPos     = Math.Clamp(pc.LedGradientMid!.Value, 0.001f, 0.999f);
            float darkAmount = pc.LedGradientBottom!.Value;

            if (isBar)
            {
                // Single continuous bar: lit portion + unlit remainder.
                int litW = (int)Math.Round(pct * totalLedW);
                litW = Math.Clamp(litW, 0, totalLedW);

                // Unlit background (full bar first).
                Color offColor = dynGraph ? GetOffColor(item, item.GraphMax, pc.LedOffColor!) : ParseColor(pc.LedOffColor!);
                using var offBrush = new SolidBrush(offColor);
                g.FillRectangle(offBrush, startX, ledStartY, totalLedW, ledH);

                if (litW > 0)
                {
                    Color barColor = dynGraph ? GetColor(item, value) : ledColor;

                    if (pc.LedGlow!.Value)
                    {
                        using var glowBrush = new SolidBrush(Color.FromArgb(70, barColor));
                        g.FillRectangle(glowBrush, startX - 1, ledStartY - 1, litW + 2, ledH + 2);
                    }

                    Color topColor = Lighten(barColor, lighten);
                    Color botColor = Darken(barColor, darkAmount);
                    for (int py = 0; py < ledH; py++)
                    {
                        float t = ledH > 1 ? (float)py / (ledH - 1) : 0f;
                        Color rowColor = t < midPos
                            ? LerpColor(topColor, barColor, t / midPos)
                            : LerpColor(barColor, botColor, (t - midPos) / (1f - midPos));
                        using var rowBrush = new SolidBrush(rowColor);
                        g.FillRectangle(rowBrush, startX, ledStartY + py, litW, 1);
                    }

                    if (pc.LedHighlight!.Value)
                    {
                        using var hilite = new SolidBrush(Color.FromArgb(140, 255, 255, 255));
                        g.FillRectangle(hilite, startX, ledStartY, litW, 1);
                    }
                }
            }
            else
            {
                // Segmented LED bar.
                var ledRect = new Rectangle(0, ledStartY, ledW, ledH);

                for (int i = 0; i < ledCount; i++)
                {
                    int   lx       = startX + i * (ledW + ledGap);
                    ledRect.X = lx;
                    float posValue = item.GraphMin + ((float)(i + 1) / ledCount) * (item.GraphMax - item.GraphMin);

                    if (i < litLeds)
                    {
                        Color segColor = dynGraph ? GetColor(item, posValue) : ledColor;

                        if (pc.LedGlow!.Value)
                        {
                            using var glowBrush = new SolidBrush(Color.FromArgb(70, segColor));
                            g.FillRectangle(glowBrush, lx - 1, ledStartY - 1, ledW + 2, ledH + 2);
                        }

                        Color topColor = Lighten(segColor, lighten);
                        Color botColor = Darken(segColor, darkAmount);
                        for (int py = 0; py < ledH; py++)
                        {
                            float t = ledH > 1 ? (float)py / (ledH - 1) : 0f;
                            Color rowColor = t < midPos
                                ? LerpColor(topColor,  segColor, t / midPos)
                                : LerpColor(segColor,  botColor, (t - midPos) / (1f - midPos));
                            using var rowBrush = new SolidBrush(rowColor);
                            g.FillRectangle(rowBrush, lx, ledStartY + py, ledW, 1);
                        }

                        if (pc.LedHighlight!.Value)
                        {
                            using var hilite = new SolidBrush(Color.FromArgb(140, 255, 255, 255));
                            g.FillRectangle(hilite, lx, ledStartY, ledW, 1);
                        }
                    }
                    else
                    {
                        Color offColor = dynGraph ? GetOffColor(item, posValue, pc.LedOffColor!) : ParseColor(pc.LedOffColor!);
                        using var offBrush = new SolidBrush(offColor);
                        g.FillRectangle(offBrush, ledRect);
                    }
                }
            }
        }

        return bmp;
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Fills a rectangle with rounded corners using the given brush.
    /// The arc diameter is <c>radius * 2</c>; clamped so it never exceeds half the shorter side.
    /// </summary>
    /// <summary>
    /// Returns the unlit-LED color for a sensor value by walking <see cref="MonitorItem.ColorRanges"/>
    /// and picking the matching <see cref="ColorRange.OffColor"/>. Used when DynamicColor affects the graph.
    /// </summary>
    private Color GetOffColor(MonitorItem item, float value, string fallbackOffColor)
    {
        if (item.ColorRanges.Count > 0)
        {
            foreach (var range in item.ColorRanges)
                if (value <= range.UpTo)
                    return ParseColor(range.OffColor);
            return ParseColor(item.ColorRanges[^1].OffColor);
        }
        return ParseColor(fallbackOffColor);
    }

    /// <summary>Linearly interpolates between two colors. t=0 → a, t=1 → b.</summary>
    private static Color LerpColor(Color a, Color b, float t) => Color.FromArgb(
        (int)(a.R + (b.R - a.R) * t),
        (int)(a.G + (b.G - a.G) * t),
        (int)(a.B + (b.B - a.B) * t));

    /// <summary>Blends a color toward white by <paramref name="amount"/> (0 = unchanged, 1 = white).</summary>
    private static Color Lighten(Color c, float amount) => Color.FromArgb(c.A,
        Math.Min(255, (int)(c.R + (255 - c.R) * amount)),
        Math.Min(255, (int)(c.G + (255 - c.G) * amount)),
        Math.Min(255, (int)(c.B + (255 - c.B) * amount)));

    /// <summary>Blends a color toward black by <paramref name="amount"/> (0 = unchanged, 1 = black).</summary>
    private static Color Darken(Color c, float amount) => Color.FromArgb(c.A,
        (int)(c.R * (1 - amount)),
        (int)(c.G * (1 - amount)),
        (int)(c.B * (1 - amount)));

    /// <summary>
    /// Resolves the active <see cref="Color"/> for a sensor value by walking
    /// <see cref="MonitorItem.ColorRanges"/> (pre-sorted ascending by <see cref="ColorRange.UpTo"/>).
    /// Falls back to <see cref="MonitorItem.Color"/> when no ranges are defined.
    /// </summary>
    private Color GetColor(MonitorItem item, float value)
    {
        if (item.ColorRanges.Count > 0)
        {
            foreach (var range in item.ColorRanges)
                if (value <= range.UpTo)
                    return ParseColor(range.Color);

            return ParseColor(item.ColorRanges[^1].Color);
        }

        return ParseColor(item.Color);
    }

    /// <summary>
    /// Parses a 6-digit HTML hex color string (with or without <c>#</c>) to a <see cref="Color"/>,
    /// caching the result so each distinct string is parsed only once per config lifetime.
    /// </summary>
    private Color ParseColor(string hex)
    {
        if (_colorCache.TryGetValue(hex, out var cached)) return cached;
        string h = hex.TrimStart('#');
        byte r   = Convert.ToByte(h[0..2], 16);
        byte g   = Convert.ToByte(h[2..4], 16);
        byte b   = Convert.ToByte(h[4..6], 16);
        var  col = Color.FromArgb(255, r, g, b);
        _colorCache[hex] = col;
        return col;
    }

    /// <summary>
    /// Returns a cached <see cref="Font"/> loaded from the TTF paths configured in
    /// <see cref="MonitorConfig.Font"/> / <see cref="MonitorConfig.BoldFont"/>.
    /// Uses a <see cref="PrivateFontCollection"/> so custom fonts work without system installation.
    /// Falls back to the system default font when no path is configured.
    /// </summary>
    private Font GetFont(float size, bool bold)
    {
        string? path = (bold && _config.BoldFont != null) ? _config.BoldFont : _config.Font;
        var key = (path ?? "", size, bold);
        if (_fontCache.TryGetValue(key, out var cached)) return cached;

        FontFamily? family = null;
        if (path != null && File.Exists(path))
        {
            if (!_fontFamilies.TryGetValue(path, out family))
            {
                _pfc.AddFontFile(path);
                family = _pfc.Families[^1]; // newly added family is always last
                _fontFamilies[path] = family;
            }
        }

        // When loading a dedicated bold TTF (e.g. segoeuib.ttf), its internal style is Regular.
        var font = family != null
            ? new Font(family, size, FontStyle.Regular, GraphicsUnit.Pixel)
            : new Font(SystemFonts.DefaultFont.FontFamily, size,
                       bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel);

        _fontCache[key] = font;
        return font;
    }
}
