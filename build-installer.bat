@echo off
setlocal
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build-installer.ps1"
if errorlevel 1 (
    echo.
    echo La generacion del instalador fallo.
    pause
    exit /b 1
)
echo.
pause