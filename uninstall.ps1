# Removes PolisK+ (internal name: Polish): stops it, deletes shortcuts, program folder and local data.
# Your styles, rules and config are backed up to Documents first.
param([switch]$Quiet)

$AppName = 'PolisK+'
$dest = Join-Path $env:LOCALAPPDATA 'Programs\Polish'
$data = Join-Path $env:LOCALAPPDATA 'Polish'
Set-Location $env:USERPROFILE

Write-Host "=== Uninstall $AppName ===" -ForegroundColor Cyan
Get-CimInstance Win32_Process -Filter "Name='powershell.exe'" |
    Where-Object { $_.CommandLine -like '*polish.ps1*' -and $_.ProcessId -ne $PID } |
    ForEach-Object { Stop-Process -Id $_.ProcessId -Force; Write-Host "Stopped $AppName (pid $($_.ProcessId))" }

if ((Test-Path (Join-Path $dest 'prompt')) -or (Test-Path (Join-Path $dest 'profiles'))) {
    $backup = Join-Path ([Environment]::GetFolderPath('MyDocuments')) ("PolisK-settings-backup-" + (Get-Date -Format 'yyyyMMdd-HHmm'))
    New-Item -ItemType Directory -Force $backup | Out-Null
    foreach ($d in 'prompt', 'profiles') {
        if (Test-Path (Join-Path $dest $d)) { Copy-Item (Join-Path $dest $d) $backup -Recurse -Force }
    }
    Copy-Item (Join-Path $dest 'config.json') $backup -Force -ErrorAction SilentlyContinue
    Write-Host "Backed up your styles and settings to: $backup" -ForegroundColor Yellow
}

$startupDir = [Environment]::GetFolderPath('Startup')
$programs = [Environment]::GetFolderPath('Programs')
$desktopDir = [Environment]::GetFolderPath('Desktop')
# Current names plus the "Polish" names used before 1.1.2.
$targets = @(
    (Join-Path $startupDir "$AppName.lnk"), (Join-Path $startupDir 'Polish.lnk'),
    (Join-Path $desktopDir "$AppName.lnk"), (Join-Path $desktopDir 'Polish.lnk'),
    (Join-Path $programs $AppName), (Join-Path $programs 'Polish'),
    $data, $dest)
foreach ($p in $targets) {
    if (Test-Path -LiteralPath $p) { Remove-Item -LiteralPath $p -Recurse -Force -ErrorAction SilentlyContinue; Write-Host "Removed $p" }
}
Write-Host "$AppName is uninstalled." -ForegroundColor Green
if (-not $Quiet) { Read-Host 'Press Enter to close' | Out-Null }
