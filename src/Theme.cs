using System.Drawing;

namespace ATSTelemetry;

public enum ThemeMode
{
    Dark,
    Light,
    Contrast
}

/// <summary>
/// Tokens visuales únicos de la aplicación. Cuatro niveles de texto, tres
/// radios, un solo acento y una retícula base de 8 px. La paleta se puede
/// cambiar en caliente entre oscuro y alto contraste.
/// </summary>
internal static class Theme
{
    public static ThemeMode Mode { get; private set; } = ThemeMode.Dark;

    public static bool IsLight => Mode == ThemeMode.Light;

    /// <summary>Se incrementa al cambiar de paleta para invalidar cachés.</summary>
    public static int Revision { get; private set; }

    /// <summary>Escala tipográfica global: 1.0, 1.25 o 1.5.</summary>
    public static float UiScale { get; private set; } = 1f;

    // Superficies
    public static Color BackgroundTop { get; private set; } = Color.FromArgb(255, 10, 12, 15);
    public static Color BackgroundBottom { get; private set; } = Color.FromArgb(255, 7, 9, 11);
    public static Color Surface { get; private set; } = Color.FromArgb(255, 18, 22, 26);
    public static Color SurfaceRaised { get; private set; } = Color.FromArgb(255, 26, 31, 36);
    public static Color Stroke { get; private set; } = Color.FromArgb(18, 255, 255, 255);
    public static Color StrokeStrong { get; private set; } = Color.FromArgb(30, 255, 255, 255);

    // Texto: cuatro niveles, no dieciséis
    public static Color TextPrimary { get; private set; } = Color.FromArgb(255, 244, 247, 249);
    public static Color TextSecondary { get; private set; } = Color.FromArgb(255, 168, 178, 186);
    public static Color TextTertiary { get; private set; } = Color.FromArgb(255, 122, 133, 141);
    public static Color TextDisabled { get; private set; } = Color.FromArgb(255, 74, 82, 89);

    // Semánticos
    public static Color Accent { get; private set; } = Color.FromArgb(255, 64, 209, 255);
    public static Color Success { get; private set; } = Color.FromArgb(255, 61, 220, 151);
    public static Color Warning { get; private set; } = Color.FromArgb(255, 255, 176, 32);
    public static Color Notice { get; private set; } = Color.FromArgb(255, 255, 138, 61);
    public static Color Danger { get; private set; } = Color.FromArgb(255, 255, 77, 94);
    public static Color Info { get; private set; } = Color.FromArgb(255, 107, 173, 255);

    // Radios
    public const float RadiusChip = 12f;
    public const float RadiusCard = 16f;
    public const float RadiusPanel = 24f;

    // Retícula de 8 px
    public const float Space1 = 8f;
    public const float Space2 = 16f;
    public const float Space3 = 24f;
    public const float Space4 = 32f;

    public static void Apply(ThemeMode mode, float uiScale = 1f)
    {
        Mode = mode;
        UiScale = Math.Clamp(uiScale, 1f, 1.5f);

        if (mode == ThemeMode.Contrast)
        {
            BackgroundTop = Color.FromArgb(255, 0, 0, 0);
            BackgroundBottom = Color.FromArgb(255, 4, 5, 6);
            Surface = Color.FromArgb(255, 11, 15, 18);
            SurfaceRaised = Color.FromArgb(255, 22, 28, 33);
            Stroke = Color.FromArgb(52, 255, 255, 255);
            StrokeStrong = Color.FromArgb(88, 255, 255, 255);
            TextPrimary = Color.FromArgb(255, 255, 255, 255);
            TextSecondary = Color.FromArgb(255, 214, 222, 229);
            TextTertiary = Color.FromArgb(255, 167, 178, 186);
            TextDisabled = Color.FromArgb(255, 115, 127, 135);
            Accent = Color.FromArgb(255, 34, 224, 255);
            Success = Color.FromArgb(255, 0, 240, 160);
            Warning = Color.FromArgb(255, 255, 212, 0);
            Notice = Color.FromArgb(255, 255, 159, 67);
            Danger = Color.FromArgb(255, 255, 90, 106);
            Info = Color.FromArgb(255, 122, 184, 255);
        }
        else if (mode == ThemeMode.Light)
        {
            BackgroundTop = Color.FromArgb(255, 248, 250, 252);
            BackgroundBottom = Color.FromArgb(255, 232, 238, 245);
            Surface = Color.FromArgb(255, 255, 255, 255);
            SurfaceRaised = Color.FromArgb(255, 241, 245, 249);
            Stroke = Color.FromArgb(30, 15, 23, 42);
            StrokeStrong = Color.FromArgb(52, 15, 23, 42);
            TextPrimary = Color.FromArgb(255, 15, 23, 42);
            TextSecondary = Color.FromArgb(255, 71, 85, 105);
            TextTertiary = Color.FromArgb(255, 91, 107, 124);
            TextDisabled = Color.FromArgb(255, 148, 163, 184);
            Accent = Color.FromArgb(255, 0, 113, 143);
            Success = Color.FromArgb(255, 4, 120, 87);
            Warning = Color.FromArgb(255, 180, 83, 9);
            Notice = Color.FromArgb(255, 194, 65, 12);
            Danger = Color.FromArgb(255, 190, 24, 45);
            Info = Color.FromArgb(255, 29, 78, 216);
        }
        else
        {
            BackgroundTop = Color.FromArgb(255, 10, 12, 15);
            BackgroundBottom = Color.FromArgb(255, 7, 9, 11);
            Surface = Color.FromArgb(255, 18, 22, 26);
            SurfaceRaised = Color.FromArgb(255, 26, 31, 36);
            Stroke = Color.FromArgb(18, 255, 255, 255);
            StrokeStrong = Color.FromArgb(30, 255, 255, 255);
            TextPrimary = Color.FromArgb(255, 244, 247, 249);
            TextSecondary = Color.FromArgb(255, 168, 178, 186);
            TextTertiary = Color.FromArgb(255, 122, 133, 141);
            TextDisabled = Color.FromArgb(255, 74, 82, 89);
            Accent = Color.FromArgb(255, 64, 209, 255);
            Success = Color.FromArgb(255, 61, 220, 151);
            Warning = Color.FromArgb(255, 255, 176, 32);
            Notice = Color.FromArgb(255, 255, 138, 61);
            Danger = Color.FromArgb(255, 255, 77, 94);
            Info = Color.FromArgb(255, 107, 173, 255);
        }

        Revision++;
    }

    public static Color Alpha(Color color, float alpha) =>
        Color.FromArgb(
            (int)Math.Clamp(alpha * 255f, 0f, 255f),
            color.R,
            color.G,
            color.B);
}