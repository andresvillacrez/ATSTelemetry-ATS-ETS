# ATS Telemetry para Windows

Port x64 de **ATS Telemetry** con interfaz **FH6 v2.3**. Lee la telemetría en
modo pasivo desde `Local\SCSTelemetry` y el clima desde la memoria del juego. No
modifica el MMF ni los plugins de Ajazz.

Última actualización: **28 de octubre de 2026**.

## Estado actual (v2.3)

- App final compilada en `dist\ATS-Telemetry-Windows\ATSTelemetry.exe` y paquete
  portátil en `dist\ATS-Telemetry-Windows.zip`.
- **Logo real de empresa:** se lee `logo` del `profile.sii` (`FLACO INC` usa
  `logo_4`) y se carga desde `Assets\CompanyLogos\logo_N.png`. Los 8 PNG son
  texturas reales de `base.scs` (`material/ui/player_logo`), no arte generado.
- **Cabecera de empresa:** nombre a la izquierda; logo de **62 × 62** a la
  derecha con margen interior de **24 px** (`x + width - 24 - 62`, `y + 18`).
  Si falta el PNG, el nombre ocupa todo el ancho.
- **Carga sin miniatura:** se eliminó el dibujo vectorial y los métodos
  auxiliares; nombre y tipo usan todo el ancho del panel.
- **Validado:** demo y telemetría real de ATS a **1280×720** y **1920×1080**,
  sin solapes ni recortes.
- **Pendiente:** ETS2 real, encendido/apagado, avisos inferidos, indicadores
  verticales de agua/aire, reserva de combustible y otros logos de perfil.

## Inicio rápido

```powershell
# Compilar y publicar la app final
.\build.ps1

# Probar sin juego
.\dist\ATS-Telemetry-Windows\ATSTelemetry.exe --demo --windowed

# Ejecutar con el juego abierto
.\dist\ATS-Telemetry-Windows\ATSTelemetry.exe
```

- Instalar el plugin de telemetría: `install-plugin.bat` (cerrar y reabrir el
  juego después).
- Pantalla/modo: menú de la bandeja o `--screen=DISPLAY2`, `--windowed`, `F11`.
- Salir: `Ctrl+Shift+Q` o **Salir** en la bandeja.
- Preferencias: `%LOCALAPPDATA%\ATSTelemetry\window.ini`.

## Qué incluye

- Panel FH6 a 60 FPS, lienzo virtual 1920×1080 escalado al monitor.
- Cluster con velocímetro, crucero, marcha, RPM, odómetro y 13 testigos.
- Paneles de empresa, carga, daños y GPS; agua/aire verticales; combustible,
  avisos y clima en la franja inferior.
- Secuencia de arranque: testigos encendidos 1,25 s y barrido de agujas/barras.
- Clima automático por humedad, con respaldo manual.
- Bandeja de Windows: pantalla, modo ventana, clima y salida.
- Lectura pasiva: no envía teclas ni escribe en el juego.

## Datos clave

- **Telemetría:** `Local\SCSTelemetry`, offsets REV12 en `TelemetryReader.cs`.
- **Clima:** `WeatherReader.cs` lee la humedad de la carretera.
  - ATS 1.61.3.18: puntero `0x3051D40`, humedad `+0x5E8`.
  - ETS2 1.61.1.9: puntero `0x3046780`, humedad `+0x5E8`.
  - Umbrales: `>=0,80` Tormenta, `>=0,15` Lluvia, `>=0,02` Nublado.
  - Si la estructura no valida, usa el clima manual de `window.ini`.
- **Finanzas:** `BankBalanceReader` busca el `game.sii` más reciente en
  Documentos, OneDrive y Steam Cloud; descifra ScsC, infla BSII y lee
  `bank.money_account` y los `profit_log` de los conductores.
- **Empresa y logo:** se leen de `company_name` y `logo` del `profile.sii`.
  El parser acepta valores entrecomillados y sin comillas.
- **Combustible:** litros convertidos a galones estadounidenses
  (`1 L = 0,264172 gal`).
- La app no necesita administrador, pero debe tener la misma o mayor integridad
  que el juego para leer el clima.

## Estructura

```text
Windows Native Final/
  build.ps1 / build.bat          Compila y publica win-x64
  install-plugin.ps1 / .bat      Instala scs-telemetry.dll en ATS/ETS2
  run.bat / run-demo.bat         Lanzadores
  Assets/
    Indicators/                  Testigos e iconos (PNG)
    BrandLogos/                  Logos de marca de camiones
    CompanyLogos/                Logo real del perfil (logo_0..logo_7)
    Plugins/scs-telemetry.dll    Plugin SCS incluido
  src/
    DashboardRenderer.cs         Dibujo FH6 y logo de empresa
    TelemetryReader.cs           MMF REV12 y snapshot demo
    WeatherReader.cs             Clima por humedad
    BankBalanceReader.cs         Savegame/perfil: saldo, empresa y logo
    MainForm.cs                  Pantalla completa, ventana y bandeja
    Snap.cs                      Modelo de telemetría
    DashboardControl.cs          Lienzo SkiaSharp
    SkiaCompat.cs                Compatibilidad de dibujo
    FrameClock.cs                Reloj de 60 FPS
  dist/ATS-Telemetry-Windows/    App final portátil
  README-FH6.md                  Bitácora detallada del rediseño
  GUIA-INTERFAZ.md               Coordenadas, métodos y datos
  HISTORIAL.md                   Historial del proyecto
```

## Entrega actual

- **EXE:** 178.717.438 bytes
  - SHA-256: `A0C8D9E781A1E1C20BEB1E8712A781103740042F5B3B604133BE3A3648664D91`
- **ZIP:** 47 entradas, 71.489.903 bytes
  - SHA-256: `15DAC73CFAB5C2CED9F8DD569952D650359E568C8380B9C42E6A12813942DF9E`

El ZIP se genera sin entrada de carpeta raíz; incluye el EXE, lanzadores,
`LEEME.txt`, iconos, logos de marca, los 8 logos de empresa y el plugin.

## Validación

Hecho:

- [x] Compilación Release sin errores.
- [x] Demo a 1280×720 y 1920×1080.
- [x] ATS real a 1280×720 y 1920×1080.
- [x] `FLACO INC` + `logo_4` a la derecha, nombre a la izquierda.
- [x] Panel de carga sin miniatura.
- [x] ZIP extraído y verificado (47 archivos, 8 logos).

Pendiente:

- [ ] ETS2 real: velocidad, RPM, marcha, testigos, clima, GPS y finanzas.
- [ ] Encendido/apagado y auto-test con telemetría real.
- [ ] Avisos de entregas, multas, peajes y cambios de saldo.
- [ ] Indicadores verticales de agua/aire y reserva de combustible.
- [ ] Otros perfiles con `logo_0`..`logo_7` y logos personalizados de mod.

## Problemas rápidos

- **Sin señal:** comprobar que `scs-telemetry.dll` está instalado y que el juego
  se reinició después de instalarlo.
- **Clima sin cambiar:** revisar `weatherAuto=1`, que haya partida cargada y que
  la app tenga la misma integridad que el juego. Tras una actualización del
  juego pueden cambiar los offsets de `WeatherReader`.
- **No se puede compilar:** cerrar `ATSTelemetry.exe`; el build no puede borrar
  `dist` si el EXE está en uso.
- **SmartScreen:** el EXE no está firmado; usar “Más información” y “Ejecutar de
  todas formas”.
- Si se mueve el EXE, mover también la carpeta `Assets`.

## Documentación detallada

- [README-FH6.md](README-FH6.md): decisiones, iteraciones y capturas del rediseño.
- [GUIA-INTERFAZ.md](GUIA-INTERFAZ.md): coordenadas, métodos y fuentes de datos.
- [HISTORIAL.md](HISTORIAL.md): historial completo y estado de validación.

## Próximos pasos

1. Probar la app final con ETS2 real.
2. Completar la validación de encendido/apagado, avisos y barras verticales.
3. Probar los 8 logos y el fallback de logos de mod sin PNG.
4. Si se cambia código o recursos: ejecutar `.\build.ps1`, regenerar el ZIP y
   actualizar tamaños/SHA-256 en este README y en `HISTORIAL.md`.
