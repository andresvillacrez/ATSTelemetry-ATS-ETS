# Guía rápida para modificar la interfaz FH6

Esta carpeta es la **app final** de **ATS Telemetry para Windows**. La interfaz
actual es el rediseño **FH6 v2.3**, inspirado en el panel digital del Volvo FH6.
La bitácora de decisiones y capturas está en
[README-FH6.md](README-FH6.md).

## Archivos importantes

```text
Windows Native Final\
  src\
    DashboardRenderer.cs     Todo el dibujo del panel (empieza aquí)
    DashboardControl.cs      Solo aloja el lienzo SkiaSharp
    MainForm.cs              Pantalla completa, modo ventana y bandeja
    WindowPreferences.cs     Memoria de pantalla y modo de ventana
    SkiaCompat.cs            Capa que imita System.Drawing sobre SkiaSharp
    TelemetryReader.cs       Lee Local\SCSTelemetry y genera el snapshot
    WeatherReader.cs         Lee la humedad del clima en memoria del juego
    Snap.cs                  Campos de una muestra de telemetría
    BankBalanceReader.cs     Savegame/perfil: saldo, empresa, logo y conductores
  Assets\Indicators\         Iconos de testigos (PNG)
  Assets\BrandLogos\         Logotipos de marca de los camiones
  Assets\CompanyLogos\       Logos reales del perfil (logo_0..logo_7)
  Assets\Plugins\            scs-telemetry.dll incluido
  build.ps1 / build.bat      Compila y publica la carpeta portátil
  run.bat                    Abre la app final con telemetría real
  run-demo.bat               Abre la app final con datos de prueba
  dist\ATS-Telemetry-Windows App final compilada
  README.md                  Documento general del proyecto
  README-FH6.md              Bitácora del rediseño FH6
  GUIA-INTERFAZ.md           Este documento
```

No edites `src\bin`, `src\obj` ni los archivos generados dentro de `dist`:
son resultados de compilación.

## Lienzo y coordenadas

El panel se dibuja siempre en un lienzo virtual de **1920 × 1080** y después se
escala y se centra en el monitor conservando la relación de aspecto. Todas las
posiciones y tamaños de esta guía son píxeles de ese lienzo, no del monitor
real.

El reparto visual está en `DashboardRenderer.DrawDashboard`:

| Zona | Posición |
|---|---|
| Cabecera | `y = 15..70`; línea inferior en `y = 92` |
| Fila de testigos | centrada, `y = 108..166` |
| Cluster central | centro `(960, 548)`, radio `302` |
| Agua (vertical) | `x = 486`, `y = 300`, `88 × 440` |
| Aire (vertical) | `x = 1346`, `y = 300`, `88 × 440` |
| Empresa | `x = 64`, `y = 116`, `400 × 380` |
| Carga | `x = 64`, `y = 512`, `400 × 424` |
| Daños | `x = 1456`, `y = 116`, `400 × 380` |
| GPS | `x = 1456`, `y = 512`, `400 × 424` |
| Combustible | `x = 486`, `y = 794`, `240 × 142` |
| Avisos | `x = 742`, `y = 794`, `438 × 142` |
| Clima | `x = 1196`, `y = 794`, `237 × 142` |
| Pie | línea en `y = 984`, texto en `y = 1008` |

### Cabecera

`DrawFh6Header` centra el bloque de marca: logotipo de `Assets\BrandLogos` a
32 px de alto, nombre/modelo del camión a 28 px y cilindrada a 13 px. El
logotipo ya muestra la marca, así que el texto evita repetirla. A la derecha
quedan el estado de la app (con punto de color) y el nombre del monitor.

### Fila de testigos

`DrawFh6IndicatorRow` dibuja **13 puestos** en una fila de 32 px de alto con
18 px de separación:

- Baliza, marcadores laterales, luz baja, luz alta, freno, direccional
  izquierda, **check engine**, direccional derecha, freno de estacionamiento,
  retarder, freno de motor, limpiabrisas y bloqueo de diferencial.
- El check engine ocupa una celda especial de **88 px** con el icono a la
  izquierda y `CHECK` / `ENGINE` en dos filas a la derecha. Se activa con
  `snap.Active && snap.Engine && EngineDmg >= 5`.
- Los iconos inactivos se atenúan; baliza, freno de estacionamiento,
  limpiabrisas y diferencial pueden pulsar.

### Cluster central

`DrawFh6SpeedCluster` es el bloque principal:

- Arco de velocidad de **224°**, de 158° a 382°, con máximo de **160 km/h**.
  El número central usa **Bahnschrift Light 184 px**; por encima del límite se
  pinta en naranja.
- Chip de crucero de **95 × 48** en `top = 310`, con icono, `CRUCERO` y la
  velocidad fijada en verde. El relleno usa verde al **10 %** y el encuadre al
  **42 %** para que el chip se despegue del fondo sin romper la estética.
- Testigos de **batería** y **presión de aceite** de **32 × 32** a los lados del
  chip, centrados en `y = 334`: batería en `cx - 97.5` y aceite en `cx + 65.5`.
  Apagados usan gris casi negro (`#242424`); encendidos, rojo `#FF2938` con
  halo. La batería se enciende con `BatteryVoltage < BatteryWarn` (respaldo
  `23.76 V`) y el aceite con `OilPressure < OilWarn` (respaldo `14 PSI`).
- Caja de marcha de **70 × 64** en `y = 592`, con el número a **42 px**.
- Barra de RPM de **330 × 14** en `(795, 710)`, con marca de redline al 92 %.
- Odómetro total en un pill azul de **220 × 38** en `(cx - 110, 736)`. No lleva
  etiqueta: solo la cifra centrada (`4.525 km`) en **Consolas 26 px**, que se
  reduce automáticamente si el valor no cabe.

### Encendido y apagado

`Render` combina `snap.Active` con `snap.Engine` para saber si el tablero está
energizado. `UpdateIgnitionElapsed` detecta el cambio de estado y marca el
instante de arranque:

- **Motor apagado:** la fila de testigos queda vacía, desaparecen el número de
  RPM, la barra de RPM y la marca de redline, se apagan los testigos de
  batería/aceite y los paneles de agua, aire y combustible quedan solo con su
  marco. Empresa, carga, daños, GPS, clima, cabecera y pie siguen visibles. El
  panel de avisos permanece visible (con `SIN AVISOS` si no hay mensajes) y el
  chip de crucero desaparece.
- **Arranque:** durante **1,25 s** se encienden todos los testigos a la vez
  (chequeo de bombillas), incluidos batería y aceite. El velocímetro barre de
  `0` a **160 km/h**, y el tacómetro y la barra de RPM de `0` a `RpmMax`, en
  **0,90 s**; aguantan **0,15 s** y vuelven suavemente al valor real en
  **1,05 s**. Las barras de agua, aire y combustible hacen el mismo barrido y
  regresan a su lectura.
- La interpolación usa `SmoothStep` (`AnimatedFraction`) y también se ejecuta al
  abrir la app si el camión ya está encendido.

### Paneles laterales

- **Empresa** (`DrawFh6FinancePanel`): `EMPRESA` y el nombre vuelven a
  `contentX`, a la izquierda. El logotipo real del perfil (`logo` de
  `profile.sii`) se carga con `CompanyLogoImage` desde
  `Assets\CompanyLogos\<logo>.png` y ocupa **62 × 62** a la derecha, en
  `x + width - 24 - 62` e `y + 18`, respetando el margen interior de 24 px. Si
  no hay PNG, no se reserva hueco y el nombre usa todo el ancho. Debajo quedan
  `TRABAJO ACTUAL`, `CONDUCTORES (n)`, `EGRESOS RUTA`, `GANANCIA NETA`,
  `DINERO TOTAL`, `SALDO DEL BANCO` y `MARGEN OPERATIVO` con barra. `SALDO DEL
  BANCO` va en `y + 290`, por encima de la cifra, para no solaparse con el
  importe.
- **Carga** (`DrawFh6CargoPanel`): nombre, tipo, peso, integridad, trabajo
  estándar/especial, remolque, ruta, distancia y entrega. No hay miniatura: el
  nombre y el tipo usan todo el ancho (`contentW`) del panel.
- **Daños** (`DrawFh6DamagePanel`): camión, tráiler, carga y motor, más
  `SISTEMA MÁS AFECTADO` y `PROMEDIO GENERAL`.
- **GPS** (`DrawFh6GpsPanel`): la señal de límite de **88 px** va al borde
  derecho, a 16 px del marco. La brújula de **104 px** queda a la izquierda. La
  columna central, de 156 px, centra `RUMBO`, `DESTINO` y `ORIGEN`. Debajo
  quedan `FALTAN`, `RECORRIDO`, `ODÓMETRO`, `LLEGADA`, `HORA PREVISTA`,
  `PROGRESO DE RUTA` y `POSICIÓN`.

La señal de límite conserva el lenguaje de la v1.5: relleno blanco, número
negro y aro rojo.

### Indicadores verticales

`DrawFh6VerticalGauges` coloca dos paneles estrechos a los lados del cluster,
entre los paneles laterales y el arco de velocidad:

- **Agua** (`DrawFh6VerticalGauge` en `486, 300`, `88 × 440`): barra vertical de
  `40` a `120`, sin título ni icono. Al pie quedan `92`, `°C` y `TEMPERATURA`,
  centrados. La fracción es `(temperatura - 40) / 80` y el color recorre azul,
  cian, verde, amarillo, naranja y rojo.
- **Aire** (`DrawFh6VerticalGauge` en `1346, 300`, `88 × 440`): mismo esquema,
  sin título ni icono, con cifra, `PSI`, `PRESIÓN` y barra de `0` a `120`. La
  fracción es `AirPressure / 120`, de rojo (vacío) a cian (lleno).

La barra mide 10 px de ancho y 316 px de alto, entre `y + 22` y `y + 338`, con
marcas cada 25 % y etiquetas numéricas en los extremos. Una línea fina en
`y + 362` separa la escala del pie. La lectura se centra debajo: número
**Bahnschrift 28 px** en `y + 368`, unidad de `10 px` en `y + 404` y subtítulo
de `6.5 px` en `y + 426`. Sin señal, el número muestra `--`, la barra queda
vacía y todo se atenúa.

### Tarjetas inferiores

- **Combustible** (`DrawFh6FuelCard` en `486, 794`, `240 × 142`): cabecera con
  icono de 22 px, `COMBUSTIBLE` y `NIVEL` / `RESERVA` / `SIN SEÑAL`. En el
  cuerpo, icono de **32 px**, porcentaje a **34 px**, divisor en `x + 150` y
  columna con `84/159 GAL` y `AUT. 738 KM`. La barra va en `y + 108` y el pie es
  `COMBUSTIBLE · DATOS DEL JUEGO`.
- **Avisos** (`DrawFh6ReservedPanel` en `742, 794`, `438 × 142`): cabecera con
  icono de 22 px en naranja, `AVISOS` y estado `EN VIVO` / `DEMO` / `SIN SEÑAL`.
  El mensaje se centra en naranja (`#FF851A`) a 24 px (mínimo 13 px) y lleva el
  pie `NOTIFICACIÓN DEL JUEGO`. Cada aviso permanece **2,5 s** visible y se
  desvanece en **0,5 s**; después entra el siguiente de la cola. Sin avisos se
  muestra `SIN AVISOS` atenuado.
  La API de telemetría no expone el texto literal de las notificaciones del
  juego: los avisos se reconstruyen con los importes que cambian (entregas,
  multas, peajes, ferry, tren, nuevo trabajo e ingresos del saldo del savegame).
  En modo demo se usa una lista de mensajes de ejemplo.
- **Clima** (`DrawFh6WeatherCard` en `1196, 794`, `237 × 142`): condición a
  19 px, icono grande de 54 px, hora del juego a 28 px y `DÍA` / `NOCHE`. El
  icono cambia entre sol y luna.

### Iconos de título

Todos los paneles usan **iconos de título de 22 px**, tomando el icono de clima
como referencia: empresa, carga, daños, GPS, combustible y clima. Los
indicadores verticales ya no llevan icono ni etiqueta de título. La etiqueta se
alinea con el centro vertical del icono y el texto se separa unos 4 px más
cuando el icono crece.

## Métodos de dibujo

Los métodos activos del rediseño son:

- `DrawFh6Backdrop`, `DrawFh6Header`, `DrawFh6IndicatorRow`,
  `DrawFh6SpeedCluster`, `DrawFh6SpeedArc`, `DrawFh6CruiseChip`,
  `DrawFh6WarningLamp`, `DrawBatteryWarningIcon`, `DrawOilWarningIcon`,
  `DrawFh6RpmBar`, `DrawFh6VerticalGauges`, `DrawFh6VerticalGauge`,
  `DrawFh6FinancePanel`, `DrawFh6CargoPanel`, `DrawFh6DamagePanel`,
  `DrawFh6GpsPanel`, `DrawFh6FuelCard`, `DrawFh6ReservedPanel`,
  `DrawFh6WeatherCard`, `DrawFh6Footer`.
- Secuencia de encendido: `UpdateIgnitionElapsed`, `AnimatedFraction` y
  `SmoothStep`.
- Avisos del juego: `UpdateGameMessages`, `PushMessage`, `StoreMessageBaseline`,
  `DetectGameMessages`, `ChangedPositive` e `Increased`.
- Identidad: `DrawFh6CompanyLogo` y `CompanyLogoImage`.
- Piezas reutilizables: `DrawGlassPanel`, `DrawHairline`, `DrawSquircle`,
  `DrawPanelFrame`, `DrawProgressBar`, `DrawSpeedLimitSign`, `DrawCompass`,
  `DrawFitted`, `DrawText`, `DrawTracked`, `DrawFuelIcon`,
  `DrawMiniGauge`, `DrawCompactWeatherIcon`.

El archivo conserva métodos del panel anterior (`DrawHeader`, `DrawFooter`,
`DrawIndicatorRow`, `DrawSpeedometer`, `DrawTachometer`, `DrawFinancePanel`,
`DrawFuelPanel`, `DrawWeatherPanel`, `DrawDamagePanel`, `DrawCargoPanel`,
`DrawGpsPanel`, etc.). **No se usan desde `DrawDashboard`**; se mantienen como
referencia. Para la interfaz actual edita los métodos `DrawFh6*`.

## Colores y tipografías

La paleta está al principio de `DashboardRenderer.cs`:

```csharp
Green     #38F594
Mint      #78FFD4
Cyan      #40D1FF
Yellow    #FFD638
Orange    #FF851A
Red       #FF2938
Blue      #6BADFF
MoonLight #CCE0FF
```

`White(alpha)` genera blanco con transparencia y `WithAlpha(color, alpha)`
aplica transparencia a un color existente.

Tipografías:

- `Rounded(...)` → **Bahnschrift** (títulos, números y etiquetas).
- `Ui(...)` → **Segoe UI** (texto de interfaz y rutas).
- `Mono(...)` → **Consolas** (datos técnicos y unidades).

`DrawFitted` reduce el tamaño automáticamente si el texto no cabe en su
`RectangleF`; `DrawTracked` dibuja texto con espaciado entre letras. Los tamaños
están en píxeles del lienzo virtual.

## Iconos

Los testigos se cargan por nombre desde `Assets\Indicators` mediante
`DashboardRenderer.Icon(...)`. Para sustituir uno, reemplaza el PNG por otro
con el mismo nombre.

Nombres usados por la fila actual:

```text
beacon-c                 side-markers-c
low-beam-c               high-beam-c
brake-c1                 turn-lights-left-c
check-engine-line        turn-lights-right-c
parking-brake-2-c        retarder-c
engine-brake-round       wiper-c
diff-lock-c
```

Otros iconos del panel: `cruise-control-c` para el crucero y
`advisor-diagnostics` para la cabecera de daños. Los logotipos de marca están
en `Assets\BrandLogos` (`kenworth.png`, `peterbilt.png`, `volvo.png`, etc.).
Los logos de empresa están en `Assets\CompanyLogos\logo_0.png`..`logo_7.png` y
se cargan con `CompanyLogoImage` según el campo `logo` del perfil; no se dibujan
con iniciales.

Los iconos de clima, batería, aceite, termómetro y manómetro se dibujan por
vectores; no son PNG.

## Clima automático

El SDK de SCS (REV12) no publica el clima. `WeatherReader` lee en tiempo real la
humedad de la carretera que el juego mantiene en memoria y la convierte en
**Despejado**, **Nublado**, **Lluvia** o **Tormenta**. El modo automático es el
predeterminado y la selección manual queda como respaldo.

Desde la bandeja, en **Clima**, se puede elegir **Automático (juego)** o fijar
uno de los cuatro estados. La preferencia se guarda en
`%LOCALAPPDATA%\ATSTelemetry\window.ini`:

```text
weather=Clear|Cloudy|Rain|Storm
weatherAuto=1|0
```

Offsets verificados: ATS `0x3051D40`, ETS2 `0x3046780`; humedad en `+0x5E8`.
Si la estructura no valida, `WeatherReader` devuelve `null` y el panel usa el
clima manual guardado. Los limpiabrisas no alteran el estado.

## Ciclo de trabajo recomendado

1. Edita `src\DashboardRenderer.cs` (o el archivo correspondiente).
2. Cierra la app si está abierta; el ejecutable de `dist` no se puede
   reemplazar mientras está en uso.
3. Compila y publica la app final:

   ```bat
   build.bat
   ```

   El resultado queda en
   `dist\ATS-Telemetry-Windows\ATSTelemetry.exe`, junto con `Assets`, el icono,
   los lanzadores y `LEEME.txt`.
4. Previsualiza con datos de prueba usando **el mismo ejecutable final**:

   ```bat
   run-demo.bat
   ```

   También se puede abrir en ventana:

   ```bat
   dist\ATS-Telemetry-Windows\ATSTelemetry.exe --demo --windowed
   ```

5. Prueba con telemetría real (ATS o ETS2 abierto):

   ```bat
   run.bat
   ```

6. Cierra el panel con `Ctrl+Shift+Q` o desde la bandeja del sistema.

No se usa una copia temporal aparte: lo que se prueba es exactamente lo que se
entrega en `dist\ATS-Telemetry-Windows`.

Si quieres abrir el panel en otra pantalla:

```bat
dist\ATS-Telemetry-Windows\ATSTelemetry.exe --screen=DISPLAY2
```

Por defecto aparece donde esté el cursor y recuerda la última pantalla y el
último modo. Con el panel abierto:

- Clic derecho en la bandeja → **Pantalla** para elegir un monitor.
- **Modo ventana** o `F11` para alternar entre pantalla completa sin bordes y
  una ventana movible/redimensionable.
- El nombre del monitor se muestra arriba a la derecha y cambia al moverlo.

La preferencia se guarda en `%LOCALAPPDATA%\ATSTelemetry\window.ini`.

## Capa SkiaCompat

`DashboardRenderer.cs` usa nombres de `System.Drawing` (`Graphics`, `Pen`,
`SolidBrush`, `Font`, etc.), pero en realidad están redirigidos a
`SkiaCompat.cs`. Las llamadas más comunes (`DrawString`, `DrawLine`,
`DrawEllipse`, `FillRectangle`, `DrawArc`, `MeasureString`, etc.) ya están
implementadas.

Si agregas una llamada nueva que no exista en `SkiaCompat.cs`, el compilador lo
indicará. Agrega el equivalente en la capa de compatibilidad en lugar de
cambiar todo el renderizador.

## Datos disponibles

Los valores que puedes mostrar están en `Snap.cs` y se rellenan en
`TelemetryReader.cs`. Antes de inventar un dato nuevo, comprueba si ya existe en
`Snap`; si no, hay que leerlo del offset correspondiente del MMC
`Local\SCSTelemetry`.

El saldo, la empresa, el logo y los conductores vienen de `BankBalanceReader`,
que lee el savegame y el `profile.sii` del juego activo (ATS o ETS2). El clima
viene de `WeatherReader`.

La API de telemetría no entrega el logo de la empresa. `profile.sii` sí incluye
el campo `logo` (por ejemplo, `logo_4`); `BankBalanceReader` lo lee y
`DashboardRenderer.CompanyLogoImage` carga el PNG real correspondiente de
`Assets\CompanyLogos`. Esos PNG se extrajeron de `base.scs`
(`material/ui/player_logo`), no se dibujan con iniciales. La carga ya no usa
ninguna imagen ni miniatura: solo se muestran los textos del juego.

## Compilación manual

Si prefieres PowerShell:

```powershell
.\build.ps1
```

La compilación es autocontenida (`win-x64`) y no necesita .NET instalado en el
equipo donde se ejecute el EXE. Para regenerar el ZIP portátil:

```powershell
Compress-Archive -Path .\dist\ATS-Telemetry-Windows\* `
  -DestinationPath .\dist\ATS-Telemetry-Windows.zip -Force
```
