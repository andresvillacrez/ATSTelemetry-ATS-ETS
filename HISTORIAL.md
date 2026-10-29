# Historial de creación — ATS Telemetry

Este documento resume el proceso de creación y evolución de la aplicación, las
decisiones importantes y el estado en que quedó el proyecto. Está pensado para
leer el contexto sin tener que reconstruirlo a partir del código.

Última actualización: **28 de octubre de 2026**.

---

## 1. Punto de partida

El objetivo era tener un panel de telemetría para **American Truck Simulator**
(compatible también con ETS2) pensado para el monitor **PM1562** a
**1920×1080**, con fondo negro, sin bordes de ventana y con la información
necesaria para conducir sin apartar la vista de la carretera.

Desde el principio se establecieron dos restricciones:

- La telemetría se lee en modo **solo lectura**.
- No se modifica ni se sustituye el plugin de Ajazz
  (`com.hsgaming.scsmon`) ni `com.hsgaming.vitals`.

En macOS, la fuente de datos es el segmento de memoria POSIX
`/SCSTelemetry`, generado por el plugin de telemetría de SCS. En Windows el
equivalente es el memory-mapped file `Local\SCSTelemetry`.

---

## 2. Primera versión macOS

La app macOS se construyó como una aplicación nativa:

- **SwiftUI** para la composición visual del panel.
- **AppKit** para la ventana borderless, la pantalla PM1562 y el menú de
  estado.
- **Foundation / Darwin** para leer la memoria compartida y descifrar el
  savegame.
- **CommonCrypto** para el contenedor cifrado de los guardados.

La estructura principal quedó así:

```text
Sources/ATSTelemetry/
  main.swift              Ventana, pantalla y menú
  DashboardView.swift     Panel de 1920×1080
  TelemetryReader.swift   Offsets REV12 y lectura de /SCSTelemetry
  BankBalanceReader.swift Savegame, ScsC/BSII y saldo bancario
Resources/Indicators/     Iconos de testigos y diagnóstico
```

La app se dibuja a **12 fotogramas por segundo**, sin cuadrícula de maquetado,
y se coloca automáticamente en la pantalla llamada `PM1562`. Si no la
encuentra, usa la pantalla principal.

### Elementos del panel

- **Velocímetro** analógico de 0 a 160 km/h, con aguja, lectura digital, límite
  de velocidad y odómetro total.
- **Tacómetro** analógico con zona roja, lectura de RPM, indicador de marcha y
  check engine.
- **Fila de 12 testigos** por encima de los relojes, usando los iconos del
  paquete de Ajazz `Truck and Bus Driving Simulator Pack`.
- **Encuadre de viaje** de 220×92 px con distancia recorrida, planificada,
  restante y tiempo estimado.
- **Paneles de daños, carga y GPS** de 400×280 px en la columna derecha.
- **Indicadores redondos** de temperatura de agua y presión de aceite en PSI.
- **Panel financiero** de 360×200 px con empresa, trabajo actual, ingresos de
  conductores, egresos y dinero total.
- **Panel de combustible** de 200×200 px, con título y galones estadounidenses.
- **Panel de clima y hora** de 200×200 px.
- **Pie de página** con el estado de la lectura y el modo demo.

Los testigos inactivos se muestran en blanco al 40 %. Baliza, freno de
estacionamiento, bloqueo de diferencial y limpiabrisas pulsan suavemente
cuando están activos.

---

## 3. Decisiones de diseño y ajustes

A lo largo de las iteraciones se tomaron estas decisiones:

1. **Combustible en galones estadounidenses.** La telemetría entrega litros;
   el panel convierte con `1 L = 0,264172 gal`.
2. **Clima automático con respaldo manual.** REV12 no expone el estado del
   clima, así que la app lee la humedad de la carretera en la memoria del juego.
   El menú **Clima** permite volver a un estado fijo (Despejado, Nublado, Lluvia
   o Tormenta), que se guarda en `window.ini` y se usa si la lectura falla. Los
   limpiabrisas no alteran el estado.
3. **Check engine por desgaste.** En REV12, `O_ENG` significa motor encendido,
   no el testigo de check engine. El testigo se activa con desgaste del motor
   igual o superior al 5 %.
4. **Odómetro sin rótulo.** Se muestra en el mismo tipo de cuadro que la
   marcha, leyendo `O_ODOMETER = 1056`.
5. **Presión de aire en PSI.** El segundo indicador redondo muestra
   `airPressure`, no la presión de aceite. El número y el aro pasan de rojo
   (vacío) a cian (lleno) y el testigo es un manómetro verde junto a la lectura.
6. **Fila compacta bajo los relojes.** Agua, aire, finanzas, combustible y clima
   comparten una altura de 200 px. Finanzas se amplió a 360 px; la fila se
   reparte entre `x = 131` y `x = 1355` con 16 px de separación, manteniendo la
   proporción de los demás paneles y los odómetros.
7. **Ancho fijo del cuadro de marcha.** El indicador de marcha se dibuja en un
   cuadro de 210 px de ancho.
8. **Saldo bancario desde el savegame.** REV12 no expone `bank.money_account`.
   `BankBalanceReader` busca el `game.sii` más reciente, descifra el
   contenedor ScsC, descomprime el BSII y lee `money_account`.
9. **Iconos reutilizados.** Los PNG del paquete de Ajazz se copiaron a
   `Resources/Indicators` para que el panel no dependa de ese plugin en tiempo
   de ejecución.
10. **Títulos compactos.** Finanzas usa el nombre de la empresa como título;
    combustible muestra **COMBUSTIBLE** al mismo tamaño (16 px). Clima mantiene
    la hora y el estado sin un rótulo adicional.
11. **Retarder en la fila de testigos.** El puesto que ocupaba el testigo de
    aire ahora muestra el retarder (`truck_ui.retarderBrake`, offset 108), con
    el icono verde `retarder-c.png`.

El saldo se actualiza cuando ATS autoguarda o cuando el jugador hace un
guardado rápido. Mientras no haya un guardado nuevo, se conserva el último
saldo conocido.

---

## 4. Port para Windows

> Esta sección describe el port inicial. La app final vive en
> `Windows Native Final`; la distribución y la interfaz vigentes se detallan en
> la sección 14.

La app original usa AppKit, SwiftUI y memoria compartida POSIX, así que no se
puede compilar tal cual para Windows. Se optó por un **port independiente**, no
por una modificación de la app macOS.

### Enfoque elegido

- **C# / WinForms** sobre **.NET 8** (`net8.0-windows`).
- **SkiaSharp** para dibujar el panel completo en un lienzo virtual de
  1920×1080, con aceleración por hardware.
- El lienzo se escala y se centra en cualquier resolución, conservando la
  relación de aspecto.
- Distribución como **un solo ejecutable autocontenido** para `win-x64`.
- La carpeta `windows/` contiene **fuente + script de compilación**, no un
  binario precompilado.

### Estructura Windows

```text
windows/
  build.ps1                         Compilación y publicación win-x64
  build.bat                         Lanzador de build.ps1
  install-plugin.ps1                Instala el plugin en ATS/ETS2
  install-plugin.bat                Lanzador de install-plugin.ps1
  run.bat                           Ejecuta en PM1562
  run-demo.bat                      Ejecuta en modo demo
  Assets/Indicators/                Iconos compartidos con la app macOS
  Assets/Plugins/                   scs-telemetry.dll (RenCloud v1.12.x)
  README.md                         Guía de compilación y uso
  src/
    ATSTelemetry.Windows.csproj
    Program.cs
    MainForm.cs
    DashboardControl.cs
    TelemetryReader.cs
    BankBalanceReader.cs
    Snap.cs
    app.manifest
```

### Telemetría

`TelemetryReader.cs` usa los mismos offsets de REV12 que la app macOS y el
backend de Ajazz. Intenta abrir, en este orden:

1. `Local\SCSTelemetry`
2. `SCSTelemetry`
3. `Global\SCSTelemetry`

La lectura es pasiva: no escribe en el MMF y no envía teclas al juego.

### Saldo bancario en Windows

`BankBalanceReader.cs` busca el guardado más reciente en:

- `Documentos\American Truck Simulator\profiles`
- `Documentos\American Truck Simulator\steam_profiles`
- Steam Cloud: `<Steam>\userdata\<usuario>\270880\remote\profiles`
- `OneDrive\Documents\American Truck Simulator`
- `OneDrive\Documentos\American Truck Simulator`

La ruta de Steam se obtiene del Registro de Windows y de las ubicaciones
habituales de instalación. El contenedor ScsC se descifra con AES-256-CBC y el
flujo BSII se descomprime con `ZLibStream`, con un respaldo para DEFLATE sin
cabecera.

### Pantalla y controles

- La ventana es borderless, fullscreen, `TopMost` y sin presencia en la barra de
  tareas.
- Busca la pantalla `PM1562` mediante `EnumDisplayDevices`; si no la encuentra,
  usa la principal. También acepta `--screen=...`.
- El icono de la bandeja permite fijar y recordar el clima o salir.
- `Ctrl+Shift+Q` cierra la app.
- `--demo` activa datos de prueba y alterna día/noche; el clima se elige en el
  menú.

---

## 5. Verificación realizada

### Compilación Windows

Se instaló temporalmente el SDK **.NET 8.0.426** y se compiló el proyecto desde
macOS usando `EnableWindowsTargeting`. El resultado fue:

```text
Build succeeded.
0 Warning(s)
0 Error(s)
```

Se generó un `ATSTelemetry.exe` autocontenido de aproximadamente 154 MB,
formato `PE32+`, junto con `Assets/Indicators`. Esa compilación de prueba se
dejó en un directorio temporal; **no se copió a `windows/dist`** porque el
proyecto se entrega como fuente + script.

### Parser de saldo

Se probó `SiiBankParser` contra un `game.sii` real de ATS (perfil de Steam
Cloud). El resultado fue:

```text
Balance: 5341147
```

Ese valor coincide con el saldo esperado del guardado utilizado.

### Verificación en Windows real (23/10/2026)

En el equipo con Windows, pantalla PM1562 y ATS 1.61.3.18s:

- Se instaló **.NET SDK 8.0.426 x64** y se compiló con `windows\build.bat` sin
  errores ni advertencias.
- El modo demo se mostró correctamente en PM1562, escalado y centrado.
- Se corrigió la detección de pantalla: `EnumDisplayDevices` devolvía
  `Generic PnP Monitor`; ahora se lee el nombre EDID (`PM1562`) del registro y
  la ventana se reposiciona si cambian las pantallas.
- `SiiBankParser` leyó el guardado real de Steam Cloud:
  **5.353.754** (el valor anterior de referencia era 5.341.147).
- Se copió `scs-telemetry.dll` a `bin\win_x64\plugins` de ATS y ETS2. Tras
  reiniciar ATS, `Local\SCSTelemetry` respondió con `SDK=1`.
- El modo real mostró `PAUSA`, `DINERO TOTAL` y el resto de paneles conectados
  al plugin.

Queda por validar en un trayecto activo: velocidad, RPM, marcha, testigos,
ruta, combustible y actualización del saldo al autoguardar.

### Verificación en Windows real (26/10/2026)

- Se generó la release autocontenida `win-x64` en
  `windows\dist\ATS-Telemetry-Windows\ATSTelemetry.exe`.
- El modo demo se mostró correctamente en la pantalla PM1562.
- Se probó con **ETS2 1.61.1.9** en ejecución: `Local\SCSTelemetry` respondió
  con `SDK=1` y el panel mostró velocidad, RPM, marcha, daños, carga,
  combustible, clima, ruta y GPS en vivo.
- Se corrigió la codificación UTF-8 de los textos del panel (por ejemplo,
  `LÍMITE`, `DAÑOS`, `SEÑAL`) y el solapamiento entre el valor grande y su
  unidad (`80` / `km/h`).
- `BankBalanceReader` ahora distingue entre ATS y ETS2 y lee el savegame del
  juego activo. En ETS2 mostró el saldo correcto del perfil
  (**203.752.550**), no el de ATS.
- El plugin `scs-telemetry.dll` está instalado en
  `bin\win_x64\plugins` de ATS y ETS2.
- Se diseñó un icono propio (velocímetro cian con zona roja) y se incrustó en
  `ATSTelemetry.exe`; la ventana y la bandeja usan el mismo icono.
- `build.ps1` genera la carpeta portátil completa con los lanzadores
  `Iniciar ATS Telemetry.bat`, `Iniciar en modo demo.bat`,
  `Instalar plugin en ATS y ETS2.bat`, el icono y `LEEME.txt`. Se verificó que
  el lanzador de demo y el de telemetría en vivo abren el panel en PM1562.
- La misma carpeta se publica como `dist\ATS-Telemetry-Windows.zip` para
  copiarla a otra ubicación.
- Se abrió **ATS 1.61.3.18** (`GAME=2`, `SDK=1`) y el panel mostró el
  **Peterbilt 389**, 650 RPM, 67 % de combustible, 30 °C de agua, 57 PSI,
  daños 5 %, GPS y el saldo correcto de ATS (**4.222.132**), sin mostrar el
  saldo de ETS2.

Queda pendiente comprobar en movimiento la velocidad, la marcha, la ruta y la
actualización del saldo tras un guardado rápido.

---

## 6. Cómo compilar y ejecutar

### macOS (proyecto original)

```zsh
./build_app.sh
open "ATS Telemetry.app"
```

Modo demo:

```zsh
open "ATS Telemetry.app" --args --demo
```

### Windows (app final)

En un PC Windows con .NET 8 SDK x64, desde `Windows Native Final`:

```bat
build.bat
```

El ejecutable queda en:

```text
dist\ATS-Telemetry-Windows\ATSTelemetry.exe
```

Ejecutar:

```bat
run.bat
```

Modo demo:

```bat
run-demo.bat
```

También se puede abrir directamente el ejecutable final:

```bat
dist\ATS-Telemetry-Windows\ATSTelemetry.exe --demo --windowed
```

---

## 7. Estado actual (28/10/2026)

- La app macOS original se conserva sin cambios; el desarrollo activo es el
  port Windows de **`Windows Native Final`**.
- El proyecto compila en Release y se publica como ejecutable autocontenido
  `win-x64` mediante `build.ps1`.
- La interfaz activa es el rediseño **FH6 v2.3**, descrito en la sección 14.
- El panel se dibuja a **60 FPS** (16 ms) con SkiaSharp/OpenGL y se escala desde
  el lienzo virtual de 1920×1080.
- El plugin `scs-telemetry.dll` está incluido en la app y se instaló en ATS y
  ETS2; `Local\SCSTelemetry` respondió con `SDK=1`.
- El parser de saldo funciona con los savegames reales de ATS y ETS2, y elige
  el juego activo.
- El clima automático lee la humedad del juego (`WeatherReader`), con selección
  manual de respaldo en la bandeja. La telemetría y el clima se leen en modo
  pasivo.
- `dist\ATS-Telemetry-Windows` contiene la app final y
  `dist\ATS-Telemetry-Windows.zip` el paquete portátil completo.
- La v2.3 se validó en demo y con telemetría real de ATS a 1280×720 y
  1920×1080. El perfil **FLACO INC** muestra su logo real `logo_4` a la derecha
  del panel de empresa y el nombre a la izquierda; el panel de carga ya no tiene
  miniatura. También se comprobaron la secuencia de arranque, el fundido de los
  avisos y el chip de crucero. La prueba completa de ETS2, la reserva de
  combustible y los mensajes inferidos siguen pendientes.

### Siguientes pasos recomendados

1. Probar la v2.3 con telemetría real de ETS2: velocidad, RPM, marcha,
   testigos, check engine rotulado, GPS, clima, combustible, indicadores
   verticales de agua y aire, avisos de batería/aceite y transición de
   encendido/apagado. Validar también los avisos generados por entregas,
   multas, peajes y cambios de saldo.
2. Confirmar el clima automático/manual y el aviso de reserva desde la bandeja.
3. Verificar el cambio de **DINERO TOTAL** tras un guardado rápido.

---

## 8. Documentos relacionados

- [`README.md`](README.md): características, requisitos y uso de la app final.
- [`README-FH6.md`](README-FH6.md): bitácora del rediseño FH6, capturas y
  validación por iteración.
- [`GUIA-INTERFAZ.md`](GUIA-INTERFAZ.md): coordenadas, métodos y ciclo de
  trabajo para modificar el panel.
- `src/DashboardRenderer.cs`: implementación SkiaSharp de la interfaz FH6.
- `src/DashboardControl.cs`, `src/MainForm.cs` y `src/WindowPreferences.cs`:
  lienzo, ventana, pantalla y bandeja.
- `src/TelemetryReader.cs`, `src/WeatherReader.cs`,
  `src/BankBalanceReader.cs` y `src/Snap.cs`: lectura de datos.
- `Assets/Indicators`, `Assets/BrandLogos`, `Assets/CompanyLogos` y
  `Assets/Plugins`: recursos de la app portátil.

---

## 9. Pantalla libre y modo ventana (26/10/2026)

Se eliminó la dependencia de un monitor concreto (`PM1562`) para que la app
funcione en cualquier PC:

- Al iniciar usa `--screen=` si se indicó, luego la última pantalla guardada y,
  si no existe, la pantalla donde está el cursor.
- El menú **Pantalla** de la bandeja lista todos los monitores detectados.
- **Modo ventana** y `F11` alternan entre pantalla completa sin bordes y una
  ventana normal movible/redimensionable. Al cambiar de modo se recrea el
  control OpenGL para evitar cerrar la app.
- La pantalla y el modo se guardan en
  `%LOCALAPPDATA%\ATSTelemetry\window.ini`.
- El encabezado muestra el nombre real del monitor actual en lugar de un texto
  fijo.
- `run.bat` ya no fuerza `--screen=PM1562`.

Validado con `DISPLAY1` (1920×1080), `DISPLAY2` (PM1562, 1920×1080), modo
ventana, F11 en ambos sentidos y restauración de preferencias. La carpeta de
trabajo actual es `Windows Native Final` y contiene el paquete portátil
reconstruido en `dist\ATS-Telemetry-Windows`.

---

## 10. Retarder y medidor de aire (26/10/2026)

- El testigo de aire de la fila superior se sustituyó por el **retarder**. Se
  activa cuando `truck_ui.retarderBrake` (REV12, offset **108**) es mayor que
  cero y usa el icono verde `Assets\Indicators\retarder-c.png`.
- El medidor circular de PSI pasó de presión de aceite a **presión de aire**
  (`O_AIR = 992`). El aro usa `AirPressure / 120` como fracción.
- El número y el aro siguen una escala de color de rojo (vacío) a cian (lleno),
  pasando por naranja, amarillo y verde. A 0 PSI el aro queda vacío y la
  lectura es roja; a 108 PSI o más, cian.
- Junto a la lectura de PSI se dibuja un **manómetro verde** de 22 px, con el
  mismo tratamiento que el termómetro rojo del medidor de agua.
- Verificado en demo con 118 PSI (cian) y 0 PSI (aro vacío), y con telemetría
  real de ATS a 114 PSI.

---

## 11. Clima fijo (26/10/2026)

- Se eliminó la inferencia de lluvia por limpiabrisas. Activar los limpiabrisas
  ya no cambia el estado del panel a **LLUVIA**.
- El menú **Clima** de la bandeja quedó con cuatro estados fijos: Despejado,
  Nublado, Lluvia y Tormenta. Se eliminó **Automático** porque REV12 no publica
  el clima real del juego.
- La selección se guarda como `weather=Clear|Cloudy|Rain|Storm` en
  `%LOCALAPPDATA%\ATSTelemetry\window.ini` y se restaura al abrir la app.
- El panel compacto muestra la hora del juego, **DÍA/NOCHE**, el estado fijado
  y su icono. Al pasar de día a noche, los iconos cambian entre sol y luna.
- Verificado en demo con limpiabrisas activos y clima **Despejado**, y con
  **Lluvia** de día (07:49) y de noche (22:18). La preferencia `weather=Rain`
  se conservó al cerrar y volver a abrir.

---

## 12. Panel financiero ampliado (26/10/2026)

- El panel de finanzas pasó de **200×200** a **360×200 px** (80 % más ancho).
  La fila compacta se redistribuyó entre `x = 131` y `x = 1355`, con 16 px de
  separación; los demás paneles conservan 200×200 y la fila mantiene la
  alineación bajo los tacómetros y los odómetros.
- El título muestra el nombre de la empresa leído de `company_name` en el
  `profile.sii` del perfil activo. En ATS aparece **FLACO INC**.
- Las filas quedaron así: **TRABAJO ACTUAL**, **CONDUCTORES (n)**, **EGRESOS
  RUTA** y **DINERO TOTAL**. Los ingresos de conductores van en verde y suman
  `profit_log_entry.revenue` de los `driver_ai` contratados; el jugador no se
  incluye en la cifra.
- Los valores de las tres primeras filas subieron a 22 px y el dinero total a
  29 px.
- El panel de combustible añadió el título **COMBUSTIBLE**, al mismo tamaño que
  el nombre de la empresa (16 px), y desplazó el bloque de barra, porcentaje,
  galones y autonomía hacia abajo.
- Verificado con el ATS real: empresa **FLACO INC**, 33 conductores,
  **$4.384.955** de ingresos de conductores y **$4.241.020** de dinero total.
  El saldo coincide con `bank.money_account` del `game.sii` y con el guardado
  más reciente.

---

## 13. Clima en tiempo real (27/10/2026)

- Se añadió `WeatherReader`, que lee en modo solo lectura la humedad de la
  carretera que el juego mantiene en memoria. No usa el SDK ni el savegame, así
  que el panel cambia en vivo y no depende de los autoguardados.
- Offsets verificados: ATS 1.61.3.18, puntero estático `0x3051D40`; ETS2
  1.61.1.9, puntero `0x3046780`; humedad en `+0x5E8`.
- El lector valida `current_weather`, la humedad, el temporizador y seis
  centinelas `0x7FFFFFFF` antes de confiar en el valor. Si algo no coincide,
  devuelve `null` y el panel usa el clima manual.
- Clasificación provisional por humedad: `>=0,80` Tormenta, `>=0,15` Lluvia,
  `>=0,02` Nublado, resto Despejado.
- El menú **Clima** incluye **Automático (juego)** y los cuatro estados
  manuales. La preferencia se guarda como `weatherAuto=1|0`; el modo automático
  es el predeterminado.
- Verificado con ATS a `0,777715` de humedad y ETS2 a `0,401756`: ambos
  mostraron **LLUVIA** en el panel. El valor manual guardado era **Despejado**,
  lo que confirmó que el panel no estaba usando el respaldo.
- Probado con la app lanzada desde Explorer, sin elevación (integridad media),
  contra ATS y ETS2. La lectura de memoria y la telemetría funcionaron.
- Los umbrales de Nublado y Tormenta son provisionales: no se pudieron forzar
  transiciones con `g_set_weather` porque la consola del juego dejó de aceptar
  entrada sintética. Se mantienen los valores observados en partidas reales.

---

## 14. Rediseño FH6 (27-28/10/2026)

### Motivación y alcance

La interfaz original de Windows reproducía el panel de la app macOS. El
rediseño FH6 la sustituyó por una composición inspirada en el cluster digital
del **Volvo FH6**: fondo negro, marcos finos, squircles, tipografía técnica y
acentos blanco, cian, verde y ámbar.

Se conservaron sin cambios:

- La lectura pasiva de `Local\SCSTelemetry`.
- El clima automático con respaldo manual (`weatherAuto=1`).
- El saldo bancario, la empresa y su logo leídos del savegame y del perfil.
- El icono, la bandeja, el modo ventana/F11 y la memoria de pantalla.
- El empaquetado portátil en `dist\ATS-Telemetry-Windows`.

### Distribución final (v2.3)

- **Cabecera:** logotipo de marca, camión y cilindrada centrados; estado y
  monitor a la derecha; línea divisoria en `y = 92`.
- **Testigos:** fila centrada de 13 puestos con iconos de 32 px. El check engine
  ocupa una celda de 88 px con `CHECK` / `ENGINE` en dos filas. Con el motor
  apagado la fila queda vacía; al arrancar todos los testigos se encienden
  durante 1,25 s.
- **Cluster:** arco de 224° y máximo de 160 km/h; número central de 184 px;
  crucero en chip de 95×48 sobre la velocidad, con relleno verde al 10 % y
  encuadre al 42 %; marcha de 42 px; barra de RPM de 330×14 y odómetro total de
  **220×38** con cifra de **26 px** bajo la barra. A los lados del chip quedan
  los testigos de batería (izquierda) y aceite (derecha), de 32×32, negros
  apagados y rojos encendidos. El velocímetro, el tacómetro y la barra de RPM
  barren hasta el máximo y vuelven a la lectura real al arrancar.
- **Laterales:** empresa y carga a la izquierda; daños y GPS a la derecha. En
  empresa, `EMPRESA` y el nombre quedan a la izquierda y el logo real del perfil
  (`logo` de `profile.sii`) ocupa 62×62 a la derecha con margen interior de
  24 px; `SALDO DEL BANCO` se sube para no solaparse con la cifra. El panel de
  carga no tiene miniatura: nombre y tipo usan todo el ancho. El GPS concentra
  rumbo, origen, destino, distancias, llegada, hora prevista, progreso y
  posición. La señal de límite de 88 px queda en el borde derecho, con fondo
  blanco, número negro y aro rojo.
- **Indicadores verticales:** agua en `486, 300` y aire en `1346, 300`, ambos de
  `88×440`, entre los paneles laterales y el arco. Sin título ni icono; barra de
  `y + 22` a `y + 338`, separador en `y + 362` y pie centrado con cifra, unidad
  y subtítulo en 28/10/6.5 px, con escalas de `40–120 °C` y `0–120 PSI`. Al
  arrancar barren hasta el máximo y vuelven a la lectura.
- **Franja inferior:** combustible (240×142), panel de avisos (438×142) y clima
  (237×142), con 16 px entre tarjetas y 22/23 px contra los paneles laterales.
  Combustible y clima se reducen un 25 % y el panel central crece un 50 %. El
  combustible también hace el barrido de arranque. El panel central muestra
  mensajes del juego en naranja, con 2,5 s visible y 0,5 s de fundido; sin
  mensajes queda `SIN AVISOS`.
- **Apagado:** sin motor se apagan testigos, tacómetro/RPM, batería/aceite,
  agua/aire y combustible; empresa, carga, daños, GPS, clima, cabecera y pie
  siguen visibles.
- **Iconos de título:** todos los paneles usan iconos de **22 px**, tomando el
  de clima como referencia.
- **Pie:** estado de lectura y modo demo/datos en vivo.

### Iteraciones

| Versión | Fecha | Resumen |
|---|---|---|
| v1 | 27/10/2026 | Primera compilación FH6: cabecera, 12 testigos, cluster, paneles laterales y tarjetas. |
| v1.1 | 27/10/2026 | Se evita duplicar la marca, número de velocidad en Bahnschrift Light, umbrales de RPM, progreso de ruta y flecha en Segoe UI. |
| v1.2 | 28/10/2026 | Fila de testigos compacta, arco degradado, margen operativo, distancia/entrega, sistema más afectado/promedio, hora prevista/posición, pie y estados sin datos. |
| v1.3 | 28/10/2026 | El GPS absorbe los datos del viaje; la tarjeta de clima sustituye a la de viaje en `1124, 794, 316×142`. |
| v1.4 | 28/10/2026 | Cabecera limpia, tarjeta de combustible concordante con clima, 13 testigos con check engine, retirada de la señal de límite exterior y cluster reordenado. |
| v1.5 | 28/10/2026 | Crucero de 95×48, marcha de 42 px, odómetro azul de 220×30, tarjetas centradas y límite de 88 px centrado con fondo blanco. |
| v1.6 | 28/10/2026 | Odómetro de 160×28 sin etiqueta, límite a la derecha con rumbo/destino centrados, check engine rotulado y tarjeta de combustible refinada. |
| v1.7 | 28/10/2026 | Agua y aire pasan a indicadores verticales de 88×440 junto al cluster; panel reservado de 292×142 en la franja inferior; odómetro total de 220×38 con cifra de 26 px. |
| v1.8 | 28/10/2026 | Lectura de agua/aire abajo a la derecha en 28/10/6.5 px; combustible y clima al 75 % (240×142 y 237×142), panel reservado al 150 % (438×142); iconos de título homogéneos a 22 px. |
| v1.9 | 28/10/2026 | Agua/aire sin título ni icono, barra de 316 px y pie centrado; testigos vectoriales de batería y aceite de 32 px a los lados del crucero, negros apagados y rojos encendidos. |
| v2.0 | 28/10/2026 | Apagado por motor: testigos, tacómetro/RPM, agua/aire y combustible a oscuras. Al arrancar, chequeo de testigos de 1,25 s y barrido de tacómetro/RPM, agua/aire y combustible hasta el máximo con retorno suave a la telemetría. |
| v2.1 | 28/10/2026 | Chip de crucero con tinte verde sutil; el velocímetro se suma al barrido de arranque (0→160→real); el panel central muestra avisos del juego en naranja, 2,5 s visible y 0,5 s de fundido. |
| v2.2 | 28/10/2026 | Emblema de 62×62 con las iniciales de la empresa; `SALDO DEL BANCO` reubicado sobre la cifra; miniatura vectorial de 112×74 en carga según el tipo de mercancía. |
| v2.3 | 28/10/2026 | Logo real del perfil (`logo_4` para FLACO INC) a la derecha con margen de 24 px; nombre a la izquierda; se elimina la miniatura de carga y sus métodos. |

### Estado final v2.3

- El flujo de trabajo compila y prueba **directamente la app final**:
  `build.ps1` reconstruye `dist\ATS-Telemetry-Windows` y las pruebas se hacen
  con ese mismo ejecutable, sin una copia demo separada.
- `dist\ATS-Telemetry-Windows.zip` se regeneró con la carpeta portátil completa
  y los ocho PNG de `Assets\CompanyLogos`: 47 entradas, 71.489.903 bytes,
  SHA-256 `15DAC73CFAB5C2CED9F8DD569952D650359E568C8380B9C42E6A12813942DF9E`.
  El EXE final tiene SHA-256
  `A0C8D9E781A1E1C20BEB1E8712A781103740042F5B3B604133BE3A3648664D91`.
- Se validó en modo demo a **1280×720** y **1920×1080**, y con telemetría real de
  ATS en ambos tamaños. `FLACO INC` muestra su logo real `logo_4` a la derecha,
  con margen de 24 px, y el nombre a la izquierda; el panel de carga no tiene
  miniatura. No hay solapes ni recortes; los testigos, el velocímetro, el
  tacómetro/RPM, las barras de agua/aire y el combustible se apagan con el motor
  y ejecutan el auto-test al arrancar.
- Capturas de referencia:
  `C:\Users\hs-se\AppData\Local\Temp\opencode\fh6-v23-final-demo-windowed.png`,
  `fh6-v23-final-demo-1920.png` y `fh6-v23-final-live-1920.png`.
- La documentación quedó sincronizada: `README.md`, `README-FH6.md`,
  `GUIA-INTERFAZ.md` y este historial.

### Pendiente

- Probar la v2.3 con telemetría real de ETS2: velocidad, RPM, marcha,
  testigos, check engine rotulado, GPS, clima, combustible, indicadores
  verticales de agua y aire, avisos de batería/aceite y transición de
  encendido/apagado.
- Validar que las entregas, multas, peajes y cambios de saldo generan un aviso
  único y con el importe correcto.
- Probar otros perfiles con `logo_0`..`logo_7` y confirmar que un logo
  personalizado de mod, sin PNG en `Assets\CompanyLogos`, degrada al nombre.
- Confirmar el clima automático/manual y el aviso de reserva desde la bandeja.
- Verificar la actualización de **DINERO TOTAL** tras un guardado rápido.
