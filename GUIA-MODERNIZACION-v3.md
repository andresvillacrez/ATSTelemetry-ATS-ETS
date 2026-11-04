# Guía de modernización de la interfaz — v3.0

**Proyecto:** ATS / ETS2 Native Telemetry App  
**Base:** FH6 v2.7  
**Documento:** guía de implementación  
**Fecha:** 3 de noviembre de 2026

> Este documento no modifica código. Describe qué cambiar, en qué orden, con qué criterios y con ejemplos listos para adaptar. Las referencias de línea corresponden a la versión v2.7 y pueden desplazarse al editar.

---

## 1. Objetivo

Convertir el panel actual en una **HMI automotriz moderna** sin perder la identidad FH6:

- Legible a 1280 × 720 y a distancia de conducción.
- Menos densidad y más jerarquía visual.
- Un sistema de diseño único (color, tipografía, espaciado, radios).
- Integración nativa con Windows 10/11 (ventana, bandeja, ajustes).
- Accesible: contraste, color, escala y movimiento reducido.
- Motion sutil y rendimiento estable a 60 FPS.

La identidad que se conserva: **cluster central**, **fila de testigos**, **paleta oscura** y **logotipos reales**. Lo que se moderniza: paneles laterales, tipografía pequeña, cromo técnico, densidad y ventana.

### Ruta recomendada

| Hito | Fases | Resultado |
|---|---|---|
| **MVP visual** | 0 + 1 | Se ve moderno y legible sin cambiar la estructura |
| **Responsive** | 2 | Funciona a 1280 × 720, 1920 × 1080 y ultrawide |
| **Producto** | 3 + 4 + 5 | Motion, overlay, ajustes, accesibilidad y unidades |

No empezar por el overlay ni por el rediseño de daños. Primero tokens, tipografía y layout: son los cambios que más mejoran la percepción con menos riesgo.

---

## 2. Principios de diseño

1. **Una sola cosa importante por zona.** El cluster manda; los paneles acompañan.
2. **Un solo acento.** El color se reserva para estado: éxito, aviso, peligro, información.
3. **Texto mínimo legible.** 11 px virtuales como suelo; 12 px recomendado para etiquetas.
4. **Superficies planas + hairline.** Evitar el “glass” falso de degradado + borde.
5. **Retícula de 8 px.** Tres radios: 12, 16 y 24.
6. **Nada de información técnica en la vista de conducción.** Monitor, ruta de memoria y nombres internos van a Ajustes/Diagnóstico.
7. **Todo cambio se valida en demo** a 1920 × 1080 y 1280 × 720 antes de tocar `dist`.

---

## 3. Mapa de archivos y responsabilidades

| Archivo | Responsabilidad | Se toca en |
|---|---|---|
| `src/DashboardRenderer.cs` | Todo el dibujo del panel | Fases 0-5 |
| `src/DashboardControl.cs` | Aloja el lienzo SkiaSharp | Fase 2 |
| `src/MainForm.cs` | Ventana, pantalla completa, bandeja | Fases 0, 4 |
| `src/WindowPreferences.cs` | Persistencia de pantalla/modo/clima | Fases 1, 4 |
| `src/Theme.cs` | **Nuevo.** Tokens de color, radios y espaciado | Fase 0 |
| `src/TypeScale.cs` | **Nuevo.** Escala tipográfica | Fase 0 |
| `src/Layout.cs` | **Nuevo.** Modos Full/Compact/Minimal y rectángulos | Fase 2 |
| `src/AppSettings.cs` | **Nuevo.** Ajustes de UI (tema, escala, unidades, motion) | Fase 1 |
| `src/SettingsForm.cs` | **Nuevo.** Ventana de Ajustes | Fase 1/4 |
| `src/OverlayMode.cs` | **Nuevo.** Capa transparente y clic a través | Fase 4 |

No editar `src\bin`, `src\obj` ni `dist\ATS-Telemetry-Windows` a mano: son resultados de compilación.

---

## 4. Fase 0 — Quick wins (1-2 días)

Objetivo: mejorar legibilidad, contraste y coherencia sin cambiar la estructura.

### 4.1 Crear `src/Theme.cs`

```csharp
using System.Drawing;

namespace ATSTelemetry;

internal static class Theme
{
    // Superficies
    public static readonly Color BackgroundTop = Color.FromArgb(255, 10, 12, 15);
    public static readonly Color BackgroundBottom = Color.FromArgb(255, 7, 9, 11);
    public static readonly Color Surface = Color.FromArgb(255, 18, 22, 26);
    public static readonly Color SurfaceRaised = Color.FromArgb(255, 26, 31, 36);
    public static readonly Color Stroke = Color.FromArgb(18, 255, 255, 255);       // 7 %
    public static readonly Color StrokeStrong = Color.FromArgb(30, 255, 255, 255); // 12 %

    // Texto: 4 niveles, no 16
    public static readonly Color TextPrimary = Color.FromArgb(255, 244, 247, 249);
    public static readonly Color TextSecondary = Color.FromArgb(255, 168, 178, 186);
    public static readonly Color TextTertiary = Color.FromArgb(255, 111, 122, 130);
    public static readonly Color TextDisabled = Color.FromArgb(255, 74, 82, 89);

    // Semánticos
    public static readonly Color Accent = Color.FromArgb(255, 64, 209, 255);
    public static readonly Color Success = Color.FromArgb(255, 61, 220, 151);
    public static readonly Color Warning = Color.FromArgb(255, 255, 176, 32);
    public static readonly Color Notice = Color.FromArgb(255, 255, 138, 61);
    public static readonly Color Danger = Color.FromArgb(255, 255, 77, 94);
    public static readonly Color Info = Color.FromArgb(255, 107, 173, 255);

    // Radios
    public const float RadiusChip = 12f;
    public const float RadiusCard = 16f;
    public const float RadiusPanel = 24f;

    // Retícula de 8
    public const float Space1 = 8f;
    public const float Space2 = 16f;
    public const float Space3 = 24f;
    public const float Space4 = 32f;

    public static Color Alpha(Color color, float alpha) =>
        Color.FromArgb(
            (int)Math.Clamp(alpha * 255f, 0f, 255f),
            color.R, color.G, color.B);
}
```

### 4.2 Migrar la paleta sin tocar todas las llamadas

En `DashboardRenderer.cs`, sustituir las constantes actuales por alias a `Theme`. Así se cambia el color en un solo sitio y no hay que reescribir el archivo:

```csharp
private static readonly Color Green = Theme.Success;
private static readonly Color Mint = Color.FromArgb(120, 255, 212); // retirar gradualmente
private static readonly Color Cyan = Theme.Accent;
private static readonly Color Yellow = Theme.Warning;
private static readonly Color Orange = Theme.Notice;
private static readonly Color Red = Theme.Danger;
private static readonly Color Blue = Theme.Info;
private static readonly Color MoonLight = Color.FromArgb(204, 224, 255);
```

**Criterio de aceptación:** el panel se ve igual o casi igual; no hay colores nuevos sueltos.

### 4.3 Superficies planas en `DrawGlassPanel`

Sustituir el degradado y el borde tenue por una superficie plana + hairline + brillo superior:

```csharp
private static void DrawGlassPanel(
    Graphics g,
    float x,
    float y,
    float width,
    float height,
    float radius)
{
    var rect = new RectangleF(x, y, width, height);
    using var path = SquirclePath(rect, radius);

    using (var fill = new SolidBrush(Theme.Surface))
    {
        g.FillPath(fill, path);
    }

    using (var border = new Pen(Theme.Stroke, 1f))
    {
        g.DrawPath(border, path);
    }

    using (var highlight = new Pen(Theme.StrokeStrong, 1f))
    {
        g.DrawLine(highlight, x + radius, y + 0.5f, x + width - radius, y + 0.5f);
    }
}
```

Opcional: dos niveles de superficie. `Theme.Surface` para paneles base y `Theme.SurfaceRaised` para la tarjeta activa o el cluster.

**Criterio de aceptación:** desaparecen los degradados por panel; el fondo general conserva su gradiente sutil.

### 4.4 Escala tipográfica

Crear `src/TypeScale.cs`:

```csharp
namespace ATSTelemetry;

internal static class TypeScale
{
    public const float Display = 184f; // velocidad
    public const float Hero = 34f;     // cifra principal de panel
    public const float Title = 24f;    // nombre de empresa, carga
    public const float Body = 16f;     // texto de lectura
    public const float Label = 12f;    // etiquetas de sección
    public const float Caption = 11f;  // texto secundario
    public const float Micro = 10f;    // solo decorativo, nunca informativo
}
```

Reglas de migración:

| Uso actual | Tamaño actual | Nuevo |
|---|---:|---:|
| Velocidad | 184 | 184 (sin cambio) |
| Cifra principal | 31-34 | 34 |
| Título de panel | 10 con tracking 2,2 | 12 con tracking 1,2 |
| Etiqueta de fila | 8,5-9 | 11 con tracking 0,8 |
| Valor secundario | 11-13 | 14 |
| Pie de panel | 7-8 | eliminar o 10 |
| Microcopy técnica | 6,5-7,5 | eliminar |

Cambios mínimos concretos:

- `DrawFh6VerticalGauge`: subtítulo `6.5f` → `11f`; unidad `10f` → `11f`; marcas `8f` → `10f`.
- `DrawFh6FuelCard`: `84/159 GAL` de `8.5f` → `11f`; `AUT. 738 KM` de `7.5f` → `11f`; pie `7f` → eliminar.
- `DrawFh6WeatherCard`: `HORA DEL JUEGO` de `7.5f` → `11f`; pie `7f` → eliminar.
- `DrawFh6GpsPanel`: etiquetas de `7.5-8.5f` → `11f`; pie `8f` → eliminar.
- `DrawFh6ReservedPanel`: `NOTIFICACIÓN DEL JUEGO` de `7f` → eliminar o `10f`.
- `DrawFh6CargoPanel`: `TIPO` de `10f` → `11f`; pie `8f` → eliminar.
- `DrawFh6FinancePanel`: `SALDO DEL BANCO` de `8f` → `10f`; `MARGEN OPERATIVO` de `8.5f` → `11f`.

Opcional: añadir un suelo en `DrawFitted` como red de seguridad, pero **revisar cada llamada** porque puede provocar desbordes:

```csharp
size = Math.Max(size, TypeScale.Caption);
var font = GetFont(family, size, style);
var floor = Math.Max(minimumSize, TypeScale.Caption);
while (size > floor && g.MeasureString(text, font).Width > rect.Width + 1f)
{
    size -= 0.5f;
    font = GetFont(family, size, style);
}
```

**Criterio de aceptación:** no queda ningún texto informativo por debajo de 11 px virtuales; los textos largos siguen cabiendo o se truncan de forma controlada.

### 4.5 Limpiar el cromo técnico

Cambios concretos en `DashboardRenderer.cs`:

1. **Cabecera:** eliminar el nombre del monitor (`DisplayName`) de la esquina superior derecha. Sustituirlo por:
   - estado de conexión (`EN VIVO` / `DEMO` / `SIN SEÑAL`) con punto de color;
   - hora de última actualización o nombre del perfil.
2. **Pie:** eliminar `"SCS TELEMETRY · LECTURA DIRECTA Local\SCSTelemetry"`. Sustituirlo por:
   - `v3.0 · <perfil>` a la izquierda;
   - estado y última sincronización a la derecha.
3. **Pies de panel:** eliminar `GPS · DATOS DEL JUEGO`, `CLIMA · DATOS DEL JUEGO`, `COMBUSTIBLE · DATOS DEL JUEGO`, `NOTIFICACIÓN DEL JUEGO`, `CARGA DEL TRABAJO ACTUAL`. Si se necesita contexto, usar una sola etiqueta corta y legible.
4. **Placeholders:** unificar `--`, `SIN CARGA`, `SIN DESTINO`, `SIN SEÑAL` en un componente `DrawEmptyState`.

**Criterio de aceptación:** la vista principal no muestra rutas de memoria, nombres de monitor ni etiquetas de 7 px.

### 4.6 Barra de título oscura en modo ventana

Añadir a `MainForm.cs`:

```csharp
[DllImport("dwmapi.dll")]
private static extern int DwmSetWindowAttribute(
    IntPtr hwnd, int attribute, ref int value, int size);

private void ApplyWindowChrome()
{
    if (!IsHandleCreated)
    {
        return;
    }

    try
    {
        var dark = 1;
        // DWMWA_USE_IMMERSIVE_DARK_MODE = 20 (Windows 10 2004+)
        if (DwmSetWindowAttribute(Handle, 20, ref dark, sizeof(int)) != 0)
        {
            // Fallback para builds anteriores
            DwmSetWindowAttribute(Handle, 19, ref dark, sizeof(int));
        }

        var rounded = 2; // DWMWCP_ROUND
        DwmSetWindowAttribute(Handle, 33, ref rounded, sizeof(int));
    }
    catch
    {
        // La app debe funcionar aunque DWM no esté disponible.
    }
}
```

Llamar a `ApplyWindowChrome()` en `OnHandleCreated` y después de `ApplyLayout` cuando `_windowed` sea `true`.

**Criterio de aceptación:** en modo ventana, la barra de título es oscura y las esquinas son redondeadas en Windows 11.

### 4.7 Menú de bandeja oscuro

En `MainForm.BuildTrayIcon`, aplicar un renderer oscuro:

```csharp
private sealed class DarkMenuColors : ProfessionalColorTable
{
    public override Color ToolStripDropDownBackground => Color.FromArgb(24, 28, 32);
    public override Color ImageMarginGradientBegin => Color.FromArgb(24, 28, 32);
    public override Color ImageMarginGradientMiddle => Color.FromArgb(24, 28, 32);
    public override Color ImageMarginGradientEnd => Color.FromArgb(24, 28, 32);
    public override Color MenuBorder => Color.FromArgb(48, 54, 60);
    public override Color MenuItemSelected => Color.FromArgb(38, 44, 50);
    public override Color MenuItemBorder => Color.FromArgb(64, 209, 255);
    public override Color SeparatorDark => Color.FromArgb(48, 54, 60);
    public override Color SeparatorLight => Color.FromArgb(48, 54, 60);
}
```

```csharp
var menu = new ContextMenuStrip
{
    BackColor = Color.FromArgb(24, 28, 32),
    ForeColor = Color.FromArgb(230, 236, 240),
    Renderer = new ToolStripProfessionalRenderer(new DarkMenuColors())
};
```

**Criterio de aceptación:** el menú de bandeja no rompe el lenguaje visual oscuro.

### 4.8 Checklist Fase 0

- [x] `Theme.cs` creado y compilado.
- [x] Paleta migrada sin colores sueltos.
- [x] `DrawGlassPanel` plano con hairline.
- [x] Ningún texto informativo < 11 px.
- [x] Monitor y ruta de memoria fuera de la vista principal.
- [x] Barra de título oscura en ventana.
- [x] Menú de bandeja oscuro.
- [x] Demo 1920 × 1080 y 1280 × 720 revisada.
- [x] `build.bat` y `run-demo.bat` sin errores.

---

## 5. Fase 1 — Sistema y componentes (3-5 días)

Objetivo: dejar de dibujar cada panel “a mano” y tener piezas reutilizables.

### 5.1 Jerarquía de texto

Usar solo cuatro niveles visuales:

| Nivel | Color | Uso |
|---|---|---|
| Primario | `Theme.TextPrimary` | Cifras y nombres principales |
| Secundario | `Theme.TextSecondary` | Etiquetas y datos de apoyo |
| Terciario | `Theme.TextTertiary` | Unidades, notas, pie |
| Deshabilitado | `Theme.TextDisabled` | Sin señal / sin datos |

Regla: **no usar alpha directo** en código nuevo. Usar `Theme.Text*` o `Theme.Alpha(Theme.TextPrimary, 0.6f)` solo para casos justificados.

### 5.2 Componentes reutilizables

Añadir a `DashboardRenderer` (o a un `DashboardComponents.cs`):

```csharp
private static void DrawSectionHeader(
    Graphics g, float x, float y, float width,
    string title, Color accent, bool showStatus = false, string? status = null)
{
    DrawTracked(g, title, Rounded(TypeScale.Label, FontStyle.Bold),
        Theme.TextSecondary, x, y, 1.2f);

    if (showStatus && status != null)
    {
        DrawText(g, status, Mono(TypeScale.Caption, FontStyle.Bold), accent,
            new RectangleF(x, y - 8f, width, 16f), Right);
    }
}

private static void DrawMetricRow(
    Graphics g, float x, float y, float width,
    string label, string value, Color color)
{
    DrawText(g, label, Rounded(TypeScale.Caption, FontStyle.Regular),
        Theme.TextTertiary, new RectangleF(x, y, width * 0.55f, 18f), Left);
    DrawFitted(g, value, "Bahnschrift", TypeScale.Body, FontStyle.Bold, color,
        new RectangleF(x + width * 0.55f, y - 2f, width * 0.45f, 22f), Right, 11f);
}

private static void DrawChip(
    Graphics g, RectangleF rect, string text, Color accent, bool active)
{
    DrawSquircle(g, rect, Theme.RadiusChip,
        active ? Theme.Alpha(accent, 0.10f) : Theme.Alpha(Theme.TextPrimary, 0.045f),
        active ? Theme.Alpha(accent, 0.42f) : Theme.Stroke,
        1f);

    DrawFitted(g, text, "Bahnschrift", TypeScale.Caption, FontStyle.Bold,
        active ? accent : Theme.TextTertiary,
        new RectangleF(rect.X + 8f, rect.Y + 4f, rect.Width - 16f, rect.Height - 8f),
        Center, 10f);
}

private static void DrawEmptyState(
    Graphics g, RectangleF area, string message, string? hint = null)
{
    DrawFitted(g, message, "Bahnschrift", TypeScale.Body, FontStyle.Bold,
        Theme.TextDisabled, area, Center, 11f);

    if (hint != null)
    {
        DrawFitted(g, hint, "Segoe UI", TypeScale.Caption, FontStyle.Regular,
            Theme.TextDisabled,
            new RectangleF(area.X, area.Y + 24f, area.Width, 20f), Center, 10f);
    }
}
```

**Criterio de aceptación:** los paneles nuevos usan estos componentes; no se repiten medidas ni colores.

### 5.3 Iconos unificados

- Sustituir gradualmente los PNG tintados por **vectores** con el mismo grid de 24 px y trazo de 1,75 px.
- Mantener los PNG actuales como fallback mientras se migra.
- Unificar tamaño óptico: 20 px en cabeceras, 24 px en testigos, 32 px en estados grandes.
- Revisar `Assets\Indicators` y `Assets\BrandLogos`: los logos de marca pueden quedar como PNG, pero los iconos funcionales deben ser vectoriales.

### 5.4 Estados vacíos y onboarding

- **Sin señal global:** un solo banner en la cabecera; los paneles muestran `--` atenuado, no diez mensajes distintos.
- **Primer arranque sin juego:** tarjeta centrada con dos pasos:
  1. “Instala el plugin de telemetría” + botón `install-plugin.bat`.
  2. “Abre ATS o ETS2” + botón `Reintentar`.
- **Modo demo:** badge visible en la cabecera, no solo en el pie.
- **Sin trabajo:** el panel de carga muestra “Sin trabajo activo” con icono, no solo `SIN CARGA`.
- **Modelo sin imagen:** mantener el fallback textual actual, pero con icono y estilo de estado vacío.

### 5.5 Ventana de Ajustes

Crear `SettingsForm.cs` con este contenido mínimo:

| Sección | Controles |
|---|---|
| Apariencia | Tema (Oscuro / Alto contraste), acento, escala de UI (100/125/150 %) |
| Diseño | Modo (Completo / Compacto / Mínimo), paneles visibles |
| Unidades | km/h o mph; °C o °F; PSI o bar; L/100 km, MPG o GAL |
| Movimiento | Reducir animaciones; intensidad |
| Ventana | Pantalla, modo ventana, siempre visible, opacidad |
| Overlay | Activar, opacidad, clic a través, atajo |
| Diagnóstico | Versión, perfil, última sincronización, ruta de `window.ini` |

La ventana debe usar el mismo `Theme` y una barra de título oscura. Guardar en `AppSettings` y persistir en `%LOCALAPPDATA%\ATSTelemetry\settings.ini` (o ampliar `window.ini`).

### 5.6 Checklist Fase 1

- [x] Componentes `DrawSectionHeader`, `DrawMetricRow`, `DrawChip`, `DrawEmptyState`.
- [x] Iconos con tamaño y trazo consistentes (parcial: helper vectorial en onboarding; PNG como fallback en testigos).
- [x] Estado sin señal unificado.
- [x] Onboarding de primer arranque.
- [x] Badge DEMO visible.
- [x] Ventana de Ajustes funcional.
- [x] Ajustes persistidos.

---

## 6. Fase 2 — Layout responsive (1-2 semanas)

Objetivo: que la app se vea bien a 1920 × 1080, 1280 × 720 y ventanas pequeñas, sin encoger todo.

### 6.1 Crear `src/Layout.cs`

```csharp
using System.Drawing;

namespace ATSTelemetry;

internal enum LayoutMode
{
    Full,
    Compact,
    Minimal
}

internal sealed class Layout
{
    public LayoutMode Mode { get; init; }
    public float CanvasWidth { get; init; }
    public float CanvasHeight { get; init; }

    public RectangleF Finance { get; init; }
    public RectangleF Cargo { get; init; }
    public RectangleF Damage { get; init; }
    public RectangleF Gps { get; init; }
    public RectangleF Fuel { get; init; }
    public RectangleF Notices { get; init; }
    public RectangleF Weather { get; init; }
    public RectangleF Water { get; init; }
    public RectangleF Air { get; init; }
    public PointF ClusterCenter { get; init; }
    public float ClusterRadius { get; init; }

    public bool ShowVerticalGauges { get; init; }
    public bool ShowDetailRows { get; init; }
    public bool ShowBottomCards { get; init; }

    public static LayoutMode ForSize(int width, int height)
    {
        if (width >= 1600 && height >= 900) return LayoutMode.Full;
        if (width >= 1024 && height >= 576) return LayoutMode.Compact;
        return LayoutMode.Minimal;
    }

    public static Layout Create(LayoutMode mode) => mode switch
    {
        LayoutMode.Full => Full(),
        LayoutMode.Compact => Compact(),
        _ => Minimal()
    };

    private static Layout Full() => new()
    {
        Mode = LayoutMode.Full,
        CanvasWidth = 1920f,
        CanvasHeight = 1080f,
        Finance = new RectangleF(64f, 116f, 400f, 380f),
        Cargo = new RectangleF(64f, 512f, 400f, 424f),
        Damage = new RectangleF(1456f, 116f, 400f, 380f),
        Gps = new RectangleF(1456f, 512f, 400f, 424f),
        Fuel = new RectangleF(486f, 794f, 240f, 142f),
        Notices = new RectangleF(742f, 794f, 438f, 142f),
        Weather = new RectangleF(1196f, 794f, 237f, 142f),
        Water = new RectangleF(486f, 300f, 88f, 440f),
        Air = new RectangleF(1346f, 300f, 88f, 440f),
        ClusterCenter = new PointF(960f, 548f),
        ClusterRadius = 302f,
        ShowVerticalGauges = true,
        ShowDetailRows = true,
        ShowBottomCards = true
    };

    private static Layout Compact() => new()
    {
        Mode = LayoutMode.Compact,
        CanvasWidth = 1280f,
        CanvasHeight = 720f,
        Finance = new RectangleF(24f, 256f, 300f, 200f),
        Cargo = new RectangleF(24f, 468f, 300f, 228f),
        Damage = new RectangleF(956f, 140f, 300f, 220f),
        Gps = new RectangleF(956f, 372f, 300f, 324f),
        Fuel = new RectangleF(24f, 140f, 300f, 104f),
        Notices = new RectangleF(340f, 126f, 600f, 28f),
        Weather = new RectangleF(956f, 44f, 300f, 72f),
        Water = RectangleF.Empty,
        Air = RectangleF.Empty,
        ClusterCenter = new PointF(640f, 350f),
        ClusterRadius = 190f,
        ShowVerticalGauges = false,
        ShowDetailRows = false,
        ShowBottomCards = false
    };

    private static Layout Minimal() => new()
    {
        Mode = LayoutMode.Minimal,
        CanvasWidth = 960f,
        CanvasHeight = 540f,
        Finance = RectangleF.Empty,
        Cargo = RectangleF.Empty,
        Damage = RectangleF.Empty,
        Gps = RectangleF.Empty,
        Fuel = new RectangleF(16f, 110f, 200f, 90f),
        Notices = new RectangleF(16f, 470f, 928f, 24f),
        Weather = RectangleF.Empty,
        Water = RectangleF.Empty,
        Air = RectangleF.Empty,
        ClusterCenter = new PointF(480f, 270f),
        ClusterRadius = 150f,
        ShowVerticalGauges = false,
        ShowDetailRows = false,
        ShowBottomCards = false
    };
}
```

### 6.2 Cambiar `Render` para elegir layout

```csharp
public void Render(SKCanvas canvas, int width, int height)
{
    canvas.Clear(SKColors.Black);

    if (width <= 0 || height <= 0) return;

    var mode = Layout.ForSize(width, height);
    var layout = Layout.Create(mode);
    var scale = Math.Min(width / layout.CanvasWidth, height / layout.CanvasHeight);
    var offsetX = (width - layout.CanvasWidth * scale) / 2f;
    var offsetY = (height - layout.CanvasHeight * scale) / 2f;

    var state = canvas.Save();
    canvas.Translate(offsetX, offsetY);
    canvas.Scale(scale, scale);

    var g = new Graphics(canvas);
    var snap = _reader.Snapshot();
    var now = DateTime.UtcNow;
    // ...
    DrawDashboard(g, snap, layout, ignitionOn, bulbCheck, ignitionElapsed);
    // ...
}
```

### 6.3 Composición Full (1920 × 1080)

Mantener la actual. Solo cambian los tokens, la tipografía y el contenido.

| Zona | Rectángulo |
|---|---|
| Cabecera | `y = 15..92` |
| Testigos | `y = 108..166` |
| Cluster | centro `(960, 548)`, radio `302` |
| Agua | `486, 300, 88 × 440` |
| Aire | `1346, 300, 88 × 440` |
| Empresa | `64, 116, 400 × 380` |
| Carga | `64, 512, 400 × 424` |
| Daños | `1456, 116, 400 × 380` |
| GPS | `1456, 512, 400 × 424` |
| Combustible | `486, 794, 240 × 142` |
| Avisos | `742, 794, 438 × 142` |
| Clima | `1196, 794, 237 × 142` |
| Pie | `y = 984..1032` |

### 6.4 Composición Compact (1280 × 720)

Objetivo: menos datos, texto más grande, sin gauges verticales.

| Zona | Rectángulo | Contenido |
|---|---|---|
| Cabecera | `0..64` | Marca + estado + clima compacto |
| Testigos | `74..118` | 13 iconos de 24 px |
| Cluster | centro `(640, 350)`, radio `190` | Velocidad 120 px, marcha, RPM, odómetro |
| Combustible | `24, 140, 300 × 104` | Nivel, %, autonomía |
| Empresa | `24, 256, 300 × 200` | 3 filas: trabajo, ganancia, saldo |
| Carga | `24, 468, 300 × 228` | Nombre, peso, integridad, ruta, ETA |
| Daños | `956, 140, 300 × 220` | 3 barras + imagen pequeña |
| GPS | `956, 372, 300 × 324` | Brújula, destino, ETA, progreso |
| Avisos | `340, 126, 600 × 28` | Ticker de una línea |
| Clima | `956, 44, 300 × 72` | Condición + hora |

Reglas de Compact:

- Ocultar gauges verticales de agua y aire.
- Ocultar filas secundarias de Empresa (egresos, conductores, margen).
- Ocultar `ORIGEN`, `ODÓMETRO`, `POSICIÓN` y `RECORRIDO` del GPS.
- Ocultar pies de panel.
- Mantener la imagen del camión, pero a 140-160 px de alto.
- Tipografía mínima: 12 px virtuales (equivalen a 12 px físicos a 1280 × 720).

### 6.5 Composición Minimal (960 × 540)

Para ventana pequeña o modo “solo conducción”.

| Zona | Rectángulo |
|---|---|
| Cabecera | `0..56` |
| Testigos | `64..100` |
| Cluster | centro `(480, 270)`, radio `150` |
| Combustible | `16, 110, 200 × 90` |
| Avisos | `16, 470, 928 × 24` |
| ETA / límite | `744, 110, 200 × 120` |

Reglas de Minimal:

- Solo cluster, combustible, ETA/límite y ticker de avisos.
- Nada de empresa, carga, daños ni GPS.
- Velocidad 96 px, marcha 40 px, RPM 200 × 8.
- Texto mínimo: 11 px virtuales.

### 6.6 Ultrawide y otras relaciones

Para 21:9 y 32:9:

- Calcular `CanvasWidth = 1080 * (width / height)` y mantener `CanvasHeight = 1080`.
- Anclar paneles laterales a `64` y `CanvasWidth - 64 - 400`.
- Centrar el cluster en `CanvasWidth / 2`.
- Distribuir las tarjetas inferiores entre los paneles laterales.
- Evitar barras negras: extender el degradado de fondo.

Para 16:10 y 4:3, usar el mismo principio: el layout se adapta al ancho disponible, no se escala todo.

### 6.7 Checklist Fase 2

- [x] `Layout.cs` con Full, Compact y Minimal.
- [x] `Render` elige layout por tamaño.
- [x] Compact probado a 1280 × 720 y 1366 × 768.
- [x] Minimal probado a 960 × 540 y 1024 × 600.
- [x] Full probado a 1920 × 1080 y 2560 × 1440.
- [x] Ultrawide probado a 3440 × 1440 (y 5120 × 1440).
- [x] Ningún texto informativo < 11 px.
- [x] Sin solapamientos ni recortes.

---

## 7. Fase 3 — Movimiento y rendimiento

### 7.1 Suavizar valores en vivo

Añadir `AnimatedValue`:

```csharp
internal sealed class AnimatedValue
{
    private double _value;
    private double _velocity;

    public double Value => _value;

    public void Update(double target, double dt)
    {
        const double stiffness = 120.0;
        const double damping = 20.0;

        var force = (target - _value) * stiffness - _velocity * damping;
        _velocity += force * dt;
        _value += _velocity * dt;

        if (Math.Abs(target - _value) < 0.001 && Math.Abs(_velocity) < 0.001)
        {
            _value = target;
            _velocity = 0;
        }
    }

    public void Snap(double target)
    {
        _value = target;
        _velocity = 0;
    }
}
```

Usarlo para velocidad, RPM, barras de combustible/agua/aire y progreso. Calcular `dt` en `Render`:

```csharp
private DateTime _lastFrameUtc = DateTime.UtcNow;
// ...
var dt = Math.Clamp((now - _lastFrameUtc).TotalSeconds, 0.0, 0.05);
_lastFrameUtc = now;
```

### 7.2 Movimiento reducido

- Añadir un ajuste `ReduceMotion` en `AppSettings`.
- Si está activo, `AnimatedValue.Snap(target)` y sin pulsos.
- Opcional: leer `SPI_GETCLIENTAREAANIMATION` de Windows para respetar la preferencia del sistema.

### 7.3 Cachear capas estáticas

Hoy se redibuja todo cada frame. Para liberar presupuesto:

- Cachear fondo + marcos de panel + etiquetas estáticas en un `SKPicture`.
- Reconstruir solo al cambiar de tamaño, layout o tema.
- Dibujar por capas: fondo, paneles, valores dinámicos, overlays.
- Reutilizar `SKPaint`/pinceles en lugar de crear `Pen` y `SolidBrush` en cada frame.

Ejemplo de estructura:

```csharp
private SKPicture? _staticLayer;
private int _staticWidth;
private int _staticHeight;

private void EnsureStaticLayer(int width, int height)
{
    if (_staticLayer != null && _staticWidth == width && _staticHeight == height)
    {
        return;
    }

    using var recorder = new SKPictureRecorder();
    var canvas = recorder.BeginRecording(new SKRect(0, 0, width, height));
    // Dibujar fondo, paneles y etiquetas estáticas
    _staticLayer = recorder.EndRecording();
    _staticWidth = width;
    _staticHeight = height;
}
```

### 7.4 Presupuesto de frame

- Objetivo: < 16,6 ms por frame a 60 FPS.
- Medir con un overlay de diagnóstico opcional (tiempo de frame, FPS, uso de GC).
- Evitar asignaciones en el bucle de dibujo: fuentes ya están cacheadas; extender el cache a pinceles y paths.

### 7.5 Checklist Fase 3

- [x] `AnimatedValue` para velocidad, RPM, combustible, agua, aire y progreso.
- [x] `ReduceMotion` hace `Snap` y elimina pulsos.
- [x] Fondo y halos cacheados en un `SKPicture` por tamaño de canvas.
- [x] Overlay de diagnóstico opcional (FPS, frame, GC) y preferencia persistida.
- [x] Presupuesto medido: Full 8,97 ms; ultrawide 12,39 ms; Compact 3,18 ms; Minimal 1,53 ms.
- [ ] Marcos de panel y etiquetas estáticas en la capa cacheada (siguiente iteración).

---

## 8. Fase 4 — Integración con Windows y overlay

### 8.1 Tema oscuro de ventana

Ya descrito en Fase 0. En Fase 4, ampliar a la ventana de Ajustes y a cualquier diálogo.

### 8.2 Ajustes persistentes

Ampliar `WindowPreferences` o crear `AppSettings` con:

```text
theme=dark|contrast
accent=#40D1FF
uiScale=1.0
layout=auto|full|compact|minimal
units=speed:kmh|mph;temp:c|f;pressure:psi|bar;fuel:l100|mpg|gal
reduceMotion=0|1
overlay=0|1
overlayOpacity=0.85
overlayClickThrough=0|1
alwaysOnTop=1
language=es|en
```

### 8.3 Modo overlay clic a través

Añadir a `MainForm.cs`:

```csharp
private const int GwlExStyle = -20;
private const int WsExLayered = 0x80000;
private const int WsExTransparent = 0x20;

[DllImport("user32.dll", SetLastError = true)]
private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

[DllImport("user32.dll", SetLastError = true)]
private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

[DllImport("user32.dll")]
private static extern bool SetLayeredWindowAttributes(
    IntPtr hwnd, uint colorKey, byte alpha, uint flags);
```

Activar:

```csharp
var exStyle = GetWindowLong(Handle, GwlExStyle);
SetWindowLong(Handle, GwlExStyle, exStyle | WsExLayered | WsExTransparent);
SetLayeredWindowAttributes(Handle, 0, 220, 0x2); // LWA_ALPHA
```

Desactivar:

```csharp
var exStyle = GetWindowLong(Handle, GwlExStyle);
SetWindowLong(Handle, GwlExStyle, exStyle & ~WsExTransparent);
```

**Seguridad obligatoria:** el modo clic a través no debe poder dejar al usuario atrapado. Añadir un atajo global (`Ctrl + Shift + O`) y un temporizador de escape; nunca arrancar en ese modo.

### 8.4 Atajos globales

Registrar `RegisterHotKey` para:

- Mostrar/ocultar overlay.
- Alternar clic a través.
- Cambiar layout (Full/Compact/Minimal).
- Abrir Ajustes.

Procesar `WM_HOTKEY = 0x0312` en `WndProc`. Si falla el registro, avisar en la bandeja y no bloquear la app.

### 8.5 Persistencia de ventana

Guardar en `window.ini`:

- pantalla,
- modo ventana,
- bounds (x, y, ancho, alto),
- layout,
- opacidad,
- siempre visible.

### 8.6 Checklist Fase 4

- [x] Barra de título oscura en `MainForm` y `SettingsForm`.
- [x] Menú de bandeja oscuro.
- [x] Preferencias persistentes: pantalla, ventana, bounds, tema, layout, opacidad, siempre visible.
- [x] Tema oscuro y alto contraste aplicable en caliente.
- [x] Overlay con opacidad y clic a través.
- [x] Atajo de escape `Ctrl+Shift+O` y guardia de 30 minutos.
- [x] Atajos globales para overlay, ajustes y layout.
- [ ] Acento, escala de UI, unidades e idioma (Fase 5).

---

## 9. Fase 5 — Accesibilidad y contenido

### 9.1 Contraste

- Texto informativo mínimo: `Theme.TextSecondary` sobre `Theme.Surface`.
- No usar `White(0.18-0.32)` para información; solo para decoración.
- Revisar contraste con herramienta WCAG: objetivo 4,5:1 para texto normal y 3:1 para texto grande.

### 9.2 Daltonismo

- Probar la paleta con simulación de deuteranopia y protanopia.
- No depender solo del color: añadir icono, forma o texto.
- Para daños, usar además del color el porcentaje y la etiqueta (`OK`, `LEVE`, `REVISAR`, `CRÍTICO`).

### 9.3 Escala de UI

- Ajuste 100 / 125 / 150 %.
- En lugar de escalar solo el canvas, cambiar el layout y los tamaños de fuente.
- Probar a 100 %, 150 % y 200 % de DPI de Windows.

### 9.4 Unidades e idioma

- Unidades configurables: km/h o mph, °C o °F, PSI o bar, L/100 km, MPG o GAL.
- Textos en recursos (`Resources.resx`) para es/en.
- Formato de números con `CultureInfo` según idioma, no hardcodear `es-ES`.

### 9.5 Checklist Fase 5

- [x] Contraste: `TextTertiary` oscuro a 4,82:1 y etiquetas activas con `Theme.Text*`.
- [x] Daltonismo: porcentaje y etiqueta (`OK`, `LEVE`, `REVISAR`, `CRÍTICO`) en daños.
- [x] Escala de UI 100 / 125 / 150 % aplicada a la tipografía del dashboard.
- [x] Unidades métrico/imperial para velocidad, temperatura, presión y volumen de combustible.
- [x] Item 3.3: fondo y marcos de panel cacheados en `SKPicture`.
- [ ] Reflow completo del layout para 150 % (hoy se escala la tipografía).
- [ ] Idioma es/en y extracción completa a recursos.
- [ ] Consumo L/100 km o MPG (requiere confirmar las unidades de `Snap.Cons`).

---

## 10. Cambios panel por panel

| Panel | Problema actual | Cambio propuesto | Método |
|---|---|---|---|
| Cabecera | Nombre del monitor y estado técnico | Estado de conexión + última sync + perfil | `DrawFh6Header` |
| Testigos | 13 iconos siempre visibles, cromo | Iconos 24-28 px, agrupados, sin marco pesado | `DrawFh6IndicatorRow` |
| Cluster | Odómetro en pill, cifras proporcionales | Número tabular limpio, sin pill, suavizado | `DrawFh6SpeedCluster` |
| Agua/Aire | Gauges de 440 px, subtítulo 6,5 px | En Full, más finos con banda objetivo; en Compact, ocultar | `DrawFh6VerticalGauge` |
| Empresa | 7 filas + logo en 380 px | 3 filas primarias; detalle en página Economía | `DrawFh6FinancePanel` |
| Carga | ~10 campos | Dos bloques: Carga y Viaje | `DrawFh6CargoPanel` |
| Daños | 3 barras + imagen con fondo claro | Esquema de camión + 3 barras; imagen sin fondo o con degradado | `DrawFh6DamagePanel` |
| GPS | ~10 campos | Brújula + destino + ETA + progreso; resto secundario | `DrawFh6GpsPanel` |
| Combustible | Sin consumo, unidades fijas | Consumo, reserva, unidades configurables | `DrawFh6FuelCard` |
| Avisos | Un solo naranja | Icono y color por tipo; ticker en Compact | `DrawFh6ReservedPanel` |
| Clima | Tarjeta separada | Mantener en Full; mover a cabecera en Compact | `DrawFh6WeatherCard` |
| Pie | Ruta de memoria y texto técnico | Versión, perfil, última sync | `DrawFh6Footer` |

### 10.1 Detalle de Empresa

Actual: `TRABAJO ACTUAL`, `CONDUCTORES`, `EGRESOS RUTA`, `GANANCIA NETA`, `DINERO TOTAL`, `SALDO DEL BANCO`, `MARGEN OPERATIVO`.

Propuesto en Full:

- Fila 1: `TRABAJO ACTUAL` (verde).
- Fila 2: `GANANCIA NETA` (cian).
- Fila 3: `SALDO DEL BANCO` (cian, cifra grande).
- Barra de margen al pie.
- `CONDUCTORES` y `EGRESOS` pasan a una página “Economía” o a un detalle expandible.

En Compact: solo las 3 filas, sin barra de margen.

### 10.2 Detalle de Carga

Actual: nombre, tipo, peso, integridad, trabajo, remolque, ruta, distancia, entrega.

Propuesto:

- Bloque “Carga”: nombre, peso, integridad.
- Bloque “Viaje”: ruta, distancia, ETA.
- `TIPO`, `TRABAJO`, `REMOLQUE` pasan a detalle secundario.
- En Compact: nombre, peso, integridad, ruta, ETA.

### 10.3 Detalle de Daños

- Mantener las 3 barras (CAMIÓN, TRÁILER, CARGA).
- Sustituir la imagen de fondo claro por:
  - PNG con alfa, o
  - JPG con máscara de degradado + viñeta + sombra de suelo.
- Añadir nombre del modelo sobre la imagen, en `Theme.TextSecondary`.
- Opcional: esquema de camión con zonas frontal/lateral/trasera.

### 10.4 Detalle de GPS

Actual: rumbo, destino, origen, faltan, recorrido, odómetro, llegada, hora prevista, progreso, posición.

Propuesto en Full:

- Brújula + límite.
- Destino.
- `FALTAN` grande.
- `LLEGADA` y `HORA PREVISTA`.
- Barra de progreso.
- Origen, odómetro, recorrido y posición a detalle secundario.

En Compact: brújula, destino, faltan, ETA, progreso.

### 10.5 Detalle de Combustible

- Nivel y porcentaje.
- Galones o litros según unidad.
- Autonomía.
- Consumo medio (si está disponible en `Snap`; si no, no inventar).
- Marcador de reserva en la barra.
- En Compact: nivel, porcentaje, autonomía.

### 10.6 Checklist Fase 6 — Paneles

- [x] Empresa: 3 filas primarias + barra de margen; conductores/egresos en detalle.
- [x] Carga: bloques Carga y Viaje; tipo/trabajo/remolque en detalle.
- [x] Daños: 3 barras con porcentaje y etiqueta; imagen con degradado inferior y nombre del modelo.
- [x] GPS: brújula, destino, faltan, llegada, progreso; resto en detalle.
- [x] Combustible: nivel, porcentaje, volumen, autonomía y marcador de reserva.
- [ ] Consumo medio L/100 km o MPG (unidades de `Snap.Cons` por confirmar).

### 11.5 Resultado de la matriz de pruebas

- Resoluciones: 1920×1080, 2560×1440, 3440×1440, 1920×1200, 1280×720, 1366×768, 1024×600 y 960×540.
- Estados: demo y sin señal.
- Accesibilidad: alto contraste, 150 % de escala y unidades imperiales.
- Presupuesto: Full 8,84 ms; 2560×1440 12,02 ms; ultrawide 11,86 ms; Compact 2,87–4,03 ms; Minimal 1,46–1,48 ms.

---

## 11. Plan de pruebas

### 11.1 Matriz de resoluciones

| Resolución | Layout esperado | Qué revisar |
|---|---|---|
| 1920 × 1080 | Full | Referencia visual |
| 2560 × 1440 | Full | Escalado y nitidez |
| 1280 × 720 | Compact | Legibilidad y recortes |
| 1366 × 768 | Compact | Ancho real de paneles |
| 1024 × 600 | Compact/Minimal | Sin solapamientos |
| 960 × 540 | Minimal | Solo lo esencial |
| 3440 × 1440 | Full ultrawide | Anclaje a bordes |

### 11.2 Matriz de estados

- Sin señal.
- Motor apagado.
- Motor encendido sin avería.
- Avería de motor.
- Combustible bajo.
- Daños críticos.
- Sin trabajo.
- Trabajo especial.
- Textos largos (empresa, carga, destino).
- Modo demo.
- Modo real con ATS y ETS2.

### 11.3 Matriz de accesibilidad

- Contraste de texto.
- Daltonismo (deuteranopia/protanopia).
- Escala de UI 100/125/150 %.
- DPI 100/150/200 %.
- Movimiento reducido.
- Navegación por teclado en Ajustes.

### 11.4 Rendimiento

- 60 FPS estables.
- Tiempo de frame < 16,6 ms.
- Sin crecimiento de memoria en 10 minutos.
- Sin fugas de GDI+ ni Skia.
- Cambio de layout sin parpadeo.

---

## 12. Orden de implementación y commits

1. `feat(theme): add design tokens`
2. `refactor(ui): flat surfaces and type scale`
3. `refactor(ui): remove technical chrome`
4. `feat(window): dark title bar and tray menu`
5. `feat(ui): reusable panel components`
6. `feat(ui): empty states and onboarding`
7. `feat(settings): settings window and persistence`
8. `feat(layout): add layout metrics and compact mode`
9. `feat(layout): add minimal mode`
10. `feat(motion): animated values and reduced motion`
11. `perf(render): cache static layers`
12. `feat(overlay): click-through mode and hotkeys`
13. `feat(a11y): contrast, colorblind and UI scale`
14. `feat(i18n): resources and units`

Cada commit debe incluir captura de demo a 1920 × 1080 y 1280 × 720, y actualizar `HISTORIAL.md` al cerrar la versión.

---

## 13. Riesgos y rollback

| Riesgo | Mitigación |
|---|---|
| Subir tamaños de fuente provoca desbordes | Revisar cada panel en demo; usar `DrawFitted` con mínimo y truncado |
| Cambiar el canvas rompe coordenadas | Introducir `Layout` antes de mover rectángulos; migrar panel a panel |
| El overlay clic a través deja al usuario atrapado | Atajo global obligatorio + temporizador de escape + no arrancar activo |
| La barra oscura falla en Windows 10 antiguo | `try/catch` y fallback al borde nativo |
| Las imágenes sin fondo no existen para los 45 modelos | Mantener JPG actual con máscara/degradado; migrar a PNG por lotes |
| El cache de capas se desincroniza | Invalidar en resize, cambio de tema y cambio de layout |
| La escala de UI rompe el layout | Probar 100/125/150 % y ajustar breakpoints |

Rollback: cada fase debe poder revertirse con un commit. Mantener la v2.7 en `dist` hasta validar la nueva versión.

---

## 14. Anexos

### A. Tokens finales

| Token | Valor |
|---|---|
| `BackgroundTop` | `#0A0C0F` |
| `BackgroundBottom` | `#07090B` |
| `Surface` | `#12161A` |
| `SurfaceRaised` | `#1A1F24` |
| `Stroke` | `#FFFFFF12` |
| `StrokeStrong` | `#FFFFFF1E` |
| `TextPrimary` | `#F4F7F9` |
| `TextSecondary` | `#A8B2BA` |
| `TextTertiary` | `#6F7A82` |
| `TextDisabled` | `#4A5259` |
| `Accent` | `#40D1FF` |
| `Success` | `#3DDC97` |
| `Warning` | `#FFB020` |
| `Notice` | `#FF8A3D` |
| `Danger` | `#FF4D5E` |
| `Info` | `#6BADFF` |

### B. Escala tipográfica

| Rol | Tamaño | Peso | Uso |
|---|---:|---|---|
| Display | 184 | Light | Velocidad |
| Hero | 34 | Bold | Cifra principal |
| Title | 24 | Bold | Nombre de empresa/carga |
| Body | 16 | Regular/Bold | Texto de lectura |
| Label | 12 | Bold | Etiquetas de sección |
| Caption | 11 | Regular/Bold | Texto secundario |
| Micro | 10 | Bold | Solo decorativo |

### C. Copy a eliminar o sustituir

| Actual | Nuevo |
|---|---|
| `SCS TELEMETRY · LECTURA DIRECTA Local\SCSTelemetry` | `v3.0 · <perfil>` |
| `GPS · DATOS DEL JUEGO` | Eliminar |
| `CLIMA · DATOS DEL JUEGO` | Eliminar |
| `COMBUSTIBLE · DATOS DEL JUEGO` | Eliminar |
| `NOTIFICACIÓN DEL JUEGO` | Eliminar o `AVISO` |
| `CARGA DEL TRABAJO ACTUAL` | Eliminar |
| `IMAGEN DEL MODELO NO DISPONIBLE` | `Sin imagen del modelo` + icono |
| `SIN SEÑAL` repetido | Banner global + `--` atenuado |

### D. Coordenadas de referencia

Ver tablas de las secciones 6.3, 6.4 y 6.5. Ajustar tras probar en demo; las coordenadas de Compact y Minimal son un punto de partida, no un contrato rígido.

### E. Referencias de código v2.7

- Lienzo fijo: `DashboardRenderer.cs:26-27`.
- Escalado: `DashboardRenderer.cs:125-131`.
- Paleta: `DashboardRenderer.cs:86-93`.
- Paneles de cristal: `DashboardRenderer.cs:1751-1778`.
- Marco de panel antiguo: `DashboardRenderer.cs:4054-4060`.
- Empresa: `DashboardRenderer.cs:980-1051`.
- Carga: `DashboardRenderer.cs:1068-1162`.
- Daños: `DashboardRenderer.cs:1190-1307`.
- GPS: `DashboardRenderer.cs:1335-1463`.
- Combustible: `DashboardRenderer.cs:1465-1532`.
- Avisos: `DashboardRenderer.cs:1636-1673`.
- Clima: `DashboardRenderer.cs:1675-1734`.
- Pie: `DashboardRenderer.cs:1736-1749`.
- Helpers de texto: `DashboardRenderer.cs:3806-3896`.
- Ventana y bandeja: `MainForm.cs:194-245`, `360-411`.
- Preferencias: `WindowPreferences.cs`.
- DPI: `ATSTelemetry.Windows.csproj:31`.

---

**Fin de la guía.**
