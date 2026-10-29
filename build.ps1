$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$project = Join-Path $root "src\ATSTelemetry.Windows.csproj"
$output = Join-Path $root "dist\ATS-Telemetry-Windows"

Write-Host ""
Write-Host "ATS Telemetry - build Windows x64" -ForegroundColor Cyan
Write-Host "Proyecto: $project"
Write-Host "Salida:   $output"
Write-Host ""

$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if ($null -eq $dotnet) {
    Write-Host "No se encontro el SDK de .NET." -ForegroundColor Red
    Write-Host "Instala .NET 8 SDK x64 desde https://dotnet.microsoft.com/download/dotnet/8.0"
    Write-Host "y vuelve a ejecutar build.bat."
    exit 1
}

Write-Host "SDK: $(dotnet --version)"

if (Test-Path $output) {
    try {
        Remove-Item $output -Recurse -Force
    } catch {
        Write-Host ""
        Write-Host "No se pudo limpiar la salida anterior." -ForegroundColor Red
        Write-Host "Cierra ATSTelemetry.exe (Ctrl + Shift + Q o desde la bandeja) y vuelve a intentar." -ForegroundColor Yellow
        exit 1
    }
}

dotnet publish $project `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -o $output

if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "La compilacion fallo." -ForegroundColor Red
    exit $LASTEXITCODE
}

$assets = Join-Path $output "Assets\Indicators"
$plugin = Join-Path $output "Assets\Plugins\scs-telemetry.dll"
if (-not (Test-Path $assets) -or -not (Test-Path $plugin)) {
    Write-Host "Aviso: faltan recursos. Copiando Assets..." -ForegroundColor Yellow
    Copy-Item (Join-Path $root "Assets") $output -Recurse -Force
}

$installer = Join-Path $output "install-plugin.ps1"
if (-not (Test-Path $installer)) {
    Copy-Item (Join-Path $root "install-plugin.ps1") $output -Force
    Copy-Item (Join-Path $root "install-plugin.bat") $output -Force
}

$exe = Join-Path $output "ATSTelemetry.exe"
if (-not (Test-Path $exe)) {
    Write-Host "No se genero ATSTelemetry.exe." -ForegroundColor Red
    exit 1
}

# ---------------------------------------------------------------------------
# Carpeta portatil: lanzadores y guia rapida
# ---------------------------------------------------------------------------

$launcher = @'
@echo off
setlocal
cd /d "%~dp0"
if not exist "ATSTelemetry.exe" (
    echo No se encontro ATSTelemetry.exe en esta carpeta.
    pause
    exit /b 1
)
start "" "ATSTelemetry.exe"
exit /b 0
'@

$demoLauncher = @'
@echo off
setlocal
cd /d "%~dp0"
if not exist "ATSTelemetry.exe" (
    echo No se encontro ATSTelemetry.exe en esta carpeta.
    pause
    exit /b 1
)
start "" "ATSTelemetry.exe" --demo
exit /b 0
'@

$installerLauncher = @'
@echo off
setlocal
cd /d "%~dp0"
if not exist "install-plugin.bat" (
    echo No se encontro install-plugin.bat en esta carpeta.
    pause
    exit /b 1
)
call "install-plugin.bat"
exit /b %errorlevel%
'@

$readme = @'
ATS TELEMETRY PARA WINDOWS
==========================

Panel de telemetria para American Truck Simulator (ATS) y
Euro Truck Simulator 2 (ETS2), pensado para una pantalla adicional
de 1920x1080. Lee los datos del plugin SCS en modo solo lectura.

Esta carpeta es portatil: se puede copiar a un pendrive o a otra
carpeta y funciona sin instalar .NET.


COMO INICIAR
------------

1. Abre el juego (ATS o ETS2) y entra a una partida.
2. Doble clic en "Iniciar ATS Telemetry.bat".
3. El panel aparece en la pantalla donde tengas el mouse. Recuerda la
   ultima pantalla y el ultimo modo (completo o ventana).
4. Para moverlo: clic derecho en el icono de la bandeja -> Pantalla,
   y elige el monitor. Tambien puedes usar "Modo ventana" y arrastrarlo.
5. Para salir: Ctrl + Shift + Q, o clic derecho en el icono de la
   bandeja del sistema y elige "Salir".


SI EL PANEL DICE "SIN SENAL"
----------------------------

El plugin scs-telemetry.dll debe estar instalado en el juego:

1. Cierra ATS y ETS2.
2. Doble clic en "Instalar plugin en ATS y ETS2.bat".
3. Acepta el aviso de permisos de administrador.
4. Abre el juego y entra a una partida.

El instalador detecta automaticamente las carpetas de Steam de
ambos juegos. Si el juego esta en otra unidad, tambien se puede
ejecutar install-plugin.bat pasandole la carpeta del juego.


MODO DEMO
---------

Doble clic en "Iniciar en modo demo.bat" para ver el panel con
datos de prueba, sin abrir el juego.


ELEGIR PANTALLA Y MODO VENTANA
-----------------------------

No hay una pantalla obligatoria. Al iniciar, el panel aparece en la
pantalla donde tengas el cursor y recuerda la ultima que usaste.

Desde el icono de la bandeja:

  Pantalla      muestra todos los monitores detectados
  Modo ventana  convierte el panel en una ventana movible (F11)

Tambien puedes elegir una pantalla al iniciar:

  --screen=DISPLAY2    segunda pantalla
  --screen=DISPLAY3    tercera pantalla
  --windowed           arranca en modo ventana
  --screen=PM1562      busca por nombre de monitor (opcional)

Ejemplo:

  "C:\ATS-Telemetry-Windows\ATSTelemetry.exe" --screen=DISPLAY2

La app recuerda la pantalla y el modo en:
  %LOCALAPPDATA%\ATSTelemetry\window.ini


CLIMA
-----

El panel lee el clima real del juego en tiempo real. Usa la humedad
de la carretera que el juego mantiene en memoria y la convierte en
cuatro estados: Despejado, Nublado, Lluvia y Tormenta.

Desde el icono de la bandeja -> Clima puedes elegir "Automatico
(juego)" o fijar uno de los cuatro estados manualmente. La seleccion
se guarda y se mantiene al reiniciar.

Si el juego se actualiza y cambia su estructura interna, el panel
vuelve automaticamente al clima manual sin dejar de mostrar la
telemetria. El panel muestra la hora del juego, si es de dia o de
noche y el icono del estado actual.


FINANZAS Y COMBUSTIBLE
----------------------

El panel de finanzas muestra el nombre de la empresa, su logotipo real,
el ingreso del trabajo actual, los ingresos de los conductores contratados,
los egresos de la ruta y el dinero total.

La empresa, el logo y los conductores se leen del perfil y del savegame del
juego activo. El logo es el que elegiste en el perfil (por ejemplo, logo_4).
El dinero total es el saldo real de la cuenta bancaria. Los datos se
actualizan al autoguardar o al hacer un guardado rapido.

El panel de combustible lleva el titulo COMBUSTIBLE.


CONTENIDO DE LA CARPETA
-----------------------

  ATSTelemetry.exe                  Programa principal
  ATSTelemetry.ico                  Icono para accesos directos
  Iniciar ATS Telemetry.bat         Abre el panel
  Iniciar en modo demo.bat          Abre el panel con datos falsos
  Instalar plugin en ATS y ETS2.bat Instala el plugin de telemetria
  install-plugin.bat / .ps1         Instalador del plugin
  Assets\Indicators                 Testigos e iconos del panel
  Assets\BrandLogos                 Logotipos de marca de los camiones
  Assets\CompanyLogos               Logos reales del perfil del juego
  Assets\Plugins\scs-telemetry.dll  Plugin de telemetria SCS
  LEEME.txt                         Este archivo


NOTAS
-----

- El ejecutable es autocontenido: no necesita .NET instalado.
- No esta firmado. Windows puede mostrar SmartScreen la primera vez;
  elige "Mas informacion" y "Ejecutar de todas formas".
- La app no modifica ni sustituye los plugins de Ajazz
  (com.hsgaming.scsmon ni com.hsgaming.vitals).
- DINERO TOTAL y los ingresos de conductores se leen del savegame del
  juego activo y se actualizan cuando el juego autoguarda o haces un
  guardado rapido.
- El nombre y el logo de la empresa se leen del profile.sii del perfil
  activo; el logo se carga de Assets\CompanyLogos.
- La app no necesita ejecutarse como administrador. Si el juego se
  ejecuta como administrador, ejecuta tambien la app como administrador
  para poder leer el clima.
- Verificado con ETS2 1.61.1.9 y ATS 1.61.3.18: telemetria en vivo,
  clima automatico (tambien sin elevacion), empresa, conductores y
  saldo bancario correctos del juego activo.
'@

$utf8Bom = New-Object System.Text.UTF8Encoding($true)
$ascii = [System.Text.Encoding]::ASCII

function Convert-ToCrlf([string]$text) {
    return ($text -replace "`r`n", "`n") -replace "`n", "`r`n"
}

[System.IO.File]::WriteAllText((Join-Path $output "Iniciar ATS Telemetry.bat"), (Convert-ToCrlf $launcher), $ascii)
[System.IO.File]::WriteAllText((Join-Path $output "Iniciar en modo demo.bat"), (Convert-ToCrlf $demoLauncher), $ascii)
[System.IO.File]::WriteAllText((Join-Path $output "Instalar plugin en ATS y ETS2.bat"), (Convert-ToCrlf $installerLauncher), $ascii)
[System.IO.File]::WriteAllText((Join-Path $output "LEEME.txt"), (Convert-ToCrlf $readme), $utf8Bom)

Write-Host ""
Write-Host "Listo: $exe" -ForegroundColor Green
Write-Host "Carpeta portatil: $output" -ForegroundColor Green
Write-Host ""
Write-Host "Ejecutar:        $exe"
Write-Host "Modo demo:      $exe --demo"
Write-Host "Pantalla:       se elige desde la bandeja (menu Pantalla)"
Write-Host "Modo ventana:   $exe --windowed  (o F11)"
Write-Host ""
