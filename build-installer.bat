@echo off
setlocal
cd /d "%~dp0"
echo Starting WinFloat packaging process...
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0build-installer.ps1" %*
if errorlevel 1 (
    echo.
    echo [ERROR] Packaging failed with exit code %errorlevel%.
    pause
    exit /b %errorlevel%
)
echo.
pause
