@echo off
setlocal
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0Create-Deploy.ps1" %*
exit /b %ERRORLEVEL%
