using System;
using System.Drawing;
using System.IO;

namespace AutomaticRain;

internal static class AppFiles
{
    internal static readonly string Root = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AutomaticRain");
    internal static readonly string Executable = Path.Combine(Root, "app", "AutomaticRain.exe");
    internal static readonly string Settings = Path.Combine(Root, "settings.json");

    internal static Icon LoadIcon()
    {
        using var stream = typeof(AppFiles).Assembly.GetManifestResourceStream("AutomaticRain.Raindrop.ico")
            ?? throw new InvalidOperationException("Das App-Symbol fehlt.");
        using var icon = new Icon(stream, 32, 32);
        return (Icon)icon.Clone();
    }
}
