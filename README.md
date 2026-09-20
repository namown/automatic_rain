# Automatic Rain

Automatic Rain ist eine kleine Windows-App, die eine ausgewählte Regen-MP3 automatisch und dauerhaft im Hintergrund abspielt.

Die Anwendung besitzt kein normales Player- oder Konsolenfenster und erscheint nicht in der Taskleiste. Gesteuert wird sie über das Regentropfen-Symbol im Windows-Infobereich unten rechts. Falls das Symbol nicht direkt sichtbar ist, befindet es sich hinter dem Pfeil **^**.

## Kurz erklärt

1. `AutomaticRain-Setup.exe` starten.
2. Eine Regen-MP3 auswählen.
3. Entscheiden, ob Automatic Rain bei der Windows-Anmeldung automatisch starten soll.
4. Auf **Installieren und starten** klicken.
5. Die App läuft anschliessend unsichtbar im Hintergrund und wiederholt die MP3 automatisch.

Die ausgewählte Datei wird nicht kopiert oder verändert. Automatic Rain speichert lediglich den Pfad zur MP3.

## Voraussetzungen

Für die fertige Setup-Datei:

- Windows 10 oder Windows 11
- 64-Bit-System
- Eine vorhandene MP3-Datei
- Keine Administratorrechte erforderlich
- Keine separate .NET-Installation erforderlich

Das Setup ist eine eigenständige Windows-Anwendung und enthält die benötigte Laufzeitumgebung.

## Installation

Die fertige Installationsdatei befindet sich hier:

```text
dist\AutomaticRain-Setup.exe
```

Nach dem Start öffnet sich der Einrichtungsdialog:

1. Über **Auswählen …** die gewünschte MP3 festlegen.
2. Die Option **Bei der Windows-Anmeldung automatisch starten** aktivieren oder deaktivieren.
3. **Installieren und starten** auswählen.

Automatic Rain wird für den aktuell angemeldeten Benutzer installiert. Es werden folgende Elemente eingerichtet:

- Anwendung unter `%LOCALAPPDATA%\AutomaticRain\app`
- Einstellungen unter `%LOCALAPPDATA%\AutomaticRain\settings.json`
- Startmenü-Eintrag **Automatic Rain**
- Optionaler Autostart bei der Windows-Anmeldung

Bereits vorhandene Installationen werden beim erneuten Ausführen des Setups aktualisiert.

## Wie startet die App?

Automatic Rain kann auf drei Arten starten:

### Direkt nach der Installation

Nach **Installieren und starten** wird die Anwendung sofort geöffnet und beginnt mit der Wiedergabe.

### Automatisch mit Windows

Ist die Autostart-Option aktiviert, startet Automatic Rain nach jeder Windows-Anmeldung. Die gespeicherte MP3 wird von vorne abgespielt.

### Manuell

Die App kann jederzeit über den Startmenü-Eintrag **Automatic Rain** geöffnet werden.

Es kann immer nur eine Instanz gleichzeitig laufen. Wird die App mehrmals gestartet, entsteht deshalb keine doppelte Wiedergabe.

## Bedienung

Mit einem Rechtsklick auf das Regentropfen-Symbol im Infobereich stehen folgende Funktionen zur Verfügung:

- **Pause / Fortsetzen** – unterbricht die Wiedergabe oder setzt sie fort.
- **MP3 auswählen …** – wählt eine andere Audiodatei aus und speichert den neuen Pfad.
- **Lautstärke** – setzt die Lautstärke auf 10, 25, 50, 75 oder 100 Prozent.
- **Mit Windows starten** – schaltet den automatischen Start ein oder aus.
- **Beenden** – beendet Wiedergabe und Anwendung vollständig.

Ein Doppelklick auf das Symbol schaltet ebenfalls zwischen Pause und Wiedergabe um.

**Beenden** deaktiviert den Windows-Autostart nicht. Die App startet bei der nächsten Anmeldung erneut, solange **Mit Windows starten** aktiviert ist.

## Verhalten bei fehlender oder fehlerhafter MP3

Wenn die gespeicherte MP3 verschoben, umbenannt oder gelöscht wurde:

- bleibt Automatic Rain im Infobereich aktiv,
- erscheint kein störendes Popup,
- prüft die App alle 15 Sekunden, ob die Datei wieder vorhanden ist,
- kann über **MP3 auswählen …** ein neuer Pfad festgelegt werden.

Bei einem Decoder- oder Wiedergabefehler sollte die MP3 erneut ausgewählt oder durch eine andere MP3 ersetzt werden.

Je nach MP3-Datei und Windows-Decoder kann beim Übergang zur nächsten Wiederholung eine kurze Pause hörbar sein.

## Einstellungen und Diagnose

Automatic Rain speichert benutzerspezifische Daten hier:

```text
%LOCALAPPDATA%\AutomaticRain
```

Wichtige Dateien:

- `settings.json` – MP3-Pfad und Lautstärke
- `app.log` – begrenztes Diagnoseprotokoll
- `app\AutomaticRain.exe` – installierte Anwendung

Das Diagnoseprotokoll wird automatisch begrenzt, damit es nicht unbegrenzt wächst.

## Deinstallation

Im Repository kann die App mit folgendem Befehl entfernt werden:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\uninstall.ps1
```

Das Skript:

- beendet Automatic Rain,
- entfernt den Windows-Autostart,
- entfernt den Startmenü-Eintrag,
- löscht die installierten Programmdateien.

Die MP3, persönlichen Einstellungen und das Diagnoseprotokoll bleiben erhalten.

## Setup selbst neu bauen

Zum Erstellen einer neuen Setup-Datei wird das **.NET SDK 8 oder neuer** benötigt.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

Die neue Datei wird anschliessend hier erstellt:

```text
dist\AutomaticRain-Setup.exe
```

Alternativ kann `install.ps1` verwendet werden. Dieses Skript baut zuerst eine neue Setup-Datei und startet danach den Einrichtungsdialog:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\install.ps1
```

Die temporären Build-Dateien werden ausserhalb des Repositorys unter `%LOCALAPPDATA%\AutomaticRainBuild` abgelegt.

## Technische Übersicht

- C# / .NET 8
- WPF und Windows Forms
- Selbstständige Single-File-Anwendung für Windows x64
- Keine externen NuGet-Pakete
- Wiedergabe über den Windows-`MediaPlayer`
- Einstellungen im JSON-Format
- Autostart über den aktuellen Windows-Benutzer
- Eigene Setup-Oberfläche und eigenes App-/Tray-Symbol
