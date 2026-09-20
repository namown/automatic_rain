param([string]$OutputDirectory = (Join-Path $PSScriptRoot 'dist'))

$ErrorActionPreference = 'Stop'
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw '.NET SDK 8 or later is required to build the app.'
}
& (Join-Path $PSScriptRoot 'build-icon.ps1')
$buildDirectory = Join-Path $env:LOCALAPPDATA 'AutomaticRainBuild\standalone'
$publishDirectory = Join-Path $buildDirectory 'publish'
& dotnet publish (Join-Path $PSScriptRoot 'AutomaticRain.csproj') -c Release -r win-x64 --self-contained true -o $publishDirectory '-p:PublishSingleFile=true' '-p:IncludeNativeLibrariesForSelfExtract=true' '-p:EnableCompressionInSingleFile=true' '-p:DebugType=None' '-p:DebugSymbols=false' "-p:BaseIntermediateOutputPath=$buildDirectory\obj\" "-p:BaseOutputPath=$buildDirectory\bin\"
if ($LASTEXITCODE -ne 0) { throw 'Could not build the setup EXE.' }
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$setupFile = Join-Path $OutputDirectory 'AutomaticRain-Setup.exe'
Copy-Item -LiteralPath (Join-Path $publishDirectory 'AutomaticRain.exe') -Destination $setupFile -Force
Write-Host "Ready: $setupFile"
