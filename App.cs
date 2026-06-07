using System.Runtime.InteropServices;
using System.Drawing;
using System.Windows.Forms;
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

    public static int Main(string[] argv)
    {
        bool sensors = false;
        bool help    = false;

        foreach (var arg in argv)
        {
            if (arg is "-d" or "--debug")   Debug   = true;
            if (arg is "-s" or "--sensors") sensors = true;
            if (arg is "-h" or "--help")    help    = true;
        }

        if (help)    { PrintHelp(); return 0; }
        if (sensors) { PrintSensors(); return 0; }

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
            cts.Cancel();
            Application.Exit();
        });
        trayIcon.ContextMenuStrip = menu;

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

    private static void MonitorLoop(CancellationToken ct)
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
                new NexusMonitor(new Nexus(device)).RunLive(1000, ct);
            }
        }
        catch (Exception ex)
        {
            if (Debug) Console.WriteLine(ex.Message);
        }
        finally
        {
            Hid.Exit();
        }
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
