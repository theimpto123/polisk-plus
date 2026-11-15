# Builds dist\PolisK+-<version>.zip from this folder (maintainer tool, not shipped).
#   powershell -NoProfile -ExecutionPolicy Bypass -File build-package.ps1
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$version = [regex]::Match((Get-Content (Join-Path $root 'Polish.cs') -Raw), 'Version = "([^"]+)"').Groups[1].Value
$name = "PolisK+-$version"
$stage = Join-Path $root "dist\$name"
$zip = Join-Path $root "dist\$name.zip"

if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
if (Test-Path $zip) { Remove-Item $zip -Force }
New-Item -ItemType Directory -Force (Join-Path $stage 'app\prompt'), (Join-Path $stage 'app\profiles') | Out-Null

foreach ($f in 'Setup.cmd', 'Uninstall.cmd', 'setup.ps1', 'README.md', 'README.vi.md') { Copy-Item (Join-Path $root $f) $stage }
foreach ($f in 'Polish.cs', 'Profiles.cs', 'Providers.cs', 'SettingsForm.cs', 'polish.ps1', 'uninstall.ps1', 'polish.ico') { Copy-Item (Join-Path $root $f) (Join-Path $stage 'app') }
Copy-Item (Join-Path $root 'prompt\rules.md') (Join-Path $stage 'app\prompt')
Copy-Item (Join-Path $root 'profiles\*.md') (Join-Path $stage 'app\profiles')

# Ship config.json with an empty claudePath: each computer finds its own CLI.
$cfg = Get-Content (Join-Path $root 'config.json') -Raw
$cfg = [regex]::Replace($cfg, '"claudePath"\s*:\s*"(?:[^"\\]|\\.)*"', '"claudePath": ""')
# Never ship API keys (they are DPAPI-encrypted and useless elsewhere, but leave nothing to chance).
$cfg = [regex]::Replace($cfg, '"apiKeys"\s*:\s*\{[^}]*\}', '"apiKeys": {}')
if ($cfg -notmatch '"apiKeys": \{\}') { throw 'config.json: could not clear apiKeys' }
[IO.File]::WriteAllText((Join-Path $stage 'app\config.json'), $cfg, (New-Object Text.UTF8Encoding($false)))

# Scripts must stay ASCII: Windows PowerShell 5.1 reads BOM-less files as ANSI.
Get-ChildItem $stage -Recurse -Include *.ps1, *.cmd | ForEach-Object {
    if ([IO.File]::ReadAllBytes($_.FullName) | Where-Object { $_ -gt 127 }) { throw "Non-ASCII character in $($_.Name)" }
}

# Build the zip by hand: PS 5.1 Compress-Archive writes "\" in entry names, which some unzip tools reject.
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::Open($zip, 'Create')
try {
    Get-ChildItem $stage -Recurse -File | ForEach-Object {
        $entry = "$name/" + $_.FullName.Substring($stage.Length + 1).Replace('\', '/')
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $_.FullName, $entry) | Out-Null
    }
} finally { $archive.Dispose() }
Write-Host "Built $zip ($([math]::Round((Get-Item $zip).Length / 1KB)) KB)"
