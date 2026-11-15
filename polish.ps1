# PolisK+ launcher (internal name: Polish): compiles the C# sources in memory and runs the tray app.
# Start with:  powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -WindowStyle Hidden -File polish.ps1 [-Settings]
#   -Settings  open the Settings window (if Polish is already running, the running copy opens it)
param([switch]$Settings)
$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$logDir = Join-Path $env:LOCALAPPDATA 'Polish'

# Already running? Ask that copy to show Settings and stop here (no compile needed).
try {
    $evt = [Threading.EventWaitHandle]::OpenExisting('Local\PolishTool.ShowSettings')
    if ($Settings) { [void]$evt.Set() }
    $evt.Dispose()
    exit 0
}
catch [Threading.WaitHandleCannotBeOpenedException] { }
catch { }

try {
    if ([Threading.Thread]::CurrentThread.GetApartmentState() -ne 'STA') {
        throw 'PolisK+ must run in an STA thread. Start it with powershell.exe -STA.'
    }
    $refs = 'System.Windows.Forms', 'System.Drawing', 'System.Web.Extensions', 'System.Security', 'UIAutomationClient', 'UIAutomationTypes', 'WindowsBase'
    Add-Type -AssemblyName $refs
    $files = 'Polish.cs', 'Profiles.cs', 'Providers.cs', 'SettingsForm.cs'
    $sources = [string[]]@($files | ForEach-Object { [IO.File]::ReadAllText((Join-Path $here $_), [Text.Encoding]::UTF8) })

    $provider = New-Object Microsoft.CSharp.CSharpCodeProvider
    $cp = New-Object System.CodeDom.Compiler.CompilerParameters
    $cp.GenerateInMemory = $true
    $cp.GenerateExecutable = $false
    $cp.CompilerOptions = '/optimize'
    $cp.ReferencedAssemblies.Add('System.dll') | Out-Null
    $cp.ReferencedAssemblies.Add('System.Core.dll') | Out-Null
    foreach ($r in $refs) { $cp.ReferencedAssemblies.Add([Reflection.Assembly]::LoadWithPartialName($r).Location) | Out-Null }
    $res = $provider.CompileAssemblyFromSource($cp, $sources)
    $errs = @($res.Errors | Where-Object { -not $_.IsWarning })
    if ($errs.Count -gt 0) {
        throw ('Compile error: ' + (($errs | Select-Object -First 5 | ForEach-Object { "$([IO.Path]::GetFileName($_.FileName)) line $($_.Line): $($_.ErrorText)" }) -join '; '))
    }
    $app = $res.CompiledAssembly.GetType('PolishTool.App')
    try { $app.GetMethod('Run').Invoke($null, [object[]]@([string]$here, [bool]$Settings.IsPresent)) | Out-Null }
    catch [Reflection.TargetInvocationException] { throw $_.Exception.InnerException }
}
catch {
    $msg = $_.Exception.Message
    if ($_.Exception.InnerException) { $msg = $_.Exception.InnerException.Message }
    New-Item -ItemType Directory -Force $logDir | Out-Null
    "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')  Startup failed: $msg" |
        Out-File -Append -Encoding utf8 (Join-Path $logDir 'polish.log')
    Add-Type -AssemblyName System.Windows.Forms
    [System.Windows.Forms.MessageBox]::Show("PolisK+ could not start:`n`n$msg", 'PolisK+') | Out-Null
}
