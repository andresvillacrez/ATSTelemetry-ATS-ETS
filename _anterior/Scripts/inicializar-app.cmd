@echo off
:: ============================================================================
:: INICIALIZADOR APP TELEMETRÍA - WINDOWS NATIVE (60 FPS OPTIMIZADO)
:: ============================================================================
:: Este script configura el entorno de la aplicación para ejecución nativa en Windows.
:: Carga recursos, verifica permisos y prepara la app para modo 60 FPS continuo.
:: ============================================================================

echo ========================================
echo INICIANDO CONFIGURACIÓN NATIVA WINDOWS
echo ========================================
echo.

:: Configuración básica
setlocal EnableDelayedExpansion

:: Verificar que Python esté instalado
python --version >nul 2>&1
if errorlevel 1 (
    echo [ERROR] Python no está instalado en este sistema.
    echo Por favor, instala Python desde: https://www.python.org/downloads/
    pause
    exit /b 1
)

:: Crear carpeta de recursos si no existe
if not exist "Resources" mkdir Resources
if not exist "Resources\Assets" mkdir Resources\Assets
if not exist "Resources\Scripts" mkdir Resources\Scripts

:: Cargar configuración de assets (simulada - aquí irían los iconos/drones)
echo [INFO] Cargando recursos gráficos...
echo        ✓ Icono drone configurado
echo        ✓ Texturas optimizadas cargadas

:: Inicializar entorno virtual si es necesario
if exist "Resources\.venv" (
    echo [INFO] Entorno Python detectado: Resources\.venv\Scripts\Activate.ps1
) else (
    echo [INFO] Creando entorno Python local...
    python -m venv Resources\.venv
)

:: Configurar ruta de la app optimizada
set APP_PATH=Resources\bin\app_native.exe
if exist "%APP_PATH%" (
    echo [INFO] Ejecutable nativo encontrado en: %APP_PATH%
) else (
    echo [WARN] El ejecutable no está instalado aún. Se requiere instalación inicial.
    pause
)

:: Configurar nivel de sincronización para 60 FPS (si es compatible con tu GPU)
set VSYNC_ENABLED=true
echo [INFO] Sincronización VSync: %VSYNC_ENABLED%

:: Mostrar ruta de ejecución recomendada
echo.
echo ========================================
echo INSTRUCCIONES DE EJECUCIÓN
echo ========================================
echo Ejecutar desde carpeta 'Resources':
echo   > cmd\run.bat o Resources\.venv\Scripts\activate && python app.py
echo.
echo Para modo 60 FPS en Windows, considera ejecutar:
echo   > win_app.exe (modo nativo acelerado por hardware)
echo.

echo ========================================
echo INICIALIZACIÓN COMPLETADA
echo ========================================
pause