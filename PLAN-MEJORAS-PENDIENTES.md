# Plan de mejoras pendientes - ATS Telemetry (Windows Native Final)

**Base:** `Windows Native Final` - FH6 v2.7, fases 0-6 completadas (2026-11-04).  
**Referencia de estado:** `ESTADO-IMPLEMENTACION.md`, `GUIA-MODERNIZACION-v3.md`.  
**Metodo:** implementacion incremental por punto, con validacion y undo.  
**Restricciones:** no modificar `src\bin`, `src\obj` ni `dist\ATS-Telemetry-Windows`; compilar con `build.bat`/`build.ps1`.

> Lo que ya esta hecho (no repetir): tokens (`Theme`, `TypeScale`), superficies planas, barra de titulo oscura, menu de bandeja oscuro, layout Full/Compact/Minimal responsive, animaciones (`AnimatedValue`, `ReduceMotion`), cache de fondo/marcos en `SKPicture`, overlay con clic a traves y atajos, accesibilidad (contraste, daltonismo, escala 100/125/150 %, unidades metrico/imperial), paneles FH6 (empresa, carga, daños, GPS, combustible, avisos, clima), encendido/apagado con chequeo de bombillas, imagenes de camion con fundido, clima automatico, instalador completo.

---

## Pendientes confirmados (de ESTADO-IMPLEMENTACION.md)

### P1 - Reflow completo del layout para 150 %

**Estado:** IMPLEMENTADO (2026-11-07). `DashboardLayout.ForSize` recibe `Theme.UiScale` y
selecciona Compact/Minimal cuando el espacio efectivo es menor, evitando solapamientos.
**Objetivo cumplido parcialmente:** reflow por cambio de layout segun escala.

Trabajo:
- En `Layout.cs`, anadir variantes de `Full`/`Compact`/`Minimal` para escala 1.25 y 1.5, o calcular rectangulos escalados por `uiScale`.
- Ajustar breakpoints en `Layout.ForSize` para tener en cuenta DPI.
- Validar que paneles, testigos y cluster no se solapen a 150 %.

Archivos: `Layout.cs`, `DashboardRenderer.cs` (`Render`).  
Validacion: demo a 1920x1080 @150 %, 1280x720 @150 %, sin solapamientos ni recortes.  
Undo: revertir commit.

### P2 - Idioma es/en y extraccion de textos a recursos

**Estado actual:** textos hardcodeados en español; `CultureInfo` fijado a `es-ES`.  
**Objetivo:** soporte de inglés y extracción a `Resources.resx`.

Trabajo:
- Crear `Resources.resx` (es) y `Resources.en.resx`.
- Mover todas las cadenas literales de `DashboardRenderer.cs`, `MainForm.cs`, `SettingsForm.cs` a recursos.
- Selector de idioma en Ajustes (ya existe el campo `language` en el plan).
- Usar `CultureInfo` segun idioma para formato de numeros/fechas.
- Actualizar `GUIA-INTERFAZ.md` con la convencion de claves.

Archivos: nuevo `Resources.resx`, `Resources.en.resx`, `DashboardRenderer.cs`, `MainForm.cs`, `SettingsForm.cs`, `AppSettings`/preferencias.  
Validacion: app en es y en, sin cadenas sueltas, formato de numeros correcto.  
Undo: revertir commit.

### P3 - Consumo L/100 km o MPG

**Estado actual:** pendiente confirmar unidades de `Snap.Cons`.  
**Objetivo:** mostrar consumo medio en el panel de combustible.

Trabajo:
- Verificar en `TelemetryReader.cs` la conversion de `Cons` (m3/s de combustible) a L/100 km o MPG.
- Anadir campo de consumo al panel de combustible (`DrawFh6FuelCard`) con formato segun unidad configurada.
- Manejar "sin datos" cuando `Cons` es null o 0.
- Anadir toggle en Ajustes (ya previsto: L/100 km / MPG / GAL).

Archivos: `TelemetryReader.cs`, `Snap.cs` (si falta campo), `DashboardRenderer.cs`, `SettingsForm.cs`.  
Validacion: con juego real, consumo coherente; demo sin errores.  
Undo: revertir commit.

### P4 - Detalle fino panel por panel (opcional)

Trabajo por panel (ver seccion 10 de `GUIA-MODERNIZACION-v3.md`):
- Empresa: conductores/egresos en detalle expandible.
- Carga: bloques Carga/Viaje, tipo/trabajo/remolque en detalle.
- Daños: esquema de camion con zonas; imagen sin fondo.
- GPS: origen/odometro/recorrido/posicion en detalle secundario.
- Combustible: consumo medio, marcador de reserva.
- Avisos: icono y color por tipo; ticker en Compact.
- Clima: mover a cabecera en Compact.

Archivos: `DashboardRenderer.cs`.  
Validacion: cada panel revisado en demo a 1920x1080 y 1280x720.  
Undo: revertir commit por panel.

---

## Nuevas funcionalidades (no implementadas todavia)

### N2 - Consumo medio en combustible (depende de P3)

Ver P3.

### N3 - Reflow completo para 150 % (depende de P1)

Ver P1.

---

## Orden recomendado

1. P1 (reflow 150 %) - mayor impacto en usabilidad.
2. P3 (consumo) - dato nuevo util.
3. P4 (detalle fino) - por panel, incremental.
4. P2 (i18n) - trabajo grande, mejor al final.

Cada punto: implementar -> compilar -> probar demo -> probar juego real -> commit -> actualizar `ESTADO-IMPLEMENTACION.md`.

## Validacion por punto

1. `build.bat` compila sin errores.
2. `run-demo.bat` renderiza todos los paneles.
3. Prueba en ATS/ETS2 real (si aplica).
4. Medir CPU/RAM antes/despues.
5. Sin errores en log.

## Undo

- Cada punto es un commit separado; revertir con `git revert` o restaurar `src\` desde backup.
- No se modifican `src\bin`, `src\obj` ni `dist\ATS-Telemetry-Windows`.

## Referencias

- `ESTADO-IMPLEMENTACION.md` - estado actual y pendientes.
- `GUIA-MODERNIZACION-v3.md` - guia completa con codigo de ejemplo.
- `GUIA-INTERFAZ.md` - estructura de archivos y coordenadas.
- `README-FH6.md` - bitacora del rediseño FH6.