$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'build.ps1')
if (-not $?) { throw 'Die Setup-EXE konnte nicht gebaut werden.' }
# Interactive setup: the user selects the MP3 before installation starts.
Start-Process -FilePath (Join-Path $PSScriptRoot 'dist\AutomaticRain-Setup.exe')
