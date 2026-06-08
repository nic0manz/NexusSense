using System.Runtime.InteropServices;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Win32;
using HidApi;

namespace NexusSense;

public class App
{
    // Debug mode: when true console messages are shown.
    public static bool Debug { get; private set; } = false;

    // Win32: hide/show the console window attached to this process.
    [DllImport("kernel32.dll")] private static extern IntPtr GetConsoleWindow();
    [DllImport("user32.dll")]   private static extern bool  ShowWindow(IntPtr hWnd, int nCmdShow);
    private const int SW_HIDE = 0;

    private static Mutex? _mutex;

    public static int Main(string[] argv)
    {
        // Ensure only one instance is running.
        _mutex = new Mutex(true, "NexusSense_SingleInstance", out bool isNew);
        if (!isNew)
        {
            MessageBox.Show("NexusSense is already running.", "NexusSense",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return 1;
        }

        bool   sensors = false;
        bool   help    = false;
        string? gifPath = null;

        for (int i = 0; i < argv.Length; i++)
        {
            if (argv[i] is "-d" or "--debug")   Debug   = true;
            if (argv[i] is "-s" or "--sensors") sensors = true;
            if (argv[i] is "-h" or "--help")    help    = true;
            if ((argv[i] is "-g" or "--gif") && i + 1 < argv.Length)
                gifPath = argv[++i];
        }

        if (help)    { PrintHelp(); return 0; }
        if (sensors) { PrintSensors(); return 0; }
        if (gifPath  != null) { PlayGif(gifPath); return 0; }

        // Hide the console window unless debug mode is active.
        if (!Debug)
        {
            var con = GetConsoleWindow();
            if (con != IntPtr.Zero) ShowWindow(con, SW_HIDE);
        }

        return RunMonitorWithTray();
    }

    private static int RunMonitorWithTray()
    {
        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.EnableVisualStyles();

        var cts = new CancellationTokenSource();

        // Build tray icon.
        using var trayIcon = new NotifyIcon
        {
            Icon    = CreateIcon(),
            Text    = "NexusSense",
            Visible = true,
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add("Exit", null, (_, _) =>
        {
            TurnOffScreen();
            cts.Cancel();
            Application.Exit();
        });
        trayIcon.ContextMenuStrip = menu;

        // Turn off the screen on Windows shutdown/logoff.
        SystemEvents.SessionEnding += (_, _) => TurnOffScreen();

        // Double-click on tray icon → show a brief tooltip.
        trayIcon.DoubleClick += (_, _) =>
            trayIcon.ShowBalloonTip(2000, "NexusSense", "Monitor is running.", ToolTipIcon.Info);

        // Run the monitor on a background thread.
        var monitorThread = new Thread(() => MonitorLoop(cts.Token)) { IsBackground = true };
        monitorThread.Start();

        // WinForms message loop keeps the tray icon alive.
        Application.Run();

        trayIcon.Visible = false;
        return 0;
    }

    /// <summary>Sets brightness to 0 on all connected Nexus devices.</summary>
    private static void TurnOffScreen()
    {
        try
        {
            foreach (DeviceInfo deviceInfo in Hid.Enumerate())
            {
                if (deviceInfo.VendorId  != 0x1b1c ||
                    deviceInfo.ProductId != 0x1b8e ||
                    deviceInfo.UsagePage != 12)
                    continue;

                using Device device = deviceInfo.ConnectToDevice();
                var nexus = new Nexus(device);
                nexus.UploadImage(new byte[640 * 48 * 4]);
                nexus.SetBrightness(0);
            }
        }
        catch { }
    }

    private static void MonitorLoop(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                bool found = false;
                try
                {
                    foreach (DeviceInfo deviceInfo in Hid.Enumerate())
                    {
                        if (deviceInfo.VendorId  != 0x1b1c ||
                            deviceInfo.ProductId != 0x1b8e ||
                            deviceInfo.UsagePage != 12)
                            continue;

                        found = true;
                        using Device device = deviceInfo.ConnectToDevice();
                        new NexusMonitor(new Nexus(device)).RunLive(1000, ct);
                    }
                }
                catch (Exception ex)
                {
                    if (Debug) Console.WriteLine(ex.Message);
                }

                if (!found && Debug) Console.WriteLine("Nexus not found, retrying in 5s...");
                Thread.Sleep(5000);
            }
        }
        finally
        {
            Hid.Exit();
        }
    }

    private static void PlayGif(string path)
    {
        if (!File.Exists(path)) { Console.WriteLine($"File not found: {path}"); return; }

        try
        {
            foreach (DeviceInfo deviceInfo in Hid.Enumerate())
            {
                if (deviceInfo.VendorId  != 0x1b1c ||
                    deviceInfo.ProductId != 0x1b8e ||
                    deviceInfo.UsagePage != 12)
                    continue;

                using Device device = deviceInfo.ConnectToDevice();
                var nexus   = new Nexus(device);
                nexus.SetBrightness(100);
                var monitor = new NexusMonitor(nexus);
                monitor.PlayGifOnce(path);
                return;
            }
            Console.WriteLine("Nexus device not found.");
        }
        catch (Exception ex) { Console.WriteLine(ex.Message); }
        finally { Hid.Exit(); }
    }

    private static void PrintSensors()
    {
        Console.WriteLine("Available Afterburner sensors:");
        AfterburnerReader.PrintAll();
    }

    private static void PrintHelp()
    {
        Console.WriteLine("NexusSense — Afterburner monitor for Corsair iCUE Nexus Companion");
        Console.WriteLine();
        Console.WriteLine("  -d  --debug     Show console messages");
        Console.WriteLine("  -s  --sensors   List all Afterburner sensors with ID");
        Console.WriteLine("  -g  --gif PATH  Play a GIF on the Nexus screen and exit");
        Console.WriteLine("  -h  --help      Show this help");
        Console.WriteLine();
        Console.WriteLine("Double-click the exe to start. Minimizes to system tray.");
    }

    /// <summary>Creates a simple 16x16 tray icon with a cyan 'N' on dark background.</summary>
    private static Icon CreateIcon()
    {
        using var bmp = new Bitmap(16, 16);
        using var g   = System.Drawing.Graphics.FromImage(bmp);
        g.Clear(Color.FromArgb(20, 20, 20));
        using var font  = new Font("Arial", 9, FontStyle.Bold, GraphicsUnit.Pixel);
        using var brush = new SolidBrush(Color.FromArgb(0, 200, 255));
        g.DrawString("N", font, brush, 1f, 1f);
        return Icon.FromHandle(bmp.GetHicon());
    }
}
