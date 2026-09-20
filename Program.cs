using System;
using System.IO;
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

    [STAThread]
    private static void Main(string[] args)
    {
        if (Array.Exists(args, arg => arg == "--stop"))
        {
            if (EventWaitHandle.TryOpenExisting(StopEventName, out var stop))
                using (stop) stop.Set();
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
        catch (Exception exception) { RainApplication.Log("Startfehler: " + exception); }
        finally { mutex.ReleaseMutex(); }
    }
}

internal sealed class Settings
{
    public string AudioFile { get; set; } = @"C:\Rain\Rain\_01.mp3";
    public double Volume { get; set; } = 0.5;
}

internal sealed class RainApplication : IDisposable
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private static readonly string DataDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AutomaticRain");
    private static readonly string SettingsPath = Path.Combine(DataDirectory, "settings.json");
    private readonly MediaPlayer player = new();
    private readonly Forms.NotifyIcon tray;
    private readonly Forms.ContextMenuStrip menu = new();
    private readonly Forms.ToolStripMenuItem status = new("Wird gestartet …") { Enabled = false };
    private readonly Forms.ToolStripMenuItem pause = new("Pause") { Enabled = false };
    private readonly Forms.ToolStripMenuItem startup = new("Mit Windows starten");
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
            Log("MP3 geladen: " + settings.AudioFile + "; Audio: " + player.HasAudio);
        };
        player.MediaEnded += (_, _) =>
        {
            player.Position = TimeSpan.Zero;
            if (!paused) player.Play();
            Log("Endlosschleife: MP3 erneut gestartet.");
        };
        player.MediaFailed += (_, args) =>
        {
            loaded = false;
            pause.Enabled = false;
            SetStatus("Abspielfehler – MP3 auswählen");
            Log("Abspielfehler: " + args.ErrorException);
        };
        menu.Items.Add(status);
        menu.Items.Add(new Forms.ToolStripSeparator());
        pause.Click += (_, _) => TogglePause();
        menu.Items.Add(pause);
        menu.Items.Add("MP3 auswählen …", null, (_, _) => ChooseFile());
        var volume = new Forms.ToolStripMenuItem("Lautstärke");
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
        menu.Items.Add("Beenden", null, (_, _) => application.Shutdown());
        tray = new Forms.NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Information,
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
        Log("Automatic Rain gestartet.");
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
            SetStatus("MP3 fehlt – Datei auswählen");
            Log("Datei fehlt: " + settings.AudioFile);
            return;
        }
        try
        {
            SetStatus("MP3 wird geladen …");
            player.Open(new Uri(Path.GetFullPath(settings.AudioFile)));
            player.Volume = settings.Volume;
            player.Play();
        }
        catch (Exception exception)
        {
            SetStatus("Abspielfehler – MP3 auswählen");
            Log("Öffnen fehlgeschlagen: " + exception.Message);
        }
    }

    private void TogglePause()
    {
        if (!loaded) return;
        paused = !paused;
        if (paused) player.Pause(); else player.Play();
        UpdatePlaybackStatus();
        Log(paused ? "Pausiert." : "Wiedergabe fortgesetzt.");
    }

    private void UpdatePlaybackStatus()
    {
        pause.Text = paused ? "Fortsetzen" : "Pause";
        SetStatus(paused ? "Pausiert" : "Regen läuft");
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
            Title = "Regen-MP3 auswählen",
            Filter = "MP3-Dateien (*.mp3)|*.mp3",
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
            Log("Autostart: " + exception.Message);
            tray.ShowBalloonTip(5000, "Automatic Rain", "Autostart konnte nicht geändert werden.", Forms.ToolTipIcon.Warning);
        }
    }

    private static Settings LoadSettings()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var value = JsonSerializer.Deserialize<Settings>(File.ReadAllText(SettingsPath));
                if (value != null && !string.IsNullOrWhiteSpace(value.AudioFile))
                {
                    value.Volume = double.IsFinite(value.Volume) ? Math.Clamp(value.Volume, 0, 1) : 0.5;
                    return value;
                }
            }
        }
        catch (Exception exception) { Log("Einstellungen: " + exception.Message); }
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
            Log("Speichern fehlgeschlagen: " + exception.Message);
            tray.ShowBalloonTip(5000, "Automatic Rain", "Einstellungen konnten nicht gespeichert werden.", Forms.ToolTipIcon.Warning);
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
        menu.Dispose();
        stopEvent.Dispose();
        Log("Automatic Rain beendet.");
    }
}
