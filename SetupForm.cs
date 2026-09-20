using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace AutomaticRain;

internal sealed class SetupForm : Forms.Form
{
    private readonly Icon rainIcon = AppFiles.LoadIcon();
    private readonly Forms.TextBox audioPath = new() { ReadOnly = true, Dock = Forms.DockStyle.Fill, Name = "AudioPath" };
    private readonly Forms.CheckBox startup = new() { Text = "Start automatically when I sign in to Windows", Checked = true, AutoSize = true };
    private readonly Forms.Button install = new() { Text = "Install and start", AutoSize = true, Enabled = false, Name = "Install" };
    private readonly Forms.Label status = new() { Text = "Select your MP3 file to get started.", AutoSize = true };

    internal SetupForm()
    {
        Text = "Automatic Rain – Setup";
        Icon = rainIcon;
        Font = new Font("Segoe UI", 10);
        AutoScaleMode = Forms.AutoScaleMode.Dpi;
        ClientSize = new Size(590, 245);
        FormBorderStyle = Forms.FormBorderStyle.FixedDialog;
        StartPosition = Forms.FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;
        var layout = new Forms.TableLayoutPanel
        {
            Dock = Forms.DockStyle.Fill,
            Padding = new Forms.Padding(22),
            ColumnCount = 2,
            RowCount = 5
        };
        layout.ColumnStyles.Add(new Forms.ColumnStyle(Forms.SizeType.Percent, 100));
        layout.ColumnStyles.Add(new Forms.ColumnStyle(Forms.SizeType.AutoSize));
        var title = new Forms.Label
        {
            Text = "Which MP3 would you like to play in the background?",
            AutoSize = true,
            Margin = new Forms.Padding(0, 0, 0, 16)
        };
        layout.Controls.Add(title, 0, 0);
        layout.SetColumnSpan(title, 2);
        layout.Controls.Add(audioPath, 0, 1);
        var browse = new Forms.Button { Text = "Browse …", AutoSize = true, Name = "Browse" };
        browse.Click += (_, _) => SelectAudio();
        layout.Controls.Add(browse, 1, 1);
        startup.Margin = new Forms.Padding(0, 16, 0, 8);
        layout.Controls.Add(startup, 0, 2);
        layout.SetColumnSpan(startup, 2);
        layout.Controls.Add(status, 0, 3);
        layout.SetColumnSpan(status, 2);
        var buttons = new Forms.FlowLayoutPanel { AutoSize = true, Dock = Forms.DockStyle.Fill, FlowDirection = Forms.FlowDirection.RightToLeft };
        var cancel = new Forms.Button { Text = "Cancel", AutoSize = true, DialogResult = Forms.DialogResult.Cancel };
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(install);
        layout.Controls.Add(buttons, 0, 4);
        layout.SetColumnSpan(buttons, 2);
        Controls.Add(layout);
        AcceptButton = install;
        CancelButton = cancel;
        install.Click += (_, _) =>
        {
            install.Enabled = false;
            browse.Enabled = false;
            cancel.Enabled = false;
            status.Text = "Setting up Automatic Rain …";
            Refresh();
            try
            {
                Installer.Install(Environment.ProcessPath!, audioPath.Text, startup.Checked);
                Process.Start(new ProcessStartInfo(AppFiles.Executable) { UseShellExecute = true });
                Close();
            }
            catch (Exception exception)
            {
                RainApplication.Log("Installation: " + exception);
                status.Text = "Setup failed. Please try again.";
                string message = exception is InvalidOperationException ? exception.Message :
                    "Setup could not finish. Check that your MP3 file is accessible, close Automatic Rain, and try again.\n\nDetails were saved to:\n" + Path.Combine(AppFiles.Root, "app.log");
                Forms.MessageBox.Show(this, message, "Automatic Rain", Forms.MessageBoxButtons.OK, Forms.MessageBoxIcon.Error);
                install.Enabled = true;
                browse.Enabled = true;
                cancel.Enabled = true;
            }
        };
    }

    private void SelectAudio()
    {
        using var dialog = new Forms.OpenFileDialog
        {
            Title = "Select an MP3 file",
            Filter = "MP3 files (*.mp3)|*.mp3",
            CheckFileExists = true,
            RestoreDirectory = true
        };
        if (dialog.ShowDialog(this) != Forms.DialogResult.OK) return;
        audioPath.Text = dialog.FileName;
        install.Enabled = true;
        status.Text = "Your selection will be saved. You can change it later in the tray menu.";
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) rainIcon.Dispose();
    }
}

internal static class Installer
{
    internal static void Install(string sourceExecutable, string audioFile, bool autoStart)
    {
        if (!File.Exists(audioFile) || !string.Equals(Path.GetExtension(audioFile), ".mp3", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Please select an existing MP3 file.");

        string appDirectory = Path.GetDirectoryName(AppFiles.Executable)!;
        Directory.CreateDirectory(appDirectory);
        bool copyRequired = !string.Equals(Path.GetFullPath(sourceExecutable), AppFiles.Executable, StringComparison.OrdinalIgnoreCase);
        string stagedFile = Path.Combine(appDirectory, "AutomaticRain.pending");
        try
        {
            // Stage before stopping an existing installation, so a copy error leaves it running.
            if (copyRequired) File.Copy(sourceExecutable, stagedFile, true);
            StopRunningApplication();
            if (copyRequired) File.Move(stagedFile, AppFiles.Executable, true);

            Settings settings = new();
            try
            {
                if (File.Exists(AppFiles.Settings))
                    settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(AppFiles.Settings)) ?? settings;
            }
            catch (Exception exception) { RainApplication.Log("Previous settings: " + exception.Message); }
            settings.AudioFile = Path.GetFullPath(audioFile);
            settings.Volume = double.IsFinite(settings.Volume) ? Math.Clamp(settings.Volume, 0, 1) : 0.5;
            string pendingSettings = AppFiles.Settings + ".pending";
            File.WriteAllText(pendingSettings, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(pendingSettings, AppFiles.Settings, true);

            using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
            if (autoStart) key.SetValue("AutomaticRain", '"' + AppFiles.Executable + '"');
            else key.DeleteValue("AutomaticRain", false);
            CreateShortcut();
        }
        finally
        {
            if (File.Exists(stagedFile)) File.Delete(stagedFile);
        }
    }

    private static void StopRunningApplication()
    {
        if (EventWaitHandle.TryOpenExisting(Program.StopEventName, out var stop))
            using (stop) stop.Set();
        foreach (var process in Process.GetProcessesByName("AutomaticRain"))
        {
            using (process)
            {
                if (process.Id == Environment.ProcessId || process.HasExited) continue;
                try
                {
                    if (!string.Equals(process.MainModule?.FileName, AppFiles.Executable, StringComparison.OrdinalIgnoreCase)) continue;
                }
                catch (InvalidOperationException) { continue; }
                if (!process.WaitForExit(10000))
                    throw new InvalidOperationException("Please exit Automatic Rain from the tray menu and try again.");
            }
        }
    }

    private static void CreateShortcut()
    {
        Type shellType = Type.GetTypeFromProgID("WScript.Shell")
            ?? throw new InvalidOperationException("Could not create the Start menu shortcut.");
        dynamic shell = Activator.CreateInstance(shellType)!;
        try
        {
            string shortcutPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Automatic Rain.lnk");
            dynamic shortcut = shell.CreateShortcut(shortcutPath);
            try
            {
                shortcut.TargetPath = AppFiles.Executable;
                shortcut.WorkingDirectory = Path.GetDirectoryName(AppFiles.Executable);
                shortcut.IconLocation = AppFiles.Executable + ",0";
                shortcut.Description = "Play MP3 audio in the background";
                shortcut.Save();
            }
            finally { Marshal.FinalReleaseComObject(shortcut); }
        }
        finally { Marshal.FinalReleaseComObject(shell); }
    }
}
