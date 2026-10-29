# Rediseño del panel — estilo Volvo FH6

Bitácora viva del cambio de interfaz del panel Windows. **Actualizar este archivo
después de cada decisión o bloque de trabajo**, para poder retomarlo más adelante
sin depender del historial del chat.

Última actualización: **28 de octubre de 2026 — v2.3 con el logo real del perfil del juego, sin miniatura de carga y con el nombre de la empresa a la izquierda**.

## Objetivo

Llevar el panel de `ATSTelemetry` a una estética y distribución inspiradas en la
pantalla digital del **Volvo FH6** de ETS2, conservando todos los datos y el
comportamiento actuales:

- Columna izquierda: **empresa** (finanzas) y **carga**.
- Columna derecha: **daños** y **GPS** (el GPS concentra también los datos del
  viaje: faltan, recorrido, llegada y odómetro).
- Zona central: velocímetro, RPM/marcha y testigos.
- **Combustible** deja de ser un panel y pasa a ser un indicador compacto,
  alineado visualmente con la tarjeta de clima.
- **Clima** vive en la tarjeta inferior derecha con condición, icono, hora del
  juego y DÍA/NOCHE.

No se cambia la lectura de telemetría, el clima automático, el saldo bancario ni
la lógica de pantalla completa/ventana.

## Referencia visual

Capturas del cluster FH6 usadas como referencia (Steam Workshop, mod *Volvo FH6
Improved Dashboard*, y el cluster digital de serie):

- Fondo negro, muy poco cromo, líneas finas y textos pequeños en mayúsculas.
- Velocidad enorme y centrada, con arco/ticks alrededor; marca del camión debajo.
- Marcha y RPM en la franja inferior central.
- Barras/porcentajes de combustible y AdBlue a los lados.
- Laterales con información de viaje, ETA y estado del vehículo, sin grandes
  marcos de colores.
- Acentos: blanco, verde menta, cian y ámbar; rojo solo para alertas.

## Distribución propuesta (lienzo 1920 × 1080)

```text
┌──────────────────────────────────────────────────────────────────────────┐
│                        marca / camión / motor          estado / pantalla  │  cabecera 24..92
├───────────────┬──────────────────────────────────────────┬───────────────┤
│ EMPRESA       │                                           │ DAÑOS         │
│ 400 × 380     │                                           │ 400 × 380     │
│               │  88×440       velocímetro central        │               │
│               │               arco 0..160 + número       │               │
│ CARGA         │               crucero / marcha / RPM     │ GPS           │
│ 400 × 424     │                                           │ 400 × 424     │
│               │                                    88×440│               │
│               ├──────┬────────────────────┬──────────┤               │
│               │ COMB.│     RESERVADO      │ CLIMA    │               │
└───────────────┴──────┴────────────────────┴──────────┴───────────────┘
   x=64..464     486..726   742..1180        1196..1433   x=1456..1856
```

- Cabecera: camión centrado, estado y nombre de pantalla a la derecha. La
  esquina izquierda queda libre: el clima ya no se duplica en la cabecera. Línea
  divisoria tenue en `y = 92`.
- Testigos: fila compacta centrada en la zona central, `y = 106..172`; el check
  engine va encuadrado entre las direccionales, con el icono y el texto
  `CHECK` / `ENGINE` en dos filas.
- Cluster central: centro en `(960, 548)`, radio ~302, arco de 224°.
  Número de velocidad centrado; crucero en chip vertical encima, marcha debajo
  de `km/h` y odómetro total compacto bajo la barra de RPM. El límite de
  velocidad vive a la derecha del panel GPS, con rumbo y destino centrados.
- Indicadores verticales: **agua** en `486, 300` y **aire** en `1346, 300`, ambos
  de `88 × 440`. Quedan entre los paneles laterales y el arco del cluster, sin
  título ni icono; la barra sube hasta `y + 338` y el pie centrado muestra
  cifra, unidad y subtítulo en 28/10/6.5 px.
- Encendido: con el motor apagado se apagan testigos, tacómetro/RPM, batería y
  aceite, agua/aire y combustible; los paneles laterales siguen visibles. Al
  arrancar, todos los testigos se encienden un momento y el velocímetro, el
  tacómetro y las barras barren hasta el máximo antes de volver a la telemetría.
- Indicadores inferiores: combustible (`240 × 142`), panel de avisos
  (`438 × 142`) y clima (`237 × 142`), todos a `y = 794..936`, con 16 px entre
  tarjetas y 22/23 px contra los paneles laterales. Combustible y clima se
  reducen un 25 % y el panel central crece un 50 % para conservar la separación.
  La tarjeta de combustible usa icono compacto, porcentaje protagonista y datos
  secundarios separados por un divisor fino; títulos y estados (`NIVEL`, `DÍA`)
  quedan en la cabecera. El panel central muestra los mensajes del juego en
  naranja: 2,5 s visible y 0,5 s de fundido; sin mensajes queda `SIN AVISOS`.
- Paneles laterales con fondo vidrio muy oscuro y borde al ~8 % de blanco, sin
  rellenos llamativos.

## Decisiones

1. **Se conserva el renderizador SkiaSharp y el lienzo virtual 1920×1080.** No se
   toca la lógica de escalado.
2. **El velocímetro deja de ser un dial de 270° con aguja.** Pasa a un arco
   central amplio con número grande, como el FH6. El tacómetro deja de ser un
   dial circular y se convierte en una barra segmentada de RPM con marcha.
3. **Los paneles existentes se reutilizan en lo posible** (`Money`, `Income`,
   `Expenses`, `DamageColor`, `DrawCompass`, `DrawSpeedLimitSign`,
   `DrawMiniGauge`, iconos). Se añaden variantes FH6 para laterales y cluster.
4. **Combustible**: tarjeta compacta con la misma estructura que la de clima
   (cabecera, icono grande, dato principal, detalle, barra y pie). Mantiene
   porcentaje, galones, autonomía y aviso de reserva.
5. **Clima**: sale de la cabecera y vive solo en la tarjeta inferior derecha,
   con icono, estado, hora del juego y DÍA/NOCHE. El menú de clima y la lectura
   automática no cambian.
6. **Daños** incorpora la fila **MOTOR** (`EngineDmg`), que ya se usa para el
   testigo de check engine.
7. **Empresa** añade **GANANCIA NETA** (ingreso del trabajo − egresos) para
   aprovechar la altura del panel; no sustituye ningún dato existente.
8. **El GPS concentra el viaje**: origen, destino, rumbo, faltan, recorrido,
   llegada, hora prevista, odómetro, progreso y posición. La antigua tarjeta de
   viaje pasa a ser una tarjeta de **clima** con condición, icono, hora y
   DÍA/NOCHE.
9. **La fila de testigos se mantiene completa**, incluidos retarder, freno de
   motor, limpiabrisas y bloqueo de diferencial; el check engine se integra
   encuadrado entre las direccionales.
10. **El flujo actual compila y prueba directamente sobre la app final y `dist`**;
    el respaldo del renderizador anterior se conserva fuera del proyecto.
11. **El crucero** pasa a un chip compacto encima del número de velocidad; la
    marcha se encuadra debajo de `km/h` y la barra de RPM se centra debajo del
    cluster sin cambiar su tamaño.
12. **Se elimina la señal de límite exterior** del cluster. El límite sigue
    visible dentro del panel GPS.
13. **Crucero vertical:** el chip baja a 95×48 (50 % más angosto y 50 % más
    alto) y apila icono, etiqueta y velocidad para conservar la legibilidad.
14. **Odómetro total:** sale de la franja de la marcha y pasa a un pill azul
    debajo de la barra de RPM.
15. **Tarjetas inferiores:** el bloque icono + datos de combustible y clima se
    centra; los títulos, `NIVEL` y `DÍA` no se mueven.
16. **Límite de velocidad:** la señal del GPS pasa a 88 px y usa relleno blanco,
    número negro y aro rojo.
17. **Odómetro v1.6:** el pill se reduce a 160×28, pierde la etiqueta
    `ODÓMETRO TOTAL` y muestra únicamente la cifra centrada.
18. **GPS v1.6:** la señal de límite se mueve al borde derecho; brújula a la
    izquierda y rumbo, destino y origen centrados en el panel.
19. **Check engine v1.6:** el testigo conserva su encuadre, pero la celda se
    ensancha para mostrar el icono y `CHECK` / `ENGINE` en dos filas.
20. **Combustible v1.6:** el icono baja de 62 a 32 px; el porcentaje queda como
    protagonista y los galones/autonomía pasan a una columna con divisor fino.
21. **Indicadores verticales v1.7:** agua y presión de aire salen de la fila
    inferior y pasan a dos paneles de `88 × 440` a los lados del cluster, con
    cifra, unidad, etiqueta y barra vertical de extremo a extremo.
22. **Panel reservado v1.7:** el hueco de los dos medidores circulares se cubre
    con un panel vacío de `292 × 142` para el siguiente widget.
23. **Odómetro v1.7:** el pill crece a `220 × 38` y la cifra sube a **26 px**
    para que el kilometraje total sea el dato dominante bajo la barra de RPM.
24. **Lectura vertical v1.8:** el número, la unidad y el subtítulo de agua/aire
    bajan a la esquina inferior derecha con **28/10/6.5 px**; la barra conserva
    su altura y la etiqueta sigue arriba a la izquierda.
25. **Fila inferior v1.8:** combustible y clima se reducen un 25 % (`240` y
    `237 px`) y el panel reservado crece un 50 % (`438 px`), con 16 px entre
    tarjetas y 22/23 px contra los paneles laterales.
26. **Iconos de título v1.8:** todos los paneles usan iconos de **22 px**,
    tomando el icono de clima como referencia.
27. **Pie de los verticales v1.9:** se retiran el título y el icono de agua/aire;
    la barra ocupa `y + 22 .. y + 338`, la línea separadora baja a `y + 362` y
    la cifra, la unidad y el subtítulo quedan centrados al pie.
28. **Testigos de batería y aceite v1.9:** dos iconos vectoriales de **32 px** a
    los lados del chip de crucero. Apagados en gris casi negro; encendidos en
    rojo con halo según `BatteryVoltage < BatteryWarn` y
    `OilPressure < OilWarn`.
29. **Validación v1.9:** demo a 1280×720 y 1920×1080, estado sin datos y avería
    forzada de batería/aceite para comprobar el encendido rojo.
30. **Apagado v2.0:** el tablero usa `snap.Active && snap.Engine`. Sin motor se
    apagan la fila de testigos, el número/barra de RPM, los testigos de
    batería/aceite, los paneles de agua/aire y el combustible. Los paneles
    laterales, el clima, la cabecera y el pie permanecen.
31. **Auto-test v2.0:** al arrancar, todos los testigos se encienden **1,25 s**;
    el tacómetro y la barra de RPM barren de 0 a `RpmMax` en **0,90 s**, aguantan
    **0,15 s** y vuelven al valor real en **1,05 s**. Agua, aire y combustible
    hacen el mismo barrido.
32. **Validación v2.0:** capturas de la secuencia a 1280×720, estado apagado
    forzado y estado final a 1920×1080.
33. **Crucero v2.1:** el chip de `95 × 48` usa relleno verde al **10 %** y
    encuadre al **42 %**, un tinte sutil que lo separa del fondo sin cambiar la
    paleta.
34. **Velocímetro v2.1:** el arco, el marcador y la cifra central también barren
    de `0` a **160 km/h** durante el auto-test y regresan al valor real con la
    misma curva `SmoothStep`.
35. **Avisos v2.1:** el panel central de `438 × 142` muestra mensajes del juego
    en naranja (`#FF851A`), centrados. Cada aviso dura **2,5 s** visible y
    **0,5 s** de fundido; después entra el siguiente. En demo se usa una lista
    de ejemplo y en vivo se reconstruyen desde entregas, multas, peajes, ferry,
    tren, nuevo trabajo e ingresos del saldo.
36. **Validación v2.1:** capturas del arranque (0→160→80 km/h), del fundido,
    del chip verde y del panel de avisos a 1280×720 y 1920×1080.
37. **Logo real de empresa v2.3:** el panel de finanzas usa el logotipo elegido
    en el perfil del juego (`logo` del `profile.sii`, por ejemplo `logo_4`). Los
    ocho PNG de `Assets\CompanyLogos` son las texturas reales de
    `material/ui/player_logo` extraídas de `base.scs`. El logo ocupa **62 × 62**
    a la derecha, con el mismo margen interior de **24 px** que el resto del
    panel, y `EMPRESA` + nombre conservan la alineación izquierda original.
38. **Saldo v2.2:** `SALDO DEL BANCO` sube a `y + 290` para dejar libre la cifra
    de `DINERO TOTAL`.
39. **Carga v2.3:** se elimina la miniatura vectorial. El nombre y el tipo vuelven
    a usar todo el ancho disponible del panel; no se reserva ningún hueco a la
    derecha.
40. **Validación v2.3:** demo a 1280×720 y 1920×1080, y telemetría real de ATS en
    ambos tamaños. El perfil **FLACO INC** muestra `logo_4` a la derecha, el
    nombre a la izquierda y el panel de carga sin miniatura, sin solapes ni
    recortes.

## Criterios de validación

- [x] Compila sin errores en Release.
- [x] Modo demo: todos los paneles y valores visibles, sin solapes ni recortes.
- [ ] Telemetría real de ETS2/ATS: velocidad, RPM, marcha, límite, crucero,
      testigos, clima, empresa, carga, daños y GPS correctos.
- [x] Logo real del perfil: `FLACO INC` + `logo_4` visibles a 1280×720 y
      1920×1080, con margen derecho de 24 px.
- [x] Carga sin miniatura: nombre y tipo usan todo el ancho en demo y en vivo.
- [ ] Combustible sigue avisando de reserva y muestra autonomía.
- [ ] Clima automático y manual siguen funcionando desde la bandeja.
- [x] Ningún texto se sale de su tarjeta al escalar a 1280×720 y 1920×1080.
- [x] Estado sin datos coherente: paneles laterales con placeholders y cluster
      apagado, sin valores falsos.
- [x] Encendido/apagado: los testigos, el velocímetro, el tacómetro y las
      barras se apagan con el motor y ejecutan el auto-test al arrancar.
- [x] Avisos: cada mensaje dura 2,5 s y se desvanece en 0,5 s, con relevo de
      cola y estado `SIN AVISOS` cuando no hay eventos.
- [ ] La app sigue recordando pantalla/modo y no cambia la lectura pasiva.

## Estado

| Fase | Descripción | Estado |
|---|---|---|
| 0 | Analizar render actual, datos disponibles y referencia FH6 | Hecho |
| 1 | Definir distribución, paleta y criterios | Hecho |
| 2 | Implementar cabecera, cluster y tarjetas FH6 | Hecho (v2.3) |
| 3 | Ajustar paneles laterales y textos | Hecho (v1.3) |
| 4 | Compilar y revisar en modo demo (capturas) | Hecho (v2.3, app final, 1280, 1920, arranque, avisos y logo real) |
| 5 | Probar con telemetría real | Parcial (ATS real: logo, empresa, carga, daños y GPS) |
| 6 | Actualizar documentación y `dist` | Hecho (README, GUIA, HISTORIAL, `dist` y ZIP al día) |

### v1 — 27/10/2026

Primera compilación correcta en Release. Captura de referencia:
`C:\Users\hs-se\AppData\Local\Temp\opencode\fh6-v1.png`.

Lo que ya funciona:

- Cabecera con clima/hora a la izquierda, camión centrado y estado/pantalla a la
  derecha.
- Fila de 12 testigos centrada.
- Cluster central con arco de velocidad, número grande, límite, crucero, check
  engine, RPM segmentado, marcha y odómetro.
- Paneles laterales: empresa/carga a la izquierda, daños/GPS a la derecha.
- Combustible como tarjeta-indicador; clima fuera de la fila inferior.
- Tarjeta de viaje y gauges de agua/PSI en la franja inferior.

Ajustes detectados en la revisión v1:

- [x] El nombre del camión duplica la marca cuando ya hay logotipo
      (`KENWORTH KENWORTH T800`). Resuelto en v1.1.
- [x] Probar una tipografía más fina para el número de velocidad
      (`Bahnschrift Light`), como el FH6. Resuelto en v1.1.
- [x] Subir el umbral de amarillo del RPM: a 67 % se ve amarillo y debería ser
      verde en conducción normal. Resuelto en v1.1.
- [x] Aprovechar el hueco inferior del GPS con una barra de progreso de ruta.
      Resuelto en v1.1.
- [x] Revisar el hueco inferior del panel de carga (posible barra/estado extra).
      Resuelto en v1.2 con DISTANCIA y ENTREGA.
- [x] Validar que ningún texto se recorte a 1280×720. Validado en v1.1 y v1.2.

### v1.1 — 27/10/2026

Segunda iteración, compilada y revisada a 1740×1010 y 1280×720:

- Corregido el nombre duplicado: ahora el logotipo muestra la marca y el texto
  solo el modelo (`KENWORTH` + `T800`).
- Número de velocidad con **Bahnschrift Light**, mucho más cercano al FH6.
- Umbrales de RPM recolocados: verde hasta 76 %, amarillo 76–84 %, naranja
  84–92 %, rojo desde 92 %. La barra vuelve a verde en conducción normal.
- GPS: añadida **PROGRESO DE RUTA** con porcentaje y barra.
- Validado a 1280×720: los textos siguen siendo legibles y no se recortan.
- Detectado que la flecha `→` de la ruta de carga no existe en Bahnschrift;
  se cambia esa línea a **Segoe UI**, que sí tiene el glifo.

Pendiente de la siguiente iteración:

- [x] Recompilar con el cambio de Segoe UI y confirmar la flecha.
- [x] Revisar el aspecto en 1920×1080 real (pantalla completa).
- [ ] Probar con telemetría real de ETS2.
- [x] Decidir si el hueco inferior del panel de carga necesita un dato extra.

### v1.2 — 28/10/2026

Tercera iteración, compilada y revisada a 1280×720 y 1920×1080. Capturas:
`C:\Users\hs-se\AppData\Local\Temp\opencode\fh6-v12c-1280.png`,
`C:\Users\hs-se\AppData\Local\Temp\opencode\fh6-v12c-1920.png` y estado sin
datos en `C:\Users\hs-se\AppData\Local\Temp\opencode\fh6-v12c-empty2.png`.

- **Cabecera:** clima/hora más legibles, separadores verticales, logotipo con
  más aire (18 px) y nombre de pantalla alineado a la derecha.
- **Testigos:** fila más compacta, iconos de 32 px, menos relleno y borde más
  sutil.
- **Cluster:** arco degradado cian→menta con halo fino y pista de 8 px; número
  de velocidad con **Bahnschrift Light**; marca duplicada eliminada bajo el
  velocímetro; barra de RPM de 14 px con marca de redline y marcha dentro de un
  squircle.
- **Empresa:** nueva fila **MARGEN OPERATIVO** con porcentaje y barra; nota
  **SALDO DEL BANCO** junto a DINERO TOTAL. Se corrigió el solape que había
  entre la nota de guardado y el margen.
- **Carga:** nuevas filas **DISTANCIA** y **ENTREGA**; el hueco inferior del
  panel queda resuelto con el pie actual.
- **Daños:** nuevas filas **SISTEMA MÁS AFECTADO** y **PROMEDIO GENERAL** con
  barra. Se eliminó la leyenda de umbrales que se pisaba con el promedio.
- **GPS:** el progreso incluye la distancia planificada (`65% · 350 km`);
  nueva **HORA PREVISTA** (hora del juego + ETA), bloque **POSICIÓN** con
  coordenadas y pie `GPS · DATOS DEL JUEGO`.
- **Combustible:** título `COMBUSTIBLE` a 10,5 px.
- **Agua/Aire:** etiqueta **AIRE**, iconos con el color del gauge y umbrales de
  temperatura normalizados.
- **Pie:** línea divisoria tenue sobre la franja inferior.
- **Estado sin datos:** los testigos se atenúan cuando no hay lectura y el
  promedio de daños muestra `--` en lugar de `0%`.
- **Correcciones tras captura:** margen y promedio ya no se solapan; la barra
  de RPM y la caja de marcha quedan equilibradas dentro del arco; la fila de
  testigos y la caja de marcha no tocan ningún borde.

Pendiente de la siguiente iteración:

- [ ] Probar con telemetría real de ATS/ETS2 y revisar velocidad, RPM, marcha,
      límite, crucero, testigos, clima, carga, daños, GPS y finanzas.
- [ ] Confirmar que el clima automático/manual y la reserva de combustible
      siguen funcionando desde la bandeja.
- [x] Revisar si el hueco inferior del GPS necesita un dato extra. Resuelto con
      HORA PREVISTA y POSICIÓN.
- [ ] Actualizar `README.md`, `GUIA-INTERFAZ.md` y `HISTORIAL.md`.
- [ ] Reconstruir `dist` solo tras la aprobación final.

### v1.3 — 28/10/2026

Cuarta iteración. El GPS absorbe los datos del viaje y la tarjeta inferior
derecha pasa a ser un widget de clima. Capturas:
`C:\Users\hs-se\AppData\Local\Temp\opencode\fh6-v13-final-1280.png`,
`fh6-v13-1920.png`, `fh6-v13-empty.png` y `fh6-v13-rain.png`.

- **GPS:** ahora reúne origen, destino, rumbo, **FALTAN**, **RECORRIDO**,
  **LLEGADA**, **HORA PREVISTA**, **ODÓMETRO**, progreso y posición. Se eliminó
  `RUTA RESTANTE` para no duplicar `FALTAN`.
- **Jerarquía del GPS:** `FALTAN` conserva el número grande en verde; recorrido y
  odómetro quedan alineados a la derecha; llegada y hora prevista comparten una
  fila; el progreso mantiene la barra cian y el porcentaje.
- **Tarjeta de clima:** sustituye a la tarjeta `VIAJE` en `1124, 794, 316×142`.
  Muestra icono, condición, hora del juego y **DÍA/NOCHE**, con el mismo lenguaje
  visual que la cabecera y el pie `CLIMA · DATOS DEL JUEGO`.
- **Estados:** sin señal muestra `SIN SEÑAL`, `-- / -- km`, `--:--` y
  `-- km`; con lluvia el icono y el acento cambian a cian.
- **Datos disponibles:** `Snap` expone `Weather` y `GameTime`, pero no
  temperatura ambiente ni humedad; el widget se limita a los datos reales del
  juego.
- **Validación:** revisado a 1280×720 y 1920×1080, en estado sin datos y con
  clima manual de lluvia. Sin solapes ni recortes.

Pendiente de la siguiente iteración:

- [ ] Probar el GPS y el widget de clima con telemetría real de ATS/ETS2.
- [ ] Confirmar el clima automático desde la bandeja después de esta iteración.
- [ ] Actualizar `README.md`, `GUIA-INTERFAZ.md` y `HISTORIAL.md`.
- [ ] Reconstruir `dist` solo tras la aprobación final.

### v1.4 — 28/10/2026

Quinta iteración. Cabecera limpia, cluster reordenado y tarjeta de combustible
concordante con la de clima. Capturas finales:
`C:\Users\hs-se\AppData\Local\Temp\opencode\fh6-v14-final-1280.png`,
`C:\Users\hs-se\AppData\Local\Temp\opencode\fh6-v14-final-1920.png` y estado sin
datos en `C:\Users\hs-se\AppData\Local\Temp\opencode\fh6-v14-empty.png`.

- **Cabecera:** se retiró el widget de clima/hora de la esquina superior
  izquierda y los separadores verticales; queda solo la línea inferior. El
  camión sigue centrado y el estado/pantalla a la derecha.
- **Combustible:** la tarjeta adopta la estructura de la de clima: cabecera con
  icono/etiqueta/estado, icono grande, dato principal, detalle, barra y pie
  `COMBUSTIBLE · DATOS DEL JUEGO`. Mantiene porcentaje, galones, autonomía y
  aviso de reserva.
- **Testigos:** la fila pasa a 13 iconos. El check engine entra como testigo
  encuadrado de 30×30 entre las direccionales; se activa con motor encendido y
  daño de motor ≥ 5 %.
- **Cluster:** se eliminó la señal de límite exterior (el límite sigue visible
  en el panel GPS). El crucero pasa a un chip centrado sobre el número de
  velocidad; la marcha va en una caja centrada bajo `km/h`; la barra de RPM se
  centra en `x = 795` sin cambiar su tamaño de 330×14.
- **Validación:** revisado a 1280×720 y 1920×1080, en demo y sin datos. Sin
  solapes ni recortes; sin señal el check engine se atenúa, la marcha muestra
  `--` y la barra de RPM queda vacía.

Pendiente de la siguiente iteración:

- [ ] Probar con telemetría real de ATS/ETS2, incluido el check engine.
- [ ] Confirmar clima automático/manual y reserva de combustible desde la bandeja.
- [ ] Actualizar `README.md`, `GUIA-INTERFAZ.md` y `HISTORIAL.md`.
- [ ] Reconstruir `dist` solo tras la aprobación final.

### v1.5 — 28/10/2026

Sexta iteración. Ajustes finos sobre la app final ya aprobada y recompilación
directa de la carpeta portátil. Capturas finales:
`C:\Users\hs-se\AppData\Local\Temp\opencode\fh6-v15-final-1280.png`,
`C:\Users\hs-se\AppData\Local\Temp\opencode\fh6-v15-final-1920.png` y estado sin
datos en `C:\Users\hs-se\AppData\Local\Temp\opencode\fh6-v15-empty.png`.

- **Crucero:** el chip pasa de 190×32 a **95×48** (50 % más angosto y 50 % más
  alto). Icono, etiqueta y velocidad se apilan en dos líneas.
- **Marcha:** el número sube de 34 a **42 px** dentro de una caja de 70×64.
- **Odómetro:** el kilometraje total sale de la franja de la marcha y pasa
  debajo de la barra de RPM en un pill de 220×30 con **encuadre azul** y
  etiqueta `ODÓMETRO TOTAL`.
- **Combustible y clima:** se centra el bloque icono + datos; los títulos,
  `NIVEL`, `DÍA`, la cabecera y los pies se mantienen.
- **Límite de velocidad:** la señal del GPS se centra en el panel, pasa de 44 a
  **88 px**, usa **fondo blanco, número negro y aro rojo**. La brújula queda a
  la izquierda y rumbo/destino/origen a la derecha para dejar espacio.
- **App final:** se recompiló `dist\ATS-Telemetry-Windows` con `build.ps1` y se
  regeneró `dist\ATS-Telemetry-Windows.zip` (71,4 MB) con la carpeta portátil
  completa. Las pruebas visuales se hicieron con ese mismo ejecutable en modo
  demo, sin una copia temporal aparte.
- **Validación:** 1280×720, 1920×1080 y estado sin datos. Sin solapes ni
  recortes; sin señal el odómetro muestra `-- km`, el límite desaparece y el
  check engine queda atenuado.

Pendiente de la siguiente iteración:

- [ ] Probar con telemetría real de ATS/ETS2 los nuevos tamaños y el check engine.
- [ ] Confirmar clima automático/manual y reserva de combustible desde la bandeja.
- [ ] Sincronizar `GUIA-INTERFAZ.md` y `HISTORIAL.md` con la distribución FH6.

### v1.6 — 28/10/2026

Séptima iteración. Ajustes de proporción y jerarquía sobre la app final, sin
cambiar la estructura de la v1.5. Capturas finales:
`C:\Users\hs-se\AppData\Local\Temp\opencode\fh6-v16-final-1280.png`,
`C:\Users\hs-se\AppData\Local\Temp\opencode\fh6-v16-final-1920.png` y estado sin
datos en `C:\Users\hs-se\AppData\Local\Temp\opencode\fh6-v16-empty.png`.

- **Odómetro total:** el pill azul baja de 220×30 a **160×28**. Se elimina la
  etiqueta `ODÓMETRO TOTAL` y queda solo la cifra centrada (`4.525 km`),
  manteniendo el encuadre azul y la atenuación sin señal (`-- km`).
- **GPS:** la señal de límite de 88 px se mueve al **borde derecho** del panel.
  La brújula se reduce a 104 px y queda a la izquierda; **rumbo, destino y
  origen se centran** en una columna de 156 px. Se conserva el fondo blanco,
  número negro y aro rojo.
- **Check engine:** el testigo mantiene el encuadre entre las direccionales,
  pero la celda pasa de 32 a **88 px** para colocar el icono a la izquierda y
  `CHECK` / `ENGINE` en **dos filas** a la derecha. El texto se atenúa sin
  señal y se ilumina con el testigo activo.
- **Combustible:** el icono grande baja de 62 a **32 px**. El porcentaje queda
  como protagonista, seguido de un divisor fino y una columna con galones y
  autonomía; la barra sube a `y + 108`. La composición queda centrada con
  márgenes equilibrados.
- **App final:** se recompiló `dist\ATS-Telemetry-Windows` con `build.ps1` y se
  regeneró `dist\ATS-Telemetry-Windows.zip` con la carpeta portátil completa.
- **Documentación:** `README.md`, `README-FH6.md`, `GUIA-INTERFAZ.md` y
  `HISTORIAL.md` quedaron sincronizados con la v1.6.
- **Validación:** 1280×720, 1920×1080 y estado sin datos. Sin solapes ni
  recortes; el check engine rotulado se lee a ambas resoluciones y las tarjetas
  mantienen su jerarquía.

Pendiente de la siguiente iteración:

- [ ] Probar con telemetría real de ATS/ETS2 el check engine rotulado y el GPS.
- [ ] Confirmar clima automático/manual y reserva de combustible desde la bandeja.

### v1.7 — 28/10/2026

Octava iteración. Se reordena la franja inferior y el cluster gana dos
indicadores verticales. Capturas finales:
`C:\Users\hs-se\AppData\Local\Temp\opencode\fh6-v17-1280.png`,
`C:\Users\hs-se\AppData\Local\Temp\opencode\fh6-v17-1920.png` y estado sin datos
en `C:\Users\hs-se\AppData\Local\Temp\opencode\fh6-v17-empty.png`.

- **Agua vertical:** sale de la fila inferior y pasa a un panel de **88 × 440**
  en `486, 300`, entre el panel de carga y el cluster. Muestra icono, `AGUA`,
  `TEMPERATURA`, cifra grande, `°C`, barra vertical de `40` a `120` y etiquetas
  de máximo/mínimo.
- **Aire vertical:** mismo formato en `1346, 300`, entre el cluster y el panel
  GPS, con `AIRE`, `PRESIÓN`, cifra, `PSI` y barra de `0` a `120`.
- **Panel reservado:** el hueco de los dos medidores circulares queda cubierto
  por un panel vacío de `292 × 142` en `816, 794`, listo para el siguiente
  widget. No lleva texto ni datos.
- **Odómetro total:** el pill azul pasa de 160×28 a **220 × 38** y la cifra de
  **13 a 26 px**; el texto se reduce automáticamente si el kilometraje no cabe.
- **App final:** recompilada `dist\ATS-Telemetry-Windows` con `build.ps1` y
  regenerado `dist\ATS-Telemetry-Windows.zip` (39 entradas, 71,4 MB).
- **Validación:** 1280×720, 1920×1080 y estado sin datos. Sin solapes con los
  paneles laterales ni con las etiquetas del arco; los indicadores verticales
  muestran `--` y barra vacía sin señal.

Pendiente de la siguiente iteración:

- [ ] Probar los indicadores verticales con telemetría real de ATS/ETS2.
- [ ] Confirmar clima automático/manual y reserva de combustible desde la bandeja.

### v1.8 — 28/10/2026

Novena iteración. Ajuste fino de los indicadores verticales, la fila inferior y
los iconos de título. Capturas finales:
`C:\Users\hs-se\AppData\Local\Temp\opencode\fh6-v18-1280.png`,
`C:\Users\hs-se\AppData\Local\Temp\opencode\fh6-v18-1920.png` y estado sin datos
en `C:\Users\hs-se\AppData\Local\Temp\opencode\fh6-v18-empty.png`.

- **Lectura inferior derecha:** en agua y aire, la cifra, la unidad y el
  subtítulo bajan a la esquina inferior derecha del panel. El número usa
  **28 px**, la unidad **10 px** y el subtítulo **6.5 px**; la barra vertical
  conserva 260 px de alto y la etiqueta sigue arriba a la izquierda.
- **Fila inferior:** combustible `240 × 142`, panel reservado `438 × 142` y
  clima `237 × 142`. Combustible y clima se reducen un 25 %; el panel reservado
  crece un 50 %. Quedan 16 px entre tarjetas y 22/23 px contra los paneles
  laterales.
- **Combustible compacto:** porcentaje a 34 px, `84/159 GAL`, `AUT. … KM`,
  divisor en `x + 150` y pie a 7 px.
- **Clima compacto:** icono grande de 54 px, condición a 19 px y hora a 28 px.
- **Iconos de título:** todos los paneles (empresa, carga, daños, GPS,
  combustible, clima, agua y aire) usan iconos de **22 px**, con el de clima
  como referencia.
- **App final:** recompilada `dist\ATS-Telemetry-Windows` y regenerado
  `dist\ATS-Telemetry-Windows.zip` (39 entradas, 71.426.093 bytes).
- **Validación:** 1280×720, 1920×1080 y estado sin datos. Sin solapes ni
  recortes; las tarjetas reducidas mantienen la jerarquía y la lectura pequeña
  se distingue del fondo.

Pendiente de la siguiente iteración:

- [ ] Probar con telemetría real de ATS/ETS2 los indicadores verticales, la
      reserva de combustible y el clima automático/manual.
- [ ] Confirmar que el panel reservado sigue vacío hasta el siguiente widget.

### v1.9 — 28/10/2026

Décima iteración. Se rehace el pie de los indicadores verticales y el cluster
gana los testigos de batería y presión de aceite. Capturas finales:
`C:\Users\hs-se\AppData\Local\Temp\opencode\fh6-v19-1280.png`,
`fh6-v19-1920.png`, estado sin datos en `fh6-v19-empty.png` y avería forzada en
`fh6-v19-warning.png`.

- **Verticales sin título:** agua y aire pierden la etiqueta y el icono de la
  cabecera del panel. La barra de 10 px sube a `y + 22 .. y + 338` (316 px de
  alto) y las etiquetas de máximo/mínimo quedan en sus extremos.
- **Pie centrado:** la línea separadora baja a `y + 362`; debajo se centran la
  cifra en **Bahnschrift 28 px** (`y + 368`), la unidad en `10 px` (`y + 404`) y
  el subtítulo en `6.5 px` (`y + 426`). Ya no hay lectura al borde derecho.
- **Testigos del crucero:** batería a la izquierda del chip y aceite a la
  derecha, ambos de **32 × 32** y centrados en `y = 334` (18 px de separación
  con el chip de 95 px). Apagados usan `#242424`; encendidos, rojo `#FF2938`
  con halo.
- **Lógica de aviso:** batería con `BatteryVoltage < BatteryWarn` (respaldo
  `23.76 V`) y aceite con `OilPressure < OilWarn` (respaldo `14 PSI`). Ambos
  requieren telemetría activa; en el demo con `26.8 V` y `60 PSI` quedan
  apagados.
- **Validación:** 1280×720, 1920×1080, estado sin datos y avería forzada
  (`22.4 V` y `8 PSI`) para confirmar el encendido rojo. Sin solapes con el
  arco, el chip ni la caja de marcha.
- **App final:** recompilada `dist\ATS-Telemetry-Windows`; ZIP regenerado con
  39 entradas, 71.425.810 bytes.

Pendiente de la siguiente iteración:

- [ ] Probar con telemetría real de ATS/ETS2 los testigos de batería/aceite, los
      indicadores verticales, la reserva de combustible y el clima
      automático/manual.
- [ ] Confirmar que los umbrales `OilWarn` y `BatteryWarn` del juego coinciden
      con los valores esperados en conducción normal.

### v2.0 — 28/10/2026

Undécima iteración. El panel se comporta como un dash real: se apaga con el
motor y ejecuta un auto-test al arrancar. Capturas:
`C:\Users\hs-se\AppData\Local\Temp\opencode\fh6-v20-startup-mid.png`,
`fh6-v20-startup-peak.png`, `fh6-v20-startup-settled.png`,
`fh6-v20-engine-off.png` y estado final en `fh6-v20-1920.png`.

- **Estado de encendido:** `Render` combina `snap.Active` con `snap.Engine`. Sin
  motor se apagan la fila de testigos, el número/barra de RPM, los testigos de
  batería/aceite, los paneles de agua/aire y el combustible. Empresa, carga,
  daños, GPS, clima, cabecera y pie siguen visibles.
- **Chequeo de testigos:** durante **1,25 s** se encienden todos los testigos a
  la vez, incluidos batería y aceite. Los iconos pulsantes quedan fijos durante
  el chequeo.
- **Barrido de arranque:** el tacómetro y la barra de RPM suben de 0 a `RpmMax`
  en **0,90 s**, aguantan **0,15 s** y vuelven al valor real en **1,05 s**. Las
  barras de agua, aire y combustible repiten el mismo recorrido. La
  interpolación usa `SmoothStep`.
- **Disparo:** la secuencia también se ejecuta al abrir la app si el camión ya
  está encendido.
- **Validación:** secuencia capturada a 1280×720 (testigos encendidos, pico de
  2.100 RPM y retorno), estado apagado forzado en demo y estado final a
  1920×1080. Sin solapes ni recortes.
- **App final:** recompilada `dist\ATS-Telemetry-Windows`; ZIP regenerado con
  39 entradas, 71.426.992 bytes.

Pendiente de la siguiente iteración:

- [ ] Probar con telemetría real el apagado/encendido del camión y confirmar que
      `snap.Engine` sigue el estado real del motor.
- [ ] Validar los testigos, el barrido y los umbrales de batería/aceite en
      conducción real.

### v2.1 — 28/10/2026

Duodécima iteración. Se pule el encendido y el panel central. Capturas:
`C:\Users\hs-se\AppData\Local\Temp\opencode\fh6-v21-startup-mid.png`,
`fh6-v21-startup-peak.png`, `fh6-v21-settled.png`, `fh6-v21-fade-mid.png`,
`fh6-v21-1920.png` y `fh6-v21-1920-demo.png`.

- **Crucero:** el chip gana un relleno verde al **10 %** y un borde al **42 %**.
  El cambio es deliberadamente sutil: se lee sobre el fondo sin competir con el
  arco ni con los testigos.
- **Velocímetro:** el arco de velocidad, el marcador y la cifra central se suman
  al barrido de arranque. Suben de `0` a **160 km/h** en **0,90 s**, aguantan
  **0,15 s** y vuelven al valor real en **1,05 s** con `SmoothStep`.
- **Avisos:** el panel reservado pasa a mostrar mensajes del juego en naranja,
  con cabecera `AVISOS` y estado `EN VIVO` / `DEMO` / `SIN SEÑAL`. Cada mensaje
  permanece **2,5 s** visible y se desvanece en **0,5 s**; después entra el
  siguiente de la cola. Sin mensajes se muestra `SIN AVISOS`.
- **Origen de los mensajes:** la API de telemetría no entrega el texto de las
  notificaciones del juego. En vivo se generan a partir de los cambios de
  `JobDeliveredRevenue`, `JobCancelledPenalty`, `FineAmount`, `TollgatePay`,
  `FerryPay`, `TrainPay`, `JobIncome` y los aumentos de `BankBalance`. En demo
  se ciclan cinco textos de ejemplo cada 3,8 s.
- **Validación:** arranque capturado a 1280×720 con el velocímetro en 160 y
  retorno a 80; fundido medido en frames consecutivos; estado final a
  1920×1080 con dos avisos distintos y el chip verde visible.
- **App final:** recompilada `dist\ATS-Telemetry-Windows`; ZIP regenerado con
  39 entradas, 71.429.682 bytes y SHA-256
  `D66F1C38F4A6D8AA0DBF91DCA540CCDDB14E3E92D70E9556E3E87BF4F7BD2E32`.

Pendiente de la siguiente iteración:

- [ ] Validar con telemetría real que los importes de entrega, multa, peaje,
      ferry y tren generan un único aviso coherente y sin duplicados.
- [ ] Confirmar que el saldo del savegame se refresca tras la entrega y que el
      aviso de ingreso aparece con el importe correcto.

### v2.2 — 28/10/2026

Decimotercera iteración. Se refinan los paneles de empresa y carga. Capturas:
`C:\Users\hs-se\AppData\Local\Temp\opencode\fh6-v22-layout.png`,
`fh6-v22-1920.png` y `fh6-v22-live-cargo.png`.

- **Emblema de empresa:** el panel de finanzas ya no usa el icono de dinero como
  cabecera. En su lugar hay un emblema de **62 × 62** con las iniciales de
  `CompanyName`, fondo en degradado, bisel interior y subrayado cian. `EMPRESA`
  y el nombre se desplazan a la derecha y el emblema ocupa el alto de ambos.
  La telemetría solo expone el nombre, no un archivo de logo; si se aporta un
  PNG se puede sustituir el emblema.
- **Saldo del banco:** el rótulo `SALDO DEL BANCO` sube a `y + 290`, alineado a
  la derecha y por encima de la cifra, para no cruzarse con el importe de
  `DINERO TOTAL`. La cifra conserva su tamaño y posición.
- **Miniatura de carga:** el nombre y el tipo reducen su ancho para reservar un
  hueco de **112 × 74** a la derecha. Dentro se dibuja una miniatura vectorial
  según `CargoId` / `CargoName`: bulldozer para maquinaria, troncos, tubos,
  coches, botellas para bebidas, cisterna para combustible y caja genérica para
  el resto. No depende de imágenes externas y se adapta al tamaño del panel.
- **Validación:** demo a 1280×720 y 1920×1080 con `Buldócer` y su bulldozer;
  telemetría real con `Limonada` y su botella; el emblema `FI` aparece con
  `FLACO INC` en ambos modos. Sin solapes en el bloque de saldo.
- **App final:** recompilada `dist\ATS-Telemetry-Windows`; ZIP regenerado con
  39 entradas, 71.431.522 bytes y SHA-256
  `CF0130D0C8E44A912DC5CF692B1A47745E87D372B5FA2B1F0C8103AF71B07436`.

Pendiente de la siguiente iteración:

- [ ] Validar en juego real los tipos de carga restantes (troncos, tubos,
      coches, combustible y caja genérica) y ajustar la clasificación si el
      nombre del juego no coincide.
- [ ] Si se dispone de un logo PNG de la empresa, integrarlo en el emblema.

### v2.3 — 28/10/2026

Decimocuarta iteración. Se corrige la cabecera de empresa y se simplifica el
panel de carga. Capturas:
`C:\Users\hs-se\AppData\Local\Temp\opencode\fh6-v23-final-demo-windowed.png`,
`fh6-v23-final-demo-1920.png` y `fh6-v23-final-live-1920.png`.

- **Logo real:** el emblema de iniciales se sustituye por el logotipo que el
  jugador eligió en el perfil. `BankBalanceReader` lee los campos `company_name`
  y `logo` del `profile.sii`; `TelemetryReader` los publica en `Snap` y
  `DashboardRenderer` carga `Assets\CompanyLogos\<logo>.png`. Los ocho PNG son
  las texturas reales de `material/ui/player_logo` extraídas de `base.scs` con
  el extractor oficial, no arte generado.
- **Composición:** el logo de **62 × 62** va a la derecha del panel, en
  `x + width - 24 - 62` e `y + 18`, respetando el margen interior de 24 px.
  `EMPRESA` y el nombre vuelven a `contentX`, a la izquierda. Si falta el PNG,
  el nombre ocupa todo el ancho y el hueco del logo queda vacío.
- **Carga:** se elimina la miniatura vectorial y todos sus métodos auxiliares.
  El nombre y `TIPO · ...` vuelven a usar `contentW` completo.
- **Parser del perfil:** `MatchProfileValue` acepta valores entrecomillados
  (`company_name: "FLACO INC"`) y sin comillas (`logo: logo_4`). El perfil real
  de ATS devuelve **FLACO INC** + **logo_4**.
- **Validación:** demo y ATS real a 1280×720 y 1920×1080. El logo aparece a la
  derecha con su margen, el nombre queda a la izquierda y el panel de carga no
  tiene miniatura ni solapes.
- **App final:** `dist\ATS-Telemetry-Windows` reconstruida y ZIP regenerado con
  los ocho PNG nuevos: 47 entradas, 71.489.903 bytes y SHA-256
  `15DAC73CFAB5C2CED9F8DD569952D650359E568C8380B9C42E6A12813942DF9E`.

Pendiente de la siguiente iteración:

- [ ] Probar otros perfiles con `logo_0`..`logo_7` y confirmar que un logo
      personalizado de mod, sin PNG en `Assets\CompanyLogos`, degrada al nombre.
- [ ] Completar la validación de telemetría real de ETS2.

## Archivos

```text
src/DashboardRenderer.cs   Dibujo del panel (único archivo a modificar)
src/TelemetryReader.cs     Datos y snapshot de demo (no tocar salvo necesidad)
src/BankBalanceReader.cs   Nombre y logo del perfil, saldo y conductores
Assets/CompanyLogos/       PNG reales logo_0..logo_7 (base.scs)
README-FH6.md              Este documento
README.md                  Documento general del proyecto
GUIA-INTERFAZ.md           Guía de coordenadas y métodos (sincronizada con v2.3)
```

Respaldo del renderizador anterior:
`C:\Users\hs-se\AppData\Local\Temp\opencode\DashboardRenderer.pre-fh6.cs`

## Cómo probar

Compilar directamente la app final y reemplazar la carpeta portátil:

```powershell
.\build.ps1
```

Ejecutar el binario final con datos de prueba:

```powershell
.\dist\ATS-Telemetry-Windows\ATSTelemetry.exe --demo --windowed
```

Para telemetría real, abrir ATS/ETS2 y ejecutar el mismo binario sin `--demo`.
No se usa una copia temporal aparte: lo que se prueba es lo que se entrega en
`dist\ATS-Telemetry-Windows`.

## Próximos pasos

1. Validar con telemetría real de ATS/ETS2.
2. Mantener `GUIA-INTERFAZ.md` y `HISTORIAL.md` sincronizados con cada iteración.
3. Mantener actualizado `dist\ATS-Telemetry-Windows.zip` cuando se publique.
