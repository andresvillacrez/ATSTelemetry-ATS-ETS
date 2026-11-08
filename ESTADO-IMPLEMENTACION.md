# Estado de implementación — modernización v3

Actualizado: 2026-11-04

## Fases completadas

- **Fase 0 — Quick wins**: tokens (`Theme`, `TypeScale`), superficies planas, barra de título oscura, menú de bandeja oscuro, cromo técnico fuera de la vista principal, demo Full/Compact revisada, `build.bat` y `run-demo.bat` validados.
- **Fase 1 — Sistema y componentes**: `DrawSectionHeader`, `DrawMetricRow`, `DrawChip`, `DrawEmptyState`; iconos vectoriales de onboarding; estado sin señal unificado; badge DEMO; Ajustes con diagnóstico; preferencias persistidas.
- **Fase 2 — Layout responsive**: Full/Compact/Minimal; canvas dinámico para ultrawide y 16:10; paneles anclados a bordes; cluster centrado; validado a 1920×1080, 2560×1440, 3440×1440, 5120×1440, 1920×1200, 1280×720, 1366×768, 960×540 y 1024×600.
- **Fase 3 — Movimiento y rendimiento**:
  - `AnimatedValue` suaviza velocidad, RPM, combustible, agua y aire en Full/Compact/Minimal.
  - `ReduceMotion` hace `Snap` y desactiva pulsos.
  - Fondo y halos cacheados en `SKPicture` por tamaño de canvas.
  - Overlay de diagnóstico opcional (FPS, frame, GC) con preferencia `showDiagnostics`.
  - Presupuesto CPU medido: Full 8,97 ms; ultrawide 12,39 ms; Compact 3,18 ms; Minimal 1,53 ms.
- **Fase 4 — Integración con Windows y overlay**:
  - Tema oscuro y alto contraste (`theme=Dark|Contrast`) aplicable en caliente e invalidando la caché del fondo.
  - Preferencias persistentes ampliadas y diagnóstico de pantalla/ruta.
  - Overlay con opacidad, clic a través y atajo `Ctrl+Shift+O`.
  - Guardia de seguridad: el clic a través se desactiva solo a los 30 minutos.
  - Atajos globales de overlay, ajustes y layout.
  - Persistencia de bounds verificada.
- **Fase 5 — Accesibilidad y contenido**:
  - Contraste: `TextTertiary` oscuro elevado a 4,82:1; etiquetas activas usan tokens.
  - Daltonismo: daños con porcentaje y etiqueta textual.
  - Escala tipográfica 100/125/150 % persistida.
  - Unidades métrico/imperial para velocidad, temperatura, presión y volumen.
  - Item 3.3: marcos de panel cacheados además del fondo.
- **Fase 6 — Paneles (bloque 10)**:
  - Empresa, Carga y GPS con la jerarquía de 3 datos primarios y detalle secundario.
  - Daños con imagen degradada, nombre del modelo y etiquetas de estado.
  - Combustible con litros/galones, autonomía y marcador de reserva.
  - Matriz de pruebas ejecutada en 8 resoluciones, demo/sin señal, contraste, 150 % e imperial.

## Correcciones recientes

- Clima eliminado del menú de bandeja; el lector queda siempre en modo automático.
- Submenús de “Diseño” y “Pantalla” con texto claro sobre fondo oscuro (incluye items reconstruidos).
- Ajustes se abre por delante del panel principal: se baja temporalmente el `TopMost` del panel y el diálogo se muestra `TopMost` + `BringToFront`.
- Checkboxes de Ajustes con control propio (`DarkCheckBox`) derivado de `Control`: sin pintado nativo, texto y palomilla siempre visibles.
- Botones “Ajustes” y “Créditos” dibujados en la cabecera Full, con hit-testing y cursor de mano.
- Diálogo de Créditos con creador, derechos, tecnologías y agradecimientos; también accesible desde la bandeja.
- Selectores de Ajustes con control propio (`DarkSelect`): elimina los bordes blancos del ComboBox nativo y respeta el tema. Incluye `SupportsTransparentBackColor` para evitar el bloqueo al abrir Ajustes.
- Tema claro (`theme=Light`) con la misma retícula, jerarquía y acentos, adaptado a fondos blancos.
- Título de ventana claro/oscuro según el tema y menús de bandeja reaplicados al cambiar.

## Pendiente

- [x] Reflow del layout para 150 %: `DashboardLayout.ForSize` recibe `Theme.UiScale` y selecciona Compact/Minimal cuando el espacio efectivo es menor. (2026-11-07)
- [x] Idioma es/en: infraestructura `Localization` + selector en Ajustes + preferencia persistida + textos clave migrados. (2026-11-07)
- [x] Consumo L/100 km / MPG anadido al panel de combustible; demo corregido. (2026-11-07)
- [x] Detalle fino: nombre del modelo sobre la imagen de danos; textos clave migrados. (2026-11-07)

## Comandos

```powershell
$dotnet = "C:\Users\hs-se\AppData\Local\Temp\opencode\dotnet8\dotnet.exe"
$proj = "E:\Proyectos de software\APP Telemetria\Windows Native Final\src\ATSTelemetry.Windows.csproj"
& $dotnet build $proj -c Release -r win-x64 -p:DebugType=None -p:DebugSymbols=false
```

## Distribución final

- `dist\ATS-Telemetry-Windows\ATSTelemetry.exe` compilado con `build.ps1` (single-file, self-contained).
- Verificado que arranca en modo demo y que `Assets` está incluido.
- **Instalador completo**: `dist\ATS-Telemetry-Setup.exe`.
  - Instalación por usuario en `%LocalAppData%\Programs\ATS Telemetry`.
  - Extrae la aplicación y los recursos, instala `scs-telemetry.dll` en ATS/ETS2 si se solicita.
  - Crea accesos directos en el escritorio y en el menú Inicio.
  - Registra la entrada de desinstalación en `HKCU`.
  - Incluye `uninstall.cmd` y desinstalador en el menú Inicio.
  - Generado con `build-installer.bat` (payload comprimido embebido).
  - Tamaño: 226.7 MB · SHA-256: `4576DAEBC3A5E9BAEFCF98C11C7EF5661F44951E19F58E8064A4DA69676A8E02`.

## Notas

- `git` no está disponible en PATH; no hay commits.
- SDK 8 local en `C:\Users\hs-se\AppData\Local\Temp\opencode\dotnet8`.
- Preferencias reales: `%LOCALAPPDATA%\ATSTelemetry\window.ini`.
- Acceso directo temporal: `ATSTelemetry temporal.lnk` → `src\bin\Release\net8.0-windows\win-x64\ATSTelemetry.exe`.
