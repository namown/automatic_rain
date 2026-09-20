# Automatic Rain

Automatic Rain is a small and easy Windows app that automatically plays a selected rain MP3 continuously in the background.

The application has no conventional player or console window and does not appear in the taskbar. Very simple. It is controlled through the raindrop icon in the Windows notification area in the lower-right corner.


## Quick Start

1. Run `AutomaticRain-Setup.exe`.
2. Select a MP3.
3. Choose whether Automatic Rain should start automatically when you sign in to Windows.
4. Click **Install and start**.
5. The app then runs invisibly in the background and automatically repeats the MP3.

The selected file is not copied or modified. Automatic Rain only stores the path to the MP3.


The setup is a self-contained Windows application and includes the required runtime.

## Installation

The completed installer is located here:

```text
dist\AutomaticRain-Setup.exe
```

After starting it, the setup dialog opens:

1. Use **Select …** to choose the desired MP3.

2. Enable or disable **Start automatically when signing in to Windows**.

3. Select **Install and start**.

Automatic Rain is installed for the currently signed-in user. The following items are set up:

- Application under `%LOCALAPPDATA%\AutomaticRain\app`
- Settings under `%LOCALAPPDATA%\AutomaticRain\settings.json`
- Start menu entry **Automatic Rain**
- Optional automatic startup when signing in to Windows

Existing installations are updated when the setup is run again.

## How Does the App Start?

Automatic Rain can start in three ways:

### Immediately After Installation

After selecting **Install and start**, the application opens immediately and begins playback.

### Automatically with Windows

If the automatic startup option is enabled, Automatic Rain starts after every Windows sign-in. The saved MP3 starts from the beginning.

### Manually

The app can be opened at any time through the **Automatic Rain** Start menu entry.

Only one instance can run at a time. Starting the app multiple times therefore does not result in duplicate playback.

## Usage

Right-click the raindrop icon in the notification area to access the following functions:

- **Pause / Resume** – pauses playback or resumes it.
- **Select MP3 …** – selects another audio file and saves its new path.
- **Volume** – sets the volume to 10, 25, 50, 75, or 100 percent.
- **Start with Windows** – enables or disables automatic startup.
- **Exit** – completely stops playback and closes the application.

Double-clicking the icon also toggles between pausing and resuming playback.

**Exit** does not disable Windows startup. The app starts again at the next sign-in as long as **Start with Windows** is enabled.

## Behavior When the MP3 Is Missing or Invalid

If the saved MP3 has been moved, renamed, or deleted:

- Automatic Rain remains active in the notification area,
- no intrusive popup is displayed,
- the app checks every 15 seconds whether the file is available again,
- a new path can be selected through **Select MP3 …**.

If a decoding or playback error occurs, select the MP3 again or replace it with another MP3.

Depending on the MP3 file and the Windows decoder, a short pause may be audible when the next repetition begins.

## Settings and Diagnostics

Automatic Rain stores user-specific data here:

```text
%LOCALAPPDATA%\AutomaticRain
```

Important files:

- `settings.json` – MP3 path and volume
- `app.log` – limited diagnostic log
- `app\AutomaticRain.exe` – installed application

The diagnostic log is automatically limited so that it does not grow indefinitely.

## Uninstallation

From the repository, the app can be removed with the following command:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\uninstall.ps1
```

The script:

- stops Automatic Rain,
- removes Windows startup,
- removes the Start menu entry,
- deletes the installed program files.

The MP3, personal settings, and diagnostic log are retained.

## Rebuilding the Setup

The **.NET SDK 8 or newer** is required to create a new setup file.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

The new file is then created here:

```text
dist\AutomaticRain-Setup.exe
```

Alternatively, `install.ps1` can be used. This script first builds a new setup file and then starts the setup dialog:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\install.ps1
```

Temporary build files are stored outside the repository under `%LOCALAPPDATA%\AutomaticRainBuild`.

## Technical Overview

- C# / .NET 8
- WPF and Windows Forms
- Self-contained single-file application for Windows x64
- No external NuGet packages
- Playback through the Windows `MediaPlayer`
- Settings in JSON format
- Startup through the current Windows user account
- Custom setup interface and custom app/tray icon
