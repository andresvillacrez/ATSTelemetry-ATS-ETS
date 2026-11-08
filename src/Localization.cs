using System.Collections.Generic;
using System.Globalization;

namespace ATSTelemetry;

/// <summary>
/// Idioma de la interfaz. Se persiste en window.ini como "language=es|en".
/// </summary>
public enum AppLanguage
{
    Es,
    En
}

/// <summary>
/// Tabla de cadenas de la interfaz. Permite cambiar de idioma en caliente
/// sin recompilar. Las claves no traducidas caen al español.
/// </summary>
internal static class Localization
{
    public static AppLanguage Language { get; set; } = AppLanguage.Es;

    private static readonly Dictionary<string, (string es, string en)> Strings = new()
    {
        // Panel headers
        ["FUEL"] = ("COMBUSTIBLE", "FUEL"),
        ["NOTICES"] = ("AVISOS", "NOTICES"),
        ["WEATHER"] = ("CLIMA", "WEATHER"),
        ["COMPANY"] = ("EMPRESA", "COMPANY"),
        ["CARGO"] = ("CARGA", "CARGO"),
        ["DAMAGE"] = ("DAÑOS", "DAMAGE"),
        ["GPS"] = ("GPS", "GPS"),
        // Status
        ["NO_DATA"] = ("SIN DATOS", "NO DATA"),
        ["NO_SIGNAL"] = ("SIN SEÑAL", "NO SIGNAL"),
        ["LIVE"] = ("EN VIVO", "LIVE"),
        ["DEMO"] = ("DEMO", "DEMO"),
        ["PAUSED"] = ("PAUSADO", "PAUSED"),
        ["ACTIVE"] = ("ACTIVO", "ACTIVE"),
        ["LEVEL"] = ("NIVEL", "LEVEL"),
        ["RESERVE"] = ("RESERVA", "RESERVE"),
        ["NO_CARGO"] = ("SIN CARGA", "NO CARGO"),
        ["NO_DESTINATION"] = ("SIN DESTINO", "NO DESTINATION"),
        ["NO_ORIGIN"] = ("SIN ORIGEN", "NO ORIGIN"),
        ["NO_ROUTE"] = ("SIN RUTA", "NO ROUTE"),
        ["NO_ALERTS"] = ("SIN AVISOS", "NO ALERTS"),
        ["NO_JOB"] = ("SIN TRABAJO", "NO JOB"),
        ["NO_IMAGE"] = ("Sin imagen del modelo", "No model image"),
        // Units
        ["AUT"] = ("AUT.", "RANGE"),
        ["CONS"] = ("CONS.", "CONS."),
        ["ODOMETER"] = ("ODÓMETRO", "ODOMETER"),
        ["ARRIVAL"] = ("LLEGADA", "ARRIVAL"),
        ["ETA"] = ("HORA PREVISTA", "ETA"),
        ["PROGRESS"] = ("PROGRESO DE RUTA", "ROUTE PROGRESS"),
        ["POSITION"] = ("POSICIÓN", "POSITION"),
        ["REMAINING"] = ("FALTAN", "REMAINING"),
        ["TRAVELED"] = ("RECORRIDO", "TRAVELED"),
        // Settings
        ["SETTINGS"] = ("Ajustes", "Settings"),
        ["LANGUAGE"] = ("Idioma", "Language"),
        ["SCALE"] = ("Escala", "Scale"),
        ["SPANISH"] = ("Español", "Spanish"),
        ["ENGLISH"] = ("Inglés", "English"),
        ["THEME"] = ("Tema", "Theme"),
        ["THEME_DARK"] = ("Oscuro", "Dark"),
        ["THEME_LIGHT"] = ("Claro", "Light"),
        ["THEME_CONTRAST"] = ("Contraste", "Contrast"),
        ["UNITS"] = ("Unidades", "Units"),
        ["METRIC"] = ("Métrico", "Metric"),
        ["IMPERIAL"] = ("Imperial", "Imperial"),
        ["LAYOUT"] = ("Diseño", "Layout"),
        ["REDUCE_MOTION"] = ("Reducir movimiento", "Reduce motion"),
        ["DIAGNOSTICS"] = ("Diagnóstico", "Diagnostics"),
        ["ALWAYS_ON_TOP"] = ("Siempre visible", "Always on top"),
        ["OVERLAY"] = ("Superposición", "Overlay"),
        ["CLICK_THROUGH"] = ("Clic a través", "Click through"),
        ["WINDOWED"] = ("Modo ventana", "Windowed mode"),
        ["LAYOUT"] = ("Diseño", "Layout"),
        ["ALWAYS_ON_TOP"] = ("Siempre visible", "Always on top"),
        ["CREDITS"] = ("Créditos", "Credits"),
        ["EXIT"] = ("Salir", "Exit"),
        ["SCREEN"] = ("Pantalla", "Screen"),
        // Tray
        ["SCREEN"] = ("Pantalla", "Screen"),
        ["WINDOWED"] = ("Modo ventana", "Windowed mode"),
        ["CREDITS"] = ("Créditos", "Credits"),
        ["EXIT"] = ("Salir", "Exit"),
    };

    public static string L(string key)
    {
        if (Strings.TryGetValue(key, out var pair))
        {
            return Language == AppLanguage.En ? pair.en : pair.es;
        }
        return key;
    }
}
