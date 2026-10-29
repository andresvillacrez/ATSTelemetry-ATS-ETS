using System.IO;

namespace ATSTelemetry;

/// <summary>
/// Preferencias de ventana por usuario. Se guardan fuera de la carpeta de la
/// app para que la carpeta portátil pueda estar en un medio de solo lectura.
/// </summary>
internal sealed class WindowPreferences
{
    private static readonly string Folder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ATSTelemetry");

    private static readonly string FilePath = Path.Combine(Folder, "window.ini");

    public string? ScreenDevice { get; set; }

    public bool Windowed { get; set; }

    public WeatherCondition Weather { get; set; } = WeatherCondition.Clear;

    /// <summary>
    /// El clima automático es el modo por defecto. El menú de la bandeja
    /// permite desactivarlo y fijar uno de los cuatro estados manuales.
    /// </summary>
    public bool WeatherAuto { get; set; } = true;

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
                    preferences.Windowed = value == "1"
                        || (bool.TryParse(value, out var windowed) && windowed);
                }
                else if (key.Equals("weather", StringComparison.OrdinalIgnoreCase)
                         && Enum.TryParse<WeatherCondition>(value, true, out var weather))
                {
                    preferences.Weather = weather;
                }
                else if (key.Equals("weatherAuto", StringComparison.OrdinalIgnoreCase))
                {
                    preferences.WeatherAuto = value == "1"
                        || (bool.TryParse(value, out var weatherAuto) && weatherAuto);
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
                "weather=" + Weather,
                "weatherAuto=" + (WeatherAuto ? "1" : "0")
            });
        }
        catch
        {
            // La app debe seguir funcionando aunque no pueda guardar preferencias.
        }
    }
}
