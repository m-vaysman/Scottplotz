using System.Net.Sockets;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Scottplotz.Charting;
using Scottplotz.Hosting;

namespace Scottplotz.Tray;

static class Program
{
    private const int FallbackPort = 5240;

    [STAThread]
    static void Main(string[] args)
    {
        var exePath = Environment.ProcessPath ?? string.Empty;
        if (args.Any(arg => string.Equals(arg, "--clear-startup", StringComparison.Ordinal)))
        {
            StartupStore.SetEnabled(false, exePath);
            return;
        }

        using var instance = SingleInstance.TryAcquire();
        if (instance is null)
            return;

        ApplicationConfiguration.Initialize();
        using var session = new TraySession(args, exePath);
        Application.Run();
        session.Stop();
    }
}

sealed class TraySession : IDisposable
{
    private const int FallbackPort = 5240;

    private readonly string[] _args;
    private readonly string _exePath;
    private readonly NotifyIcon _icon;
    private readonly System.Windows.Forms.Timer _start;
    private string _page = TrayLinks.TestPage(FallbackPort);
    private WebApplication? _app;
    private Task? _host;

    public TraySession(string[] args, string exePath)
    {
        _args = args;
        _exePath = exePath;

        var menu = new ContextMenuStrip();
        menu.Items.Add("Open test page", null, (_, _) => TrayLinks.Open(_page));
        menu.Items.Add("Open logs folder", null, (_, _) => TrayLinks.OpenFolder(UserPaths.LogsFolder));
        var startup = new ToolStripMenuItem("Start with Windows")
        {
            Checked = StartupStore.IsEnabled(),
            CheckOnClick = true
        };
        startup.Click += (_, _) => StartupStore.SetEnabled(startup.Checked, _exePath);
        menu.Items.Add(startup);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Quit", null, (_, _) => Application.Exit());

        _icon = new NotifyIcon
        {
            Icon = Icon.ExtractAssociatedIcon(exePath) ?? SystemIcons.Application,
            Visible = true,
            Text = "Scottplotz",
            ContextMenuStrip = menu
        };

        _start = new System.Windows.Forms.Timer { Interval = 50 };
        _start.Tick += (_, _) =>
        {
            _start.Stop();
            _host = Task.Run(StartHost);
        };
        _start.Start();
    }

    public void Stop()
    {
        if (_app is null)
            return;

        try
        {
            _app.StopAsync().GetAwaiter().GetResult();
            _app.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
        catch (Exception)
        {
        }

        _host?.Wait(TimeSpan.FromSeconds(5));
    }

    public void Dispose()
    {
        _start.Dispose();
        _icon.Visible = false;
        _icon.Dispose();
        _icon.ContextMenuStrip?.Dispose();
    }

    private void StartHost()
    {
        try
        {
            var app = ScottplotzHost.Create(_args);
            _app = app;
            _page = TrayLinks.TestPage(app.Services.GetRequiredService<ChartingLimits>().Port);
            app.StartAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            var port = _app is null ? FallbackPort : _app.Services.GetRequiredService<ChartingLimits>().Port;
            var message = AddressInUse(ex)
                ? $"Port {port} is already in use."
                : "Scottplotz could not start. " + ex.Message;
            _icon.ShowBalloonTip(8000, "Scottplotz", message, ToolTipIcon.Warning);
        }
    }

    private static bool AddressInUse(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is SocketException { SocketErrorCode: SocketError.AddressAlreadyInUse })
                return true;
        }

        return false;
    }
}

static class SingleInstance
{
    public static IDisposable? TryAcquire()
    {
        var mutex = new Mutex(initiallyOwned: false, name: @"Local\Scottplotz");
        try
        {
            if (!mutex.WaitOne(0))
            {
                mutex.Dispose();
                return null;
            }
        }
        catch (AbandonedMutexException)
        {
        }

        return mutex;
    }
}

public static class TrayLinks
{
    public static string TestPage(int port) => $"http://localhost:{port}/";

    public static void Open(string target) =>
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(target) { UseShellExecute = true });

    public static void OpenFolder(string folder)
    {
        Directory.CreateDirectory(folder);
        Open(folder);
    }
}
