param([string]$AudioFile = 'C:\Rain\Rain\_01.mp3')

$ErrorActionPreference = 'Stop'
$installDirectory = Join-Path $env:LOCALAPPDATA 'AutomaticRain'
$appDirectory = Join-Path $installDirectory 'app'
$buildDirectory = Join-Path $env:LOCALAPPDATA 'AutomaticRainBuild'
$publishDirectory = Join-Path $buildDirectory 'publish'
$project = Join-Path $PSScriptRoot 'AutomaticRain.csproj'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'Zum Bauen wird das .NET SDK 8 oder neuer benoetigt.'
}

& dotnet publish $project -c Release -r win-x64 --self-contained false -o $publishDirectory "-p:BaseIntermediateOutputPath=$buildDirectory\obj\" "-p:BaseOutputPath=$buildDirectory\bin\"
if ($LASTEXITCODE -ne 0) { throw 'Die App konnte nicht gebaut werden.' }

$executable = Join-Path $appDirectory 'AutomaticRain.exe'
$running = @(Get-Process -Name AutomaticRain -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $executable })
if (Test-Path -LiteralPath $executable) {
    Start-Process -FilePath $executable -ArgumentList '--stop' -WindowStyle Hidden -Wait
    foreach ($process in $running) {
        if (-not $process.WaitForExit(10000)) { throw 'Bitte Automatic Rain ueber das Tray-Menue beenden und erneut installieren.' }
    }
}

New-Item -ItemType Directory -Path $appDirectory -Force | Out-Null
Copy-Item -Path (Join-Path $publishDirectory '*') -Destination $appDirectory -Recurse -Force
$settingsPath = Join-Path $installDirectory 'settings.json'
if (-not (Test-Path -LiteralPath $settingsPath) -or $PSBoundParameters.ContainsKey('AudioFile')) {
    @{ AudioFile = $AudioFile; Volume = 0.5 } | ConvertTo-Json | Set-Content -LiteralPath $settingsPath -Encoding UTF8
}

$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
if (-not (Test-Path -LiteralPath $runKey)) { New-Item -Path $runKey | Out-Null }
New-ItemProperty -Path $runKey -Name AutomaticRain -Value ('"' + $executable + '"') -PropertyType String -Force | Out-Null

$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut((Join-Path ([Environment]::GetFolderPath('Programs')) 'Automatic Rain.lnk'))
$shortcut.TargetPath = $executable
$shortcut.WorkingDirectory = $appDirectory
$shortcut.Description = 'Regengeraeusche im Hintergrund'
$shortcut.Save()

Start-Process -FilePath $executable -WindowStyle Hidden
Write-Host "Automatic Rain ist installiert und gestartet: $executable"
Write-Host 'Autostart ist aktiviert. Rechtsklick auf das Info-Symbol im Infobereich: Pause oder Beenden.'
if (-not (Test-Path -LiteralPath $AudioFile)) {
    Write-Host "MP3 noch nicht gefunden: $AudioFile. Im Tray-Menue kannst du eine Datei auswaehlen."
}
