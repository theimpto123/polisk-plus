@echo off
rem PolisK+ setup: per-user install, no admin needed.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0setup.ps1" %*
echo.
pause
