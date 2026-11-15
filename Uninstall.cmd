@echo off
rem Removes PolisK+ (your styles, rules and config are backed up to Documents first).
if exist "%LOCALAPPDATA%\Programs\Polish\uninstall.ps1" (
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%LOCALAPPDATA%\Programs\Polish\uninstall.ps1"
) else (
  echo PolisK+ is not installed for this user.
  pause
)
