@echo off
rem PolisK+ setup: per-user install, no admin needed.
if not exist "%~dp0setup.ps1" goto notextracted
rem Files from a downloaded zip carry Windows' "downloaded from the internet" mark. Remove it so PowerShell runs them.
set "POLISK_PKG=%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "Get-ChildItem -LiteralPath $env:POLISK_PKG -Recurse -File | Unblock-File -ErrorAction SilentlyContinue"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0setup.ps1" %*
echo.
pause
exit /b

:notextracted
echo.
echo setup.ps1 was not found next to Setup.cmd.
echo Setup.cmd was probably opened from inside the zip without extracting it.
echo.
echo Right-click the downloaded zip, choose "Extract All...", open the extracted
echo folder, then double-click Setup.cmd there.
echo.
pause
