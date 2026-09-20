$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'build.ps1')
if (-not $?) { throw 'Could not build the setup EXE.' }
# Interactive setup: the user selects the MP3 before installation starts.
Start-Process -FilePath (Join-Path $PSScriptRoot 'dist\AutomaticRain-Setup.exe')
