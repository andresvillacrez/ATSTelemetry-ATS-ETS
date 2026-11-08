using System.Globalization;
using System.IO;

namespace ATSTelemetry;

public enum UnitSystem
{
    Metric,
    Imperial
}

/// <summary>
/// Preferencias de ventana y de interfaz por usuario. Se guardan fuera de la
/// carpeta de la app para que la carpeta portátil pueda estar en un medio de
/// solo lectura.
/// </summary>
internal sealed class WindowPreferences
{
    private static readonly string Folder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ATSTelemetry");

    private static readonly string FilePath = Path.Combine(Folder, "window.ini");

    /// <summary>Ruta pública usada por el diagnóstico de Ajustes.</summary>
    public static string PreferencesPath => FilePath;

    public string? ScreenDevice { get; set; }

    public bool Windowed { get; set; }

    public ThemeMode Theme { get; set; } = ThemeMode.Dark;

    /// <summary>Escala tipográfica global (1.0, 1.25 o 1.5).</summary>
    public float UiScale { get; set; } = 1f;

    /// <summary>Sistema de unidades: métrico o imperial.</summary>
    public UnitSystem Units { get; set; } = UnitSystem.Metric;

    /// <summary>Idioma de la interfaz.</summary>
    public AppLanguage Language { get; set; } = AppLanguage.Es;

    /// <summary>
    /// Última posición y tamaño en modo ventana. Se guardan solo cuando la
    /// ventana está normal (ni maximizada ni en overlay).
    /// </summary>
    public int? WindowX { get; set; }

    public int? WindowY { get; set; }

    public int? WindowWidth { get; set; }

    public int? WindowHeight { get; set; }

    /// <summary>
    /// Muestra las filas secundarias de los paneles. Desactivado por defecto:
    /// la vista de conducción prioriza tres datos por panel.
    /// </summary>
    public bool ShowDetails { get; set; }

    public bool ReduceMotion { get; set; }

    /// <summary>Overlay opcional con FPS, tiempo de frame y memoria GC.</summary>
    public bool ShowDiagnostics { get; set; }

    public bool AlwaysOnTop { get; set; } = true;

    public bool Overlay { get; set; }

    public float OverlayOpacity { get; set; } = 0.90f;

    /// <summary>
    /// Nunca se restaura al arrancar: el clic a través solo se activa de forma
    /// explícita y siempre con un atajo de escape disponible.
    /// </summary>
    public bool OverlayClickThrough { get; set; }

    public LayoutMode Layout { get; set; } = LayoutMode.Auto;

    public static WindowPreferences Load()
    {
        var preferences = new WindowPreferences();

        try
        {
            if (!File.Exists(FilePath))
            {
                return preferences;
            }

            foreach (var line in File.ReadAllLines(FilePath))
            {
                var separator = line.IndexOf('=');
                if (separator <= 0)
                {
                    continue;
                }

                var key = line[..separator].Trim();
                var value = line[(separator + 1)..].Trim();

                if (key.Equals("screen", StringComparison.OrdinalIgnoreCase))
                {
                    preferences.ScreenDevice = value;
                }
                else if (key.Equals("windowed", StringComparison.OrdinalIgnoreCase))
                {
                    preferences.Windowed = ParseBool(value);
                }
                else if (key.Equals("theme", StringComparison.OrdinalIgnoreCase)
                         && Enum.TryParse<ThemeMode>(value, true, out var theme))
                {
                    preferences.Theme = theme;
                }
                else if (key.Equals("uiScale", StringComparison.OrdinalIgnoreCase)
                         && float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var uiScale))
                {
                    preferences.UiScale = Math.Clamp(uiScale, 1f, 1.5f);
                }
                else if (key.Equals("units", StringComparison.OrdinalIgnoreCase)
                         && Enum.TryParse<UnitSystem>(value, true, out var units))
                {
                    preferences.Units = units;
                }
                else if (key.Equals("language", StringComparison.OrdinalIgnoreCase)
                         && Enum.TryParse<AppLanguage>(value, true, out var language))
                {
                    preferences.Language = language;
                }
                else if (key.Equals("windowX", StringComparison.OrdinalIgnoreCase)
                         && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var windowX))
                {
                    preferences.WindowX = windowX;
                }
                else if (key.Equals("windowY", StringComparison.OrdinalIgnoreCase)
                         && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var windowY))
                {
                    preferences.WindowY = windowY;
                }
                else if (key.Equals("windowWidth", StringComparison.OrdinalIgnoreCase)
                         && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var windowWidth))
                {
                    preferences.WindowWidth = windowWidth;
                }
                else if (key.Equals("windowHeight", StringComparison.OrdinalIgnoreCase)
                         && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var windowHeight))
                {
                    preferences.WindowHeight = windowHeight;
                }
                else if (key.Equals("showDetails", StringComparison.OrdinalIgnoreCase))
                {
                    preferences.ShowDetails = ParseBool(value);
                }
                else if (key.Equals("reduceMotion", StringComparison.OrdinalIgnoreCase))
                {
                    preferences.ReduceMotion = ParseBool(value);
                }
                else if (key.Equals("showDiagnostics", StringComparison.OrdinalIgnoreCase))
                {
                    preferences.ShowDiagnostics = ParseBool(value);
                }
                else if (key.Equals("alwaysOnTop", StringComparison.OrdinalIgnoreCase))
                {
                    preferences.AlwaysOnTop = ParseBool(value);
                }
                else if (key.Equals("overlay", StringComparison.OrdinalIgnoreCase))
                {
                    preferences.Overlay = ParseBool(value);
                }
                else if (key.Equals("overlayOpacity", StringComparison.OrdinalIgnoreCase)
                         && float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var opacity))
                {
                    preferences.OverlayOpacity = Math.Clamp(opacity, 0.20f, 1.00f);
                }
                else if (key.Equals("overlayClickThrough", StringComparison.OrdinalIgnoreCase))
                {
                    // Se lee, pero MainForm no lo aplica al arrancar.
                    preferences.OverlayClickThrough = ParseBool(value);
                }
                else if (key.Equals("layout", StringComparison.OrdinalIgnoreCase)
                         && Enum.TryParse<LayoutMode>(value, true, out var layout))
                {
                    preferences.Layout = layout;
                }
            }
        }
        catch
        {
            // Si el archivo no se puede leer, se usan los valores por defecto.
        }

        return preferences;
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllLines(FilePath, new[]
            {
                "screen=" + (ScreenDevice ?? string.Empty),
                "windowed=" + (Windowed ? "1" : "0"),
                "theme=" + Theme,
                "uiScale=" + UiScale.ToString("0.00", CultureInfo.InvariantCulture),
                "units=" + Units,
                "language=" + Language,
                "windowX=" + (WindowX?.ToString(CultureInfo.InvariantCulture) ?? string.Empty),
                "windowY=" + (WindowY?.ToString(CultureInfo.InvariantCulture) ?? string.Empty),
                "windowWidth=" + (WindowWidth?.ToString(CultureInfo.InvariantCulture) ?? string.Empty),
                "windowHeight=" + (WindowHeight?.ToString(CultureInfo.InvariantCulture) ?? string.Empty),
                "showDetails=" + (ShowDetails ? "1" : "0"),
                "reduceMotion=" + (ReduceMotion ? "1" : "0"),
                "showDiagnostics=" + (ShowDiagnostics ? "1" : "0"),
                "alwaysOnTop=" + (AlwaysOnTop ? "1" : "0"),
                "overlay=" + (Overlay ? "1" : "0"),
                "overlayOpacity=" + OverlayOpacity.ToString("0.00", CultureInfo.InvariantCulture),
                "overlayClickThrough=" + (OverlayClickThrough ? "1" : "0"),
                "layout=" + Layout
            });
        }
        catch
        {
            // La app debe seguir funcionando aunque no pueda guardar preferencias.
        }
    }

    private static bool ParseBool(string value) =>
        value == "1" || (bool.TryParse(value, out var parsed) && parsed);
}
