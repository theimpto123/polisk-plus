# PolisK+ setup (internal name: Polish) - per-user install, no admin rights needed.
# Usually started by double-clicking Setup.cmd.
#   -Quiet        no questions: autostart on, skip Claude install/sign-in prompts
#   -NoAutostart  do not add PolisK+ to the Windows Startup folder
param([switch]$Quiet, [switch]$NoAutostart)

$ErrorActionPreference = 'Stop'
$pkg     = Split-Path -Parent $MyInvocation.MyCommand.Path
$src     = Join-Path $pkg 'app'
$dest    = Join-Path $env:LOCALAPPDATA 'Programs\Polish'
$psExe   = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
$AppName = 'PolisK+'
$refs    = 'System.Windows.Forms', 'System.Drawing', 'System.Web.Extensions', 'UIAutomationClient', 'UIAutomationTypes', 'WindowsBase'

function Say([string]$msg, [string]$color = 'Gray') { Write-Host $msg -ForegroundColor $color }
function Ask([string]$question, [bool]$default = $true) {
    if ($Quiet) { return $default }
    $hint = if ($default) { '[Y/n]' } else { '[y/N]' }
    $a = Read-Host "$question $hint"
    if ([string]::IsNullOrWhiteSpace($a)) { return $default }
    return $a.Trim().ToLower().StartsWith('y')
}
function Fail([string]$msg) { Say ''; Say "SETUP FAILED: $msg" 'Red'; exit 1 }

# Do not let variables from a running Claude Code session leak into the CLI checks below.
Get-ChildItem env: | Where-Object { $_.Name -like 'CLAUDE*' -and $_.Name -notin 'CLAUDE_CODE_OAUTH_TOKEN', 'CLAUDE_CONFIG_DIR' } |
    ForEach-Object { Remove-Item "env:$($_.Name)" -ErrorAction SilentlyContinue }

Say ''
Say "=== $AppName setup ===" 'Cyan'
if (-not (Test-Path (Join-Path $src 'Polish.cs'))) { Fail "app\Polish.cs not found next to setup.ps1. Extract the whole zip first, then run Setup.cmd." }

# ---------------------------------------------------------------- 1. environment checks
Say ''
Say '[1/5] Checking this computer...' 'Cyan'
Say "  PowerShell $($PSVersionTable.PSVersion), language mode: $($ExecutionContext.SessionState.LanguageMode)"
if ($PSVersionTable.PSVersion.Major -lt 5) { Fail 'Windows PowerShell 5.1 or newer is required.' }
if ($ExecutionContext.SessionState.LanguageMode -ne 'FullLanguage') {
    Fail 'PowerShell runs in Constrained Language Mode on this computer (company policy), so PolisK+ cannot run. Ask IT whether PowerShell Add-Type is allowed.'
}
$csFiles = 'Polish.cs', 'Profiles.cs', 'SettingsForm.cs'
try {
    Add-Type -AssemblyName $refs
    $provider = New-Object Microsoft.CSharp.CSharpCodeProvider
    $cp = New-Object System.CodeDom.Compiler.CompilerParameters
    $cp.GenerateInMemory = $true
    $cp.ReferencedAssemblies.Add('System.dll') | Out-Null
    $cp.ReferencedAssemblies.Add('System.Core.dll') | Out-Null
    foreach ($r in $refs) { $cp.ReferencedAssemblies.Add([Reflection.Assembly]::LoadWithPartialName($r).Location) | Out-Null }
    $sources = [string[]]@($csFiles | ForEach-Object { [IO.File]::ReadAllText((Join-Path $src $_), [Text.Encoding]::UTF8) })
    $res = $provider.CompileAssemblyFromSource($cp, $sources)
    $errs = @($res.Errors | Where-Object { -not $_.IsWarning })
    if ($errs.Count) { throw ($errs | Select-Object -First 3 | ForEach-Object { "line $($_.Line): $($_.ErrorText)" }) -join '; ' }
    Say '  C# compiler / WinForms: OK' 'Green'
} catch { Fail "Cannot compile PolisK+ on this computer: $($_.Exception.Message)" }

# ---------------------------------------------------------------- 2. copy files
Say ''
Say "[2/5] Installing to $dest" 'Cyan'
Get-CimInstance Win32_Process -Filter "Name='powershell.exe'" |
    Where-Object { $_.CommandLine -like '*polish.ps1*' } |
    ForEach-Object { Stop-Process -Id $_.ProcessId -Force; Say "  Stopped running PolisK+ (pid $($_.ProcessId))" }

$profDir = Join-Path $dest 'profiles'
$hadProfiles = Test-Path $profDir
$cfgPath = Join-Path $dest 'config.json'

# Upgrade from v1.0 (one tone.md + structure.md): turn customised files into a style named "My style".
$migrated = $false
$oldTone = Join-Path $dest 'prompt\tone.md'
$oldStruct = Join-Path $dest 'prompt\structure.md'
if (-not $hadProfiles -and ((Test-Path $oldTone) -or (Test-Path $oldStruct))) {
    function Read-Text([string]$p) { if (Test-Path $p) { [IO.File]::ReadAllText($p, [Text.Encoding]::UTF8).Trim() } else { '' } }
    $t = Read-Text $oldTone; $s = Read-Text $oldStruct
    $changed = ($t -ne (Read-Text (Join-Path $dest 'defaults\prompt\tone.md'))) -or ($s -ne (Read-Text (Join-Path $dest 'defaults\prompt\structure.md')))
    if ($changed) {
        New-Item -ItemType Directory -Force $profDir | Out-Null
        $body = "description: Your tone and structure from Polish 1.0`r`norder: 0`r`n`r`n# Tone`r`n`r`n$t`r`n`r`n# Output structure`r`n`r`n$s`r`n"
        [IO.File]::WriteAllText((Join-Path $profDir 'My style.md'), $body, (New-Object Text.UTF8Encoding($false)))
        $migrated = $true
    }
    $oldDir = Join-Path $dest 'prompt\old-v1'
    New-Item -ItemType Directory -Force $oldDir | Out-Null
    foreach ($f in $oldTone, $oldStruct) { if (Test-Path $f) { Move-Item $f $oldDir -Force } }
    Remove-Item (Join-Path $dest 'defaults\prompt\tone.md'), (Join-Path $dest 'defaults\prompt\structure.md') -Force -ErrorAction SilentlyContinue
}

New-Item -ItemType Directory -Force $dest, (Join-Path $dest 'prompt'), $profDir, (Join-Path $dest 'defaults\prompt'), (Join-Path $dest 'defaults\profiles') | Out-Null

# Files you never edited (still equal to the previous version's default) get the new default text.
function Norm([string]$p) { ([regex]::Replace([IO.File]::ReadAllText($p, [Text.Encoding]::UTF8).Replace("`r`n", "`n"), "[ \t]+\n", "`n")).Trim() }
$updated = @()
foreach ($dir in 'prompt', 'profiles') {
    foreach ($new in Get-ChildItem (Join-Path $src "$dir\*.md")) {
        $mine = Join-Path $dest "$dir\$($new.Name)"
        $oldDefault = Join-Path $dest "defaults\$dir\$($new.Name)"
        if ((Test-Path $mine) -and (Test-Path $oldDefault) -and
            ((Norm $mine) -eq (Norm $oldDefault)) -and ((Norm $mine) -ne (Norm $new.FullName))) {
            Copy-Item $new.FullName $mine -Force; $updated += "$dir\$($new.Name)"
        }
    }
}
if ($updated.Count) { Say "  Updated to the new default text (you had not changed them): $($updated -join ', ')" }
# Program files: always replaced.
foreach ($f in $csFiles + 'polish.ps1', 'uninstall.ps1', 'polish.ico') { Copy-Item (Join-Path $src $f) $dest -Force }
foreach ($r in 'README.md', 'README.vi.md') { Copy-Item (Join-Path $pkg $r) $dest -Force -ErrorAction SilentlyContinue }
Copy-Item (Join-Path $src 'prompt\*.md') (Join-Path $dest 'defaults\prompt') -Force
Copy-Item (Join-Path $src 'profiles\*.md') (Join-Path $dest 'defaults\profiles') -Force
Copy-Item (Join-Path $src 'config.json') (Join-Path $dest 'defaults\config.json') -Force
# User files: only created when missing, so an upgrade keeps your styles, rules and config.
$kept = @()
foreach ($p in Get-ChildItem (Join-Path $src 'prompt\*.md')) {
    $target = Join-Path $dest "prompt\$($p.Name)"
    if (Test-Path $target) { if ($updated -notcontains "prompt\$($p.Name)") { $kept += "prompt\$($p.Name)" } } else { Copy-Item $p.FullName $target }
}
if ($hadProfiles) { $kept += 'profiles (your styles)' }
else { foreach ($p in Get-ChildItem (Join-Path $src 'profiles\*.md')) { Copy-Item $p.FullName (Join-Path $profDir $p.Name) } }
if (Test-Path $cfgPath) { $kept += 'config.json' } else { Copy-Item (Join-Path $src 'config.json') $cfgPath }
if ($migrated) {
    $raw = Get-Content $cfgPath -Raw
    if ($raw -match '"defaultProfile"') { $raw = [regex]::Replace($raw, '"defaultProfile"\s*:\s*"[^"]*"', '"defaultProfile": "My style"') }
    else { $raw = $raw -replace '^\s*\{', "{`r`n  `"defaultProfile`": `"My style`"," }
    [IO.File]::WriteAllText($cfgPath, $raw, (New-Object Text.UTF8Encoding($false)))
    Say '  Your 1.0 tone/structure became the style "My style" (now the default). Old files: prompt\old-v1' 'Yellow'
}
Get-ChildItem $dest -Recurse -File | Unblock-File -ErrorAction SilentlyContinue
if ($kept.Count) { Say "  Kept your existing settings: $($kept -join ', ')" 'Yellow' }
Say '  Files installed.' 'Green'

# ---------------------------------------------------------------- 3. Claude Code CLI
Say ''
Say '[3/5] Looking for the Claude Code CLI...' 'Cyan'
function Find-Claude {
    $cfg = Get-Content $cfgPath -Raw | ConvertFrom-Json
    if ($cfg.claudePath -and (Test-Path $cfg.claudePath)) { return $cfg.claudePath }
    $cmd = Get-Command claude.exe -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($cmd) { return $cmd.Source }
    $local = Join-Path $env:USERPROFILE '.local\bin\claude.exe'
    if (Test-Path $local) { return $local }
    $bundled = Join-Path $env:APPDATA 'Claude\claude-code'
    if (Test-Path $bundled) {
        $b = Get-ChildItem $bundled -Recurse -Filter claude.exe -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1
        if ($b) { return $b.FullName }
    }
    $npm = Join-Path $env:APPDATA 'npm\node_modules\@anthropic-ai\claude-code\cli.js'
    if ((Test-Path $npm) -and (Get-Command node.exe -ErrorAction SilentlyContinue)) { return $npm }
    return $null
}
function Invoke-Claude([string]$path, [string[]]$cliArgs) {
    if ($path -like '*.js') { & node.exe $path @cliArgs } else { & $path @cliArgs }
}

$claude = Find-Claude
if (-not $claude) {
    Say '  Claude Code CLI not found.' 'Yellow'
    Say '  PolisK+ needs it. The official installer is per-user (no admin):'
    Say '    irm https://claude.ai/install.ps1 | iex'
    if (Ask '  Install Claude Code now?' (-not $Quiet)) {
        try { Invoke-RestMethod https://claude.ai/install.ps1 | Invoke-Expression } catch { Say "  Installer error: $($_.Exception.Message)" 'Red' }
        $claude = Find-Claude
    }
}
if ($claude) {
    Say "  Found: $claude" 'Green'
    $raw = Get-Content $cfgPath -Raw
    $json = (ConvertTo-Json $claude)
    $raw = [regex]::Replace($raw, '"claudePath"\s*:\s*"(?:[^"\\]|\\.)*"', { param($m) '"claudePath": ' + $json })
    [IO.File]::WriteAllText($cfgPath, $raw, (New-Object Text.UTF8Encoding($false)))
} else {
    Say '  Skipped. Install Claude Code later, then run Setup again (or set "claudePath" in config.json).' 'Yellow'
}

# ---------------------------------------------------------------- 4. sign-in
Say ''
Say '[4/5] Checking Claude sign-in...' 'Cyan'
function Get-LoggedIn {
    try {
        $out = (Invoke-Claude $claude @('auth', 'status')) -join "`n"
        return [bool]($out | ConvertFrom-Json).loggedIn
    } catch { return $false }
}
if ($claude) {
    if (Get-LoggedIn) { Say '  Signed in.' 'Green' }
    else {
        Say '  The Claude Code CLI is not signed in. PolisK+ uses your own Claude plan (Pro/Max/Team), no API key.' 'Yellow'
        if (Ask '  Sign in now? (a browser window opens)' (-not $Quiet)) {
            Invoke-Claude $claude @('auth', 'login')
            if (Get-LoggedIn) { Say '  Signed in.' 'Green' } else { Say '  Still not signed in. Run Setup again later.' 'Yellow' }
        }
    }
} else { Say '  Skipped (no CLI).' 'Yellow' }

# ---------------------------------------------------------------- 5. shortcuts + start
Say ''
Say '[5/5] Creating shortcuts and starting PolisK+...' 'Cyan'
$launchArgs = "-NoProfile -ExecutionPolicy Bypass -STA -WindowStyle Hidden -File `"$(Join-Path $dest 'polish.ps1')`""
$icon = Join-Path $dest 'polish.ico'
$sh = New-Object -ComObject WScript.Shell
function New-Shortcut([string]$path, [string]$target, [string]$arguments, [string]$desc, [int]$style = 7, [string]$ico = $icon) {
    $l = $sh.CreateShortcut($path); $l.TargetPath = $target; $l.Arguments = $arguments
    $l.WorkingDirectory = $dest; $l.WindowStyle = $style; $l.Description = $desc
    if ($ico) { $l.IconLocation = "$ico,0" }
    $l.Save()
}
# Shortcuts from versions before 1.1.2 were named "Polish": replace them with the new name.
$programs = [Environment]::GetFolderPath('Programs')
$oldDesktop = Join-Path ([Environment]::GetFolderPath('Desktop')) 'Polish.lnk'
$oldStartup = Join-Path ([Environment]::GetFolderPath('Startup')) 'Polish.lnk'
$hadDesktop = Test-Path $oldDesktop
$hadStartup = Test-Path $oldStartup
foreach ($p in (Join-Path $programs 'Polish'), $oldDesktop, $oldStartup) {
    if (Test-Path $p) { Remove-Item $p -Recurse -Force -ErrorAction SilentlyContinue }
}

$menuDir = Join-Path $programs $AppName
New-Item -ItemType Directory -Force $menuDir | Out-Null
# The main shortcut opens Settings (and starts the app first if it is not running).
New-Shortcut (Join-Path $menuDir "$AppName.lnk") $psExe "$launchArgs -Settings" "$AppName - settings, styles (tone + structure). Use: select text, press the hotkey."
New-Shortcut (Join-Path $menuDir "$AppName settings folder.lnk") (Join-Path $env:SystemRoot 'explorer.exe') "`"$dest`"" 'Styles, rules and config files' 1 ''
New-Shortcut (Join-Path $menuDir "Uninstall $AppName.lnk") $psExe "-NoProfile -ExecutionPolicy Bypass -File `"$(Join-Path $dest 'uninstall.ps1')`"" "Remove $AppName" 1 ''
Say "  Start Menu: $AppName (opens Settings)"

$desktop = Join-Path ([Environment]::GetFolderPath('Desktop')) "$AppName.lnk"
if ($hadDesktop -or (Test-Path $desktop) -or (Ask "  Add a $AppName icon to the Desktop?" $true)) {
    New-Shortcut $desktop $psExe "$launchArgs -Settings" "$AppName - settings, styles (tone + structure)"
    Say "  Desktop icon: $desktop"
}

$startup = Join-Path ([Environment]::GetFolderPath('Startup')) "$AppName.lnk"
if (-not $NoAutostart -and ($hadStartup -or (Ask "  Start $AppName automatically when you sign in to Windows?" $true))) {
    New-Shortcut $startup $psExe $launchArgs "$AppName - select text, press Ctrl+Alt+P"
    Say "  Autostart: $startup"
} elseif (Test-Path $startup) { Remove-Item $startup; Say '  Autostart removed.' }

# Refresh the per-user icon cache so shortcuts show the current icon (no admin needed).
$ie4 = Join-Path $env:SystemRoot 'System32\ie4uinit.exe'
if (Test-Path $ie4) { try { Start-Process $ie4 -ArgumentList '-show' -WindowStyle Hidden -Wait } catch { } }

$logFile = Join-Path $env:LOCALAPPDATA 'Polish\polish.log'
$logBefore = if (Test-Path $logFile) { (Get-Item $logFile).Length } else { 0 }
Start-Process $psExe -ArgumentList $launchArgs -WindowStyle Hidden
Start-Sleep -Seconds 5
$running = Get-CimInstance Win32_Process -Filter "Name='powershell.exe'" | Where-Object { $_.CommandLine -like "*$dest*polish.ps1*" }
$newLog = ''
if ((Test-Path $logFile) -and (Get-Item $logFile).Length -gt $logBefore) {
    $fs = [IO.File]::Open($logFile, 'Open', 'Read', 'ReadWrite'); [void]$fs.Seek($logBefore, 'Begin')
    $newLog = (New-Object IO.StreamReader($fs)).ReadToEnd(); $fs.Dispose()
}
if ($newLog -match 'Startup failed') { Say "  PolisK+ could not start: $($newLog.Trim())" 'Red' }
elseif ($running) { Say '  PolisK+ is running (navy blue "P" icon in the tray; it may be hidden under the ^ arrow).' 'Green' }
else { Say "  PolisK+ did not stay running. See $logFile" 'Red' }

$hotkey = (Get-Content $cfgPath -Raw | ConvertFrom-Json).hotkey
Say ''
Say '=== Done ===' 'Cyan'
Say "  Use: select text in any app, press $hotkey. In the popup, Ctrl+1..9 switches style."
Say '  Settings and styles (tone + structure per situation): Start Menu / Desktop > PolisK+,'
Say '  or double-click the tray icon.'
Say "  Files: $dest"
