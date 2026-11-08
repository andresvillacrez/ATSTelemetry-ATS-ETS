using System.Drawing;

namespace ATSTelemetry;

public enum LayoutMode
{
    Auto,
    Full,
    Compact,
    Minimal,
    Portrait
}

/// <summary>
/// Métricas de composición para cada tamaño de ventana. Full conserva el
/// canvas de referencia de 1920 × 1080; Compact y Minimal usan canvas propios
/// para no encoger la tipografía.
/// </summary>
internal sealed class DashboardLayout
{
    public LayoutMode Mode { get; init; }
    public float CanvasWidth { get; init; }
    public float CanvasHeight { get; init; }

    public RectangleF Header { get; init; }
    public RectangleF Indicators { get; init; }
    public RectangleF Finance { get; init; }
    public RectangleF Cargo { get; init; }
    public RectangleF Damage { get; init; }
    public RectangleF Gps { get; init; }
    public RectangleF Fuel { get; init; }
    public RectangleF Notices { get; init; }
    public RectangleF Weather { get; init; }
    public RectangleF Trip { get; init; }
    public RectangleF Water { get; init; }
    public RectangleF Air { get; init; }
    public RectangleF Footer { get; init; }
    public PointF ClusterCenter { get; init; }
    public float ClusterRadius { get; init; }

    public bool ShowVerticalGauges { get; init; }
    public bool ShowDetailRows { get; init; }
    public bool ShowBottomCards { get; init; }

    public static LayoutMode ForSize(int width, int height, float uiScale = 1f)
    {
        // A mayor escala de UI, el espacio efectivo es menor: se reorganiza
        // el layout para evitar solapamientos y desbordes de texto.
        var scale = uiScale <= 0 ? 1f : uiScale;
        var effectiveWidth = (int)(width / scale);
        var effectiveHeight = (int)(height / scale);

        if (effectiveWidth >= 1700 && effectiveHeight >= 950)
        {
            return LayoutMode.Full;
        }

        if (effectiveWidth >= 1152 && effectiveHeight >= 648)
        {
            return LayoutMode.Compact;
        }

        return LayoutMode.Minimal;
    }

    public static DashboardLayout Create(LayoutMode mode) => mode switch
    {
        LayoutMode.Compact => Compact(),
        LayoutMode.Minimal => Minimal(),
        LayoutMode.Portrait => Portrait(),
        _ => Full()
    };

    /// <summary>
    /// Variante Full para relaciones distintas de 16:9. El canvas crece en el
    /// eje que sobra (ultrawide o 16:10) y los paneles se anclan a los bordes;
    /// el cluster permanece centrado. Así no se generan barras negras ni se
    /// encoge la tipografía.
    /// </summary>
    public static DashboardLayout CreateFull(float canvasWidth, float canvasHeight)
    {
        const float margin = 64f;
        const float sideWidth = 400f;
        const float headerHeight = 92f;
        const float footerHeight = 48f;

        var centerX = canvasWidth / 2f;
        var centerY = canvasHeight / 2f + 8f;
        var footerY = canvasHeight - footerHeight - 48f;
        var bottomY = footerY - 190f;
        var cargoY = 512f;
        var cargoHeight = Math.Max(240f, footerY - 48f - cargoY);

        return new DashboardLayout
        {
            Mode = LayoutMode.Full,
            CanvasWidth = canvasWidth,
            CanvasHeight = canvasHeight,
            Header = new RectangleF(0f, 0f, canvasWidth, headerHeight),
            Indicators = new RectangleF(0f, 108f, canvasWidth, 58f),
            Finance = new RectangleF(margin, 116f, sideWidth, 380f),
            Cargo = new RectangleF(margin, cargoY, sideWidth, cargoHeight),
            Damage = new RectangleF(canvasWidth - margin - sideWidth, 116f, sideWidth, 380f),
            Gps = new RectangleF(canvasWidth - margin - sideWidth, cargoY, sideWidth, cargoHeight),
            Fuel = new RectangleF(centerX - 474f, bottomY, 240f, 142f),
            Notices = new RectangleF(centerX - 218f, bottomY, 438f, 142f),
            Weather = new RectangleF(centerX + 236f, bottomY, 237f, 142f),
            Trip = RectangleF.Empty,
            Water = new RectangleF(centerX - 474f, centerY - 248f, 88f, 440f),
            Air = new RectangleF(centerX + 386f, centerY - 248f, 88f, 440f),
            Footer = new RectangleF(0f, footerY, canvasWidth, footerHeight),
            ClusterCenter = new PointF(centerX, centerY),
            ClusterRadius = 302f,
            ShowVerticalGauges = true,
            ShowDetailRows = true,
            ShowBottomCards = true
        };
    }

    private static DashboardLayout Full() => new()
    {
        Mode = LayoutMode.Full,
        CanvasWidth = 1920f,
        CanvasHeight = 1080f,
        Header = new RectangleF(0f, 0f, 1920f, 92f),
        Indicators = new RectangleF(0f, 108f, 1920f, 58f),
        Finance = new RectangleF(64f, 116f, 400f, 380f),
        Cargo = new RectangleF(64f, 512f, 400f, 424f),
        Damage = new RectangleF(1456f, 116f, 400f, 380f),
        Gps = new RectangleF(1456f, 512f, 400f, 424f),
        Fuel = new RectangleF(486f, 794f, 240f, 142f),
        Notices = new RectangleF(742f, 794f, 438f, 142f),
        Weather = new RectangleF(1196f, 794f, 237f, 142f),
        Trip = RectangleF.Empty,
        Water = new RectangleF(486f, 300f, 88f, 440f),
        Air = new RectangleF(1346f, 300f, 88f, 440f),
        Footer = new RectangleF(0f, 984f, 1920f, 48f),
        ClusterCenter = new PointF(960f, 548f),
        ClusterRadius = 302f,
        ShowVerticalGauges = true,
        ShowDetailRows = true,
        ShowBottomCards = true
    };

    private static DashboardLayout Compact() => new()
    {
        Mode = LayoutMode.Compact,
        CanvasWidth = 1280f,
        CanvasHeight = 720f,
        Header = new RectangleF(0f, 0f, 1280f, 60f),
        Indicators = new RectangleF(340f, 74f, 600f, 42f),
        Finance = new RectangleF(24f, 192f, 300f, 200f),
        Cargo = new RectangleF(24f, 404f, 300f, 280f),
        Damage = new RectangleF(956f, 176f, 300f, 196f),
        Gps = new RectangleF(956f, 384f, 300f, 300f),
        Fuel = new RectangleF(24f, 76f, 300f, 104f),
        Notices = new RectangleF(340f, 600f, 600f, 84f),
        Weather = new RectangleF(956f, 76f, 300f, 88f),
        Trip = RectangleF.Empty,
        Water = RectangleF.Empty,
        Air = RectangleF.Empty,
        Footer = new RectangleF(0f, 696f, 1280f, 24f),
        ClusterCenter = new PointF(640f, 360f),
        ClusterRadius = 175f,
        ShowVerticalGauges = false,
        ShowDetailRows = false,
        ShowBottomCards = false
    };

    private static DashboardLayout Minimal() => new()
    {
        Mode = LayoutMode.Minimal,
        CanvasWidth = 960f,
        CanvasHeight = 540f,
        Header = new RectangleF(0f, 0f, 960f, 52f),
        Indicators = new RectangleF(240f, 58f, 480f, 36f),
        Finance = RectangleF.Empty,
        Cargo = RectangleF.Empty,
        Damage = RectangleF.Empty,
        Gps = RectangleF.Empty,
        Fuel = new RectangleF(16f, 108f, 200f, 96f),
        Notices = new RectangleF(16f, 492f, 928f, 24f),
        Weather = RectangleF.Empty,
        Trip = new RectangleF(744f, 108f, 200f, 120f),
        Water = RectangleF.Empty,
        Air = RectangleF.Empty,
        Footer = new RectangleF(0f, 516f, 960f, 24f),
        ClusterCenter = new PointF(480f, 280f),
        ClusterRadius = 148f,
        ShowVerticalGauges = false,
        ShowDetailRows = false,
        ShowBottomCards = false
    };

    /// <summary>
    /// Layout para pantallas verticales (1080 x 1920). Los paneles se
    /// reorganizan en dos columnas superiores y una fila inferior; el cluster
    /// queda centrado en la parte superior-media.
    /// </summary>
    private static DashboardLayout Portrait() => new()
    {
        Mode = LayoutMode.Portrait,
        CanvasWidth = 1080f,
        CanvasHeight = 1920f,
        Header = new RectangleF(0f, 0f, 1080f, 80f),
        Indicators = new RectangleF(0f, 92f, 1080f, 52f),
        // Velocimetro + indicadores de temperatura/presion (centrados) entre
        // los testigos superiores y los paneles de combustible/avisos/clima
        ClusterCenter = new PointF(540f, 520f),
        ClusterRadius = 280f,
        Water = new RectangleF(40f, 300f, 80f, 380f),
        Air = new RectangleF(960f, 300f, 80f, 380f),
        // Fila de combustible / avisos / clima
        Fuel = new RectangleF(40f, 820f, 320f, 130f),
        Notices = new RectangleF(380f, 820f, 360f, 130f),
        Weather = new RectangleF(760f, 820f, 280f, 130f),
        // Paneles laterales (empresa, carga, danos, gps) debajo, con altura suficiente
        Finance = new RectangleF(40f, 970f, 480f, 380f),
        Damage = new RectangleF(560f, 970f, 480f, 380f),
        Cargo = new RectangleF(40f, 1370f, 480f, 424f),
        Gps = new RectangleF(560f, 1370f, 480f, 424f),
        Trip = RectangleF.Empty,
        Footer = new RectangleF(0f, 1872f, 1080f, 48f),
        ShowVerticalGauges = true,
        ShowDetailRows = true,
        ShowBottomCards = true
    };
}
