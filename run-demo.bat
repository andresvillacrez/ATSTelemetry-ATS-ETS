@echo off
setlocal
set "EXE=%~dp0dist\ATS-Telemetry-Windows\ATSTelemetry.exe"
if not exist "%EXE%" (
    echo No existe el ejecutable. Ejecuta build.bat primero.
    pause
    exit /b 1
)
start "" "%EXE%" --demo
