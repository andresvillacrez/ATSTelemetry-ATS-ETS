param(
    [string[]]$GameDir = @()
)

$ErrorActionPreference = "Stop"

function Test-Admin {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

if (-not (Test-Admin)) {
    Write-Host "Se necesitan permisos de administrador para escribir en las carpetas de los juegos." -ForegroundColor Yellow
    $arguments = @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", "`"$PSCommandPath`"")
    foreach ($game in $GameDir) {
        $arguments += "-GameDir"
        $arguments += "`"$game`""
    }
    Start-Process -FilePath "powershell.exe" -ArgumentList $arguments -Verb RunAs
    exit
}

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$sourceCandidates = @(
    (Join-Path $root "Assets\Plugins\scs-telemetry.dll"),
    (Join-Path $root "..\Assets\Plugins\scs-telemetry.dll"),
    (Join-Path $root "dist\ATS-Telemetry-Windows\Assets\Plugins\scs-telemetry.dll")
)

$source = $sourceCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $source) {
    Write-Host "No se encontro scs-telemetry.dll junto a este script." -ForegroundColor Red
    Write-Host "Ejecuta build.bat para copiar el plugin a Assets\Plugins."
    exit 1
}

$source = (Resolve-Path -LiteralPath $source).Path

function Get-SteamRoots {
    $roots = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)

    $registryCandidates = @(
        @("HKCU:\Software\Valve\Steam", "SteamPath"),
        @("HKLM:\SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath"),
        @("HKLM:\SOFTWARE\Valve\Steam", "InstallPath")
    )

    foreach ($entry in $registryCandidates) {
        try {
            $value = (Get-ItemProperty -Path $entry[0] -Name $entry[1] -ErrorAction Stop).($entry[1])
            if ($value) {
                [void]$roots.Add(($value -replace '/', '\').TrimEnd('\'))
            }
        } catch {
        }
    }

    foreach ($base in @($env:ProgramFiles, ${env:ProgramFiles(x86)})) {
        if ($base) {
            [void]$roots.Add((Join-Path $base "Steam"))
        }
    }

    return @($roots | Where-Object { Test-Path -LiteralPath $_ })
}

function Get-SteamLibraries {
    $libraries = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)

    foreach ($root in Get-SteamRoots) {
        [void]$libraries.Add($root)

        $vdf = Join-Path $root "steamapps\libraryfolders.vdf"
        if (-not (Test-Path -LiteralPath $vdf)) {
            continue
        }

        try {
            $text = Get-Content -LiteralPath $vdf -Raw
            foreach ($match in [regex]::Matches($text, '"path"\s+"([^"]+)"')) {
                $path = $match.Groups[1].Value -replace '\\\\', '\'
                if (Test-Path -LiteralPath $path) {
                    [void]$libraries.Add($path.TrimEnd('\'))
                }
            }
        } catch {
        }
    }

    return @($libraries)
}

function Find-GameDirs {
    $games = New-Object System.Collections.Generic.List[string]

    foreach ($library in Get-SteamLibraries) {
        $common = Join-Path $library "steamapps\common"
        if (-not (Test-Path -LiteralPath $common)) {
            continue
        }

        foreach ($name in @("American Truck Simulator", "Euro Truck Simulator 2")) {
            foreach ($candidate in @((Join-Path $common $name), (Join-Path $common ($name + " (64-bit)")))) {
                if (Test-Path -LiteralPath $candidate) {
                    $games.Add($candidate)
                }
            }
        }
    }

    foreach ($dir in $GameDir) {
        if ($dir) {
            $games.Add($dir)
        }
    }

    $unique = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($game in $games) {
        [void]$unique.Add($game)
    }
    return @($unique)
}

function Resolve-GameRoot {
    param([string]$Path)

    $candidate = (Resolve-Path -LiteralPath $Path).Path.TrimEnd('\')

    if (Test-Path -LiteralPath (Join-Path $candidate "bin\win_x64")) {
        return $candidate
    }
    if (Test-Path -LiteralPath (Join-Path $candidate "win_x64")) {
        return (Split-Path -Parent $candidate)
    }
    if (Test-Path -LiteralPath (Join-Path $candidate "*.exe")) {
        return (Split-Path -Parent (Split-Path -Parent $candidate))
    }

    return $candidate
}

$gameDirs = Find-GameDirs
if ($gameDirs.Count -eq 0) {
    Write-Host "No se encontro ATS ni ETS2 en las bibliotecas de Steam." -ForegroundColor Yellow
    Write-Host "Indica la carpeta del juego manualmente, por ejemplo:"
    Write-Host '  .\install-plugin.ps1 -GameDir "D:\SteamLibrary\steamapps\common\American Truck Simulator"'
    exit 1
}

Write-Host ""
Write-Host "ATS Telemetry - instalar plugin de telemetria" -ForegroundColor Cyan
Write-Host "Origen: $source"
Write-Host ""

$installed = $false

foreach ($gameDir in $gameDirs) {
    try {
        $rootDir = Resolve-GameRoot -Path $gameDir
        $plugins = Join-Path $rootDir "bin\win_x64\plugins"

        if (-not (Test-Path -LiteralPath $plugins)) {
            New-Item -ItemType Directory -Path $plugins -Force | Out-Null
        }

        $target = Join-Path $plugins "scs-telemetry.dll"
        Copy-Item -LiteralPath $source -Destination $target -Force

        $gameName = Split-Path -Leaf $rootDir
        Write-Host "  OK  $gameName" -ForegroundColor Green
        Write-Host "      $target"
        $installed = $true
    } catch {
        Write-Host "  ERROR al instalar en $gameDir" -ForegroundColor Red
        Write-Host "        $($_.Exception.Message)"
    }
}

Write-Host ""

if (-not $installed) {
    Write-Host "No se instalo el plugin en ningun juego." -ForegroundColor Red
    exit 1
}

$running = Get-Process -Name "amtrucks", "eurotrucks2" -ErrorAction SilentlyContinue
if ($running) {
    Write-Host "ATS/ETS2 esta abierto. Cierralo y vuelve a abrirlo para cargar el plugin." -ForegroundColor Yellow
} else {
    Write-Host "Listo. Abre ATS/ETS2 y luego la app ATS Telemetry." -ForegroundColor Green
}
