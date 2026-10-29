@echo off
setlocal
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0install-plugin.ps1" %*
if errorlevel 1 (
    echo.
    echo No se pudo instalar el plugin de telemetria.
    pause
    exit /b 1
)
echo.
pause
