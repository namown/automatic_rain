# Automatic Rain

Eine kleine Windows-App, die eine MP3 in Endlosschleife im Hintergrund abspielt.
Kein Player-Fenster, kein Konsolenfenster, kein Eintrag in der Taskleiste.
Die App ist über ein Info-Symbol im Infobereich unten rechts erreichbar
(gegebenenfalls hinter dem Pfeil **^**).

## Installieren

In PowerShell in diesem Ordner ausführen:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\install.ps1
```

Standarddatei: `C:\Rain\Rain\_01.mp3`. Eine andere Datei lässt sich direkt angeben:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\install.ps1 -AudioFile 'C:\Rain\Rain_01.mp3'
```

Der Installer baut die App, installiert sie für den aktuellen Benutzer unter
`%LOCALAPPDATA%\AutomaticRain\app`, erstellt einen Startmenü-Eintrag, aktiviert
den Windows-Autostart und startet die Wiedergabe. Administratorrechte sind nicht nötig.
Benötigt werden Windows 10/11 x64, das .NET SDK 8 oder neuer zum Bauen und
die .NET Desktop Runtime 8 zum Ausführen. Es werden keine externen NuGet-Pakete verwendet.
Build-Dateien liegen außerhalb des Projekts unter `%LOCALAPPDATA%\AutomaticRainBuild`.

## Bedienung

Rechtsklick auf das Info-Symbol:

- **Pause / Fortsetzen** – auch per Doppelklick auf das Symbol.
- **MP3 auswählen …** – Datei wechseln; die Auswahl wird gespeichert.
- **Lautstärke** – 10, 25, 50, 75 oder 100 Prozent; Startwert ist 50 Prozent.
- **Mit Windows starten** – Autostart ein- oder ausschalten.
- **Beenden** – Wiedergabe und App vollständig schließen.

Bei der nächsten Windows-Anmeldung nach Einschalten oder Neustart beginnt die
MP3 wieder von vorne, sofern der Autostart aktiviert ist. **Beenden** schaltet
den Autostart nicht aus. Mehrfaches Starten erzeugt keine doppelte Wiedergabe.
Die MP3 wird direkt in der App abgespielt und nicht mit einem externen Player geöffnet.
Die Wiederholung kann abhängig von MP3 und Decoder eine kurze Pause haben.

Fehlt die Datei, bleibt die App ohne Popup im Infobereich und prüft alle
15 Sekunden, ob sie inzwischen vorhanden ist. Über **MP3 auswählen …** lässt
sich der Pfad korrigieren. Nach einem Decoderfehler die Datei erneut auswählen.
Einstellungen und ein begrenztes Diagnoseprotokoll liegen unter
`%LOCALAPPDATA%\AutomaticRain\settings.json` bzw. `app.log`.

## Deinstallieren

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\uninstall.ps1
```

Beendet die App und entfernt Autostart, Startmenü-Verknüpfung und App-Dateien.
Die MP3 sowie die persönlichen Einstellungen und das Protokoll bleiben erhalten.
