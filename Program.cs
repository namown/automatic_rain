using System;
using System.IO;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace AutomaticRain;

internal static class Program
{
    internal const string StopEventName = @"Local\AutomaticRain.Stop";

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern bool SetThreadPreferredUILanguages(uint flags, string languages, out uint count);

    [STAThread]
    private static void Main(string[] args)
    {
        // Keep application messages and native dialog resources in English.
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("en-US");
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
        SetThreadPreferredUILanguages(0x8, "en-US\0\0", out _);
        if (Array.Exists(args, arg => arg == "--stop"))
        {
            if (EventWaitHandle.TryOpenExisting(StopEventName, out var stop))
                using (stop) stop.Set();
            return;
        }
        Forms.Application.EnableVisualStyles();
        Forms.Application.SetCompatibleTextRenderingDefault(false);
        if (Array.Exists(args, arg => arg == "--install") ||
            string.Equals(Path.GetFileName(Environment.ProcessPath), "AutomaticRain-Setup.exe", StringComparison.OrdinalIgnoreCase))
        {
            using var setupMutex = new Mutex(true, @"Local\AutomaticRain.Setup", out bool firstSetup);
            if (!firstSetup) return;
            try { Forms.Application.Run(new SetupForm()); }
            finally { setupMutex.ReleaseMutex(); }
            return;
        }
        using var mutex = new Mutex(true, @"Local\AutomaticRain.Instance", out bool firstInstance);
        if (!firstInstance) return;
        try
        {
            var application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            using var rain = new RainApplication(application);
            application.Run();
        }
        catch (Exception exception) { RainApplication.Log("Startup error: " + exception); }
        finally { mutex.ReleaseMutex(); }
    }
}

internal sealed class Settings
{
    public string AudioFile { get; set; } = "";
    public double Volume { get; set; } = 0.5;
}

internal sealed class RainApplication : IDisposable
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private static readonly string DataDirectory = AppFiles.Root;
    private static readonly string SettingsPath = AppFiles.Settings;
    private readonly System.Drawing.Icon icon = AppFiles.LoadIcon();
    private readonly MediaPlayer player = new();
    private readonly Forms.NotifyIcon tray;
    private readonly Forms.ContextMenuStrip menu = new();
    private readonly Forms.ToolStripMenuItem status = new("Starting …") { Enabled = false };
    private readonly Forms.ToolStripMenuItem pause = new("Pause") { Enabled = false };
    private readonly Forms.ToolStripMenuItem startup = new("Start with Windows");
    private readonly DispatcherTimer retry = new() { Interval = TimeSpan.FromSeconds(15) };
    private readonly EventWaitHandle stopEvent = new(false, EventResetMode.ManualReset, Program.StopEventName);
    private readonly RegisteredWaitHandle stopRegistration;
    private readonly Settings settings;
    private bool paused;
    private bool loaded;
    private bool waitingForFile;
    private bool disposed;

    public RainApplication(Application application)
    {
        settings = LoadSettings();
        player.Volume = settings.Volume;
        player.MediaOpened += (_, _) =>
        {
            loaded = true;
            pause.Enabled = true;
            UpdatePlaybackStatus();
            Log("MP3 loaded: " + settings.AudioFile + "; Audio: " + player.HasAudio);
        };
        player.MediaEnded += (_, _) =>
        {
            player.Position = TimeSpan.Zero;
            if (!paused) player.Play();
            Log("Loop: MP3 restarted.");
        };
        player.MediaFailed += (_, args) =>
        {
            loaded = false;
            pause.Enabled = false;
            SetStatus("Playback error – select an MP3");
            Log("Playback error: " + args.ErrorException);
        };
        menu.Items.Add(status);
        menu.Items.Add(new Forms.ToolStripSeparator());
        pause.Click += (_, _) => TogglePause();
        menu.Items.Add(pause);
        menu.Items.Add("Select MP3 …", null, (_, _) => ChooseFile());
        var volume = new Forms.ToolStripMenuItem("Volume");
        foreach (int percent in new[] { 10, 25, 50, 75, 100 })
        {
            var item = new Forms.ToolStripMenuItem(percent + " %")
            {
                Checked = Math.Abs(settings.Volume - percent / 100.0) < 0.001
            };
            item.Click += (_, _) =>
            {
                settings.Volume = percent / 100.0;
                player.Volume = settings.Volume;
                foreach (Forms.ToolStripMenuItem sibling in volume.DropDownItems) sibling.Checked = false;
                item.Checked = true;
                SaveSettings();
            };
            volume.DropDownItems.Add(item);
        }
        menu.Items.Add(volume);
        startup.Click += (_, _) => ToggleStartup();
        menu.Items.Add(startup);
        menu.Opening += (_, _) => startup.Checked = IsStartupEnabled();
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => application.Shutdown());
        tray = new Forms.NotifyIcon
        {
            Icon = icon,
            Text = "Automatic Rain",
            ContextMenuStrip = menu,
            Visible = true
        };
        tray.DoubleClick += (_, _) => TogglePause();
        stopRegistration = ThreadPool.RegisterWaitForSingleObject(stopEvent,
            (_, _) => application.Dispatcher.BeginInvoke(new Action(application.Shutdown)),
            null, Timeout.Infinite, true);
        retry.Tick += (_, _) =>
        {
            if (waitingForFile && File.Exists(settings.AudioFile)) OpenAudio();
        };
        retry.Start();
        OpenAudio();
        if (string.IsNullOrWhiteSpace(settings.AudioFile))
            application.Dispatcher.BeginInvoke(new Action(ChooseFile));
        Log("Automatic Rain started.");
    }

    private void OpenAudio()
    {
        player.Close();
        loaded = false;
        paused = false;
        pause.Text = "Pause";
        pause.Enabled = false;
        waitingForFile = !File.Exists(settings.AudioFile);
        if (waitingForFile)
        {
            bool noSelection = string.IsNullOrWhiteSpace(settings.AudioFile);
            SetStatus(noSelection ? "Select an MP3 to begin" : "MP3 missing – select a file");
            Log(noSelection ? "No MP3 selected yet." : "File missing: " + settings.AudioFile);
            return;
        }
        try
        {
            SetStatus("Loading MP3 …");
            player.Open(new Uri(Path.GetFullPath(settings.AudioFile)));
            player.Volume = settings.Volume;
            player.Play();
        }
        catch (Exception exception)
        {
            SetStatus("Playback error – select an MP3");
            Log("Failed to open file: " + exception.Message);
        }
    }

    private void TogglePause()
    {
        if (!loaded) return;
        paused = !paused;
        if (paused) player.Pause(); else player.Play();
        UpdatePlaybackStatus();
        Log(paused ? "Paused." : "Playback resumed.");
    }

    private void UpdatePlaybackStatus()
    {
        pause.Text = paused ? "Resume" : "Pause";
        SetStatus(paused ? "Paused" : "Playing");
    }

    private void SetStatus(string text)
    {
        status.Text = text;
        tray.Text = "Automatic Rain: " + text;
    }

    private void ChooseFile()
    {
        using var dialog = new Forms.OpenFileDialog
        {
            Title = "Select an MP3 file",
            Filter = "MP3 files (*.mp3)|*.mp3",
            CheckFileExists = true,
            RestoreDirectory = true
        };
        if (dialog.ShowDialog() != Forms.DialogResult.OK) return;
        settings.AudioFile = dialog.FileName;
        SaveSettings();
        OpenAudio();
    }

    private static bool IsStartupEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return string.Equals(key?.GetValue("AutomaticRain") as string,
            '"' + Environment.ProcessPath + '"', StringComparison.OrdinalIgnoreCase);
    }

    private void ToggleStartup()
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            if (IsStartupEnabled()) key.DeleteValue("AutomaticRain", false);
            else key.SetValue("AutomaticRain", '"' + Environment.ProcessPath + '"');
            startup.Checked = IsStartupEnabled();
        }
        catch (Exception exception)
        {
            Log("Windows startup: " + exception.Message);
            tray.ShowBalloonTip(5000, "Automatic Rain", "Could not change the Windows startup setting.", Forms.ToolTipIcon.Warning);
        }
    }

    private static Settings LoadSettings()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var value = JsonSerializer.Deserialize<Settings>(File.ReadAllText(SettingsPath));
                if (value != null)
                {
                    value.AudioFile ??= "";
                    value.Volume = double.IsFinite(value.Volume) ? Math.Clamp(value.Volume, 0, 1) : 0.5;
                    return value;
                }
            }
        }
        catch (Exception exception) { Log("Settings: " + exception.Message); }
        return new Settings();
    }

    private void SaveSettings()
    {
        try
        {
            Directory.CreateDirectory(DataDirectory);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception exception)
        {
            Log("Failed to save settings: " + exception.Message);
            tray.ShowBalloonTip(5000, "Automatic Rain", "Could not save your settings.", Forms.ToolTipIcon.Warning);
        }
    }

    internal static void Log(string text)
    {
        try
        {
            Directory.CreateDirectory(DataDirectory);
            string path = Path.Combine(DataDirectory, "app.log");
            if (File.Exists(path) && new FileInfo(path).Length > 256_000) File.WriteAllText(path, "");
            File.AppendAllText(path, DateTimeOffset.Now.ToString("O") + " " + text + Environment.NewLine);
        }
        catch { /* Logging failures must never interrupt background playback. */ }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        retry.Stop();
        stopRegistration.Unregister(null);
        player.Close();
        tray.Visible = false;
        tray.Dispose();
        icon.Dispose();
        menu.Dispose();
        stopEvent.Dispose();
        Log("Automatic Rain exited.");
    }
}
