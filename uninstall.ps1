$ErrorActionPreference = 'Stop'
$installDirectory = Join-Path $env:LOCALAPPDATA 'AutomaticRain'
$appDirectory = Join-Path $installDirectory 'app'
$executable = Join-Path $appDirectory 'AutomaticRain.exe'
$running = @(Get-Process -Name AutomaticRain -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $executable })
if (Test-Path -LiteralPath $executable) {
    Start-Process -FilePath $executable -ArgumentList '--stop' -WindowStyle Hidden -Wait
    foreach ($process in $running) {
        if (-not $process.WaitForExit(10000)) { throw 'Please exit Automatic Rain from the tray menu first.' }
    }
}

Remove-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name AutomaticRain -ErrorAction SilentlyContinue
$shortcut = Join-Path ([Environment]::GetFolderPath('Programs')) 'Automatic Rain.lnk'
if (Test-Path -LiteralPath $shortcut) { Remove-Item -LiteralPath $shortcut }

# Only remove the verified application folder. Keep the MP3, settings and logs.
$expectedDirectory = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'AutomaticRain\app'))
if (Test-Path -LiteralPath $appDirectory) {
    $resolvedDirectory = (Resolve-Path -LiteralPath $appDirectory).Path
    $folder = Get-Item -LiteralPath $appDirectory
    if ($resolvedDirectory -ne $expectedDirectory -or ($folder.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        throw 'Unexpected installation path; uninstall cancelled.'
    }
    Remove-Item -LiteralPath $resolvedDirectory -Recurse -Force
}
Write-Host 'Automatic Rain has been removed. Your MP3 file and personal settings have been kept.'
