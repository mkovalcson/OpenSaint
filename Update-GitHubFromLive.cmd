@echo off
setlocal
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0Update-GitHubFromLive.ps1" %*
exit /b %ERRORLEVEL%
