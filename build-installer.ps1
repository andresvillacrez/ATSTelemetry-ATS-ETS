param(
    [switch]$SkipAppBuild,
    [switch]$KeepPayload
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$dotnetCommand = Get-Command dotnet -ErrorAction SilentlyContinue
if ($null -eq $dotnetCommand) {
    $local = Join-Path $env:LOCALAPPDATA "Temp\opencode\dotnet8\dotnet.exe"
    if (Test-Path -LiteralPath $local) {
        $dotnetPath = $local
    } else {
        throw "No se encontró el SDK de .NET 8."
    }
} else {
    $dotnetPath = $dotnetCommand.Source
}

$env:PATH = (Split-Path -Parent $dotnetPath) + ";" + $env:PATH

if (-not $SkipAppBuild) {
    Write-Host "Compilando la aplicación..." -ForegroundColor Cyan
    & (Join-Path $root "build.ps1")
    if ($LASTEXITCODE -ne 0) {
        throw "Falló la compilación de la aplicación."
    }
}

$dist = Join-Path $root "dist\ATS-Telemetry-Windows"
$setupProject = Join-Path $root "installer\ATSTelemetry.Setup.csproj"
$payload = Join-Path $root "installer\payload.zip"

if (-not (Test-Path -LiteralPath (Join-Path $dist "ATSTelemetry.exe"))) {
    throw "No existe dist\ATS-Telemetry-Windows\ATSTelemetry.exe."
}

Write-Host "Empaquetando la aplicación..." -ForegroundColor Cyan
Get-Process -Name ATSTelemetry -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500

if (Test-Path -LiteralPath $payload) {
    Remove-Item -LiteralPath $payload -Force
}

Compress-Archive -Path (Join-Path $dist "*") -DestinationPath $payload -CompressionLevel Optimal -ErrorAction Stop
if (-not (Test-Path -LiteralPath $payload)) {
    throw "No se generó el paquete de la aplicación."
}

$payloadLength = (Get-Item -LiteralPath $payload).Length
if ($payloadLength -lt 50MB) {
    throw "El paquete de la aplicación es demasiado pequeño ($([Math]::Round($payloadLength / 1MB, 1)) MB). Revisa que ATSTelemetry.exe no esté bloqueado."
}

$payloadMb = [Math]::Round($payloadLength / 1MB, 1)
Write-Host "Payload: $payloadMb MB" -ForegroundColor DarkGray

$out = Join-Path $root "dist\ATS-Telemetry-Setup"
if (Test-Path -LiteralPath $out) {
    Remove-Item -LiteralPath $out -Recurse -Force
}

Write-Host "Generando instalador..." -ForegroundColor Cyan
& $dotnetPath publish $setupProject `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -o $out

if ($LASTEXITCODE -ne 0) {
    throw "Falló la publicación del instalador."
}

$setupExe = Join-Path $out "ATSTelemetrySetup.exe"
if (-not (Test-Path -LiteralPath $setupExe)) {
    throw "No se generó ATSTelemetrySetup.exe."
}

$finalSetup = Join-Path $root "dist\ATS-Telemetry-Setup.exe"
Copy-Item -LiteralPath $setupExe -Destination $finalSetup -Force

if (-not $KeepPayload) {
    Remove-Item -LiteralPath $payload -Force
}

$sizeMb = [Math]::Round((Get-Item -LiteralPath $finalSetup).Length / 1MB, 1)
$hash = (Get-FileHash -LiteralPath $finalSetup -Algorithm SHA256).Hash
Write-Host ""
Write-Host "Instalador listo: $finalSetup" -ForegroundColor Green
Write-Host "Tamaño: $sizeMb MB"
Write-Host "SHA-256: $hash"