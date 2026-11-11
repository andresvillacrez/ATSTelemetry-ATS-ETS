using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using SkiaSharp;
using ColorMatrix = ATSTelemetry.SkiaCompat.SkiaColorMatrix;
using Font = ATSTelemetry.SkiaCompat.SkiaFont;
using Graphics = ATSTelemetry.SkiaCompat.SkiaGraphics;
using GraphicsPath = ATSTelemetry.SkiaCompat.SkiaGraphicsPath;
using Image = ATSTelemetry.SkiaCompat.SkiaImage;
using ImageAttributes = ATSTelemetry.SkiaCompat.SkiaImageAttributes;
using LinearGradientBrush = ATSTelemetry.SkiaCompat.SkiaLinearGradientBrush;
using PathGradientBrush = ATSTelemetry.SkiaCompat.SkiaPathGradientBrush;
using Pen = ATSTelemetry.SkiaCompat.SkiaPen;
using SolidBrush = ATSTelemetry.SkiaCompat.SkiaSolidBrush;
using TextFormat = ATSTelemetry.SkiaCompat.TextFormat;

namespace ATSTelemetry;

/// <summary>
/// Full-screen 1920x1080 dashboard rendered with SkiaSharp. The virtual canvas
/// is scaled and centered to any monitor resolution, preserving the layout.
/// </summary>
public sealed partial class DashboardRenderer
{
    internal enum NoticeKind { Default, Income, Expense, Route }
    private const float InactiveIndicatorOpacity = 0.20f;
    private const float BrandLogoHeight = 40f;
    private const float BrandLogoGap = 16f;

    // Secuencia de encendido: los testigos hacen un chequeo breve y las agujas
    // barren hasta el máximo antes de volver a la lectura real.
    private const double IgnitionSweepUpSeconds = 0.90;
    private const double IgnitionSweepHoldSeconds = 0.15;
    private const double IgnitionSweepDownSeconds = 1.05;
    private const double IgnitionBulbSeconds = 1.25;
    private const double IgnitionTotalSeconds =
        IgnitionSweepUpSeconds + IgnitionSweepHoldSeconds + IgnitionSweepDownSeconds;

    private bool _ignitionInitialized;
    private bool _ignitionWasOn;
    private DateTime _ignitionStartedUtc;

    // Fundido cruzado de 0,5 s al cambiar de modelo de camión.
    private const double TruckImageFadeSeconds = 0.5;
    private string? _truckImageKey;
    private Image? _truckImageCurrent;
    private Image? _truckImagePrevious;
    private DateTime _truckImageFadeStartedUtc = DateTime.MinValue;

    private string? _cargoImageKey;
    private Image? _cargoImageCurrent;
    private Image? _cargoImagePrevious;
    private DateTime _cargoImageFadeStartedUtc = DateTime.MinValue;

    // Avisos del juego en el panel central: 2,5 s visibles y 0,5 s de fundido.
    private const double MessageHoldSeconds = 2.5;
    private const double MessageFadeSeconds = 0.5;
    private const double MessageTotalSeconds = MessageHoldSeconds + MessageFadeSeconds;
    private const int MessageQueueLimit = 6;

    private static readonly string[] DemoMessages =
    {
        "INGRESO POR ENTREGA +$12.450",
        "MULTA DE TRÁFICO -$450",
        "PEAJE DE AUTOPISTA -$125",
        "TRABAJO COMPLETADO +$8.900",
        "REPOSTAJE REALIZADO -$1.240"
    };

    private readonly Queue<(string Text, NoticeKind Kind)> _messageQueue = new();
    private (string Text, NoticeKind Kind)? _currentMessage;
    private DateTime _messageStartedUtc;
    private float _messageOpacity;
    private bool _messageDemoMode;
    private int _demoMessageIndex;
    private DateTime _demoNextMessageUtc = DateTime.MinValue;
    private bool _messageBaselineReady;
    private long? _prevBankBalance;
    private long? _prevDeliveredRevenue;
    private long? _prevCancelledPenalty;
    private long? _prevFineAmount;
    private long? _prevTollgatePay;
    private long? _prevFerryPay;
    private long? _prevTrainPay;
    private ulong? _prevJobIncome;
    private string? _prevCityDst;

    private static readonly CultureInfo Es = CultureInfo.GetCultureInfo("es-ES");

    private static Color Green => Theme.Success;
    private static Color Mint => Theme.IsLight ? Color.FromArgb(0, 150, 110) : Color.FromArgb(120, 255, 212);
    private static Color Cyan => Theme.Accent;
    private static Color Yellow => Theme.Warning;
    private static Color Orange => Theme.Notice;
    private static Color Red => Theme.Danger;
    private static Color Blue => Theme.Info;
    private static Color MoonLight => Theme.IsLight ? Color.FromArgb(55, 85, 130) : Color.FromArgb(204, 224, 255);

    private static readonly TextFormat Center = TextFormat.Center;
    private static readonly TextFormat Left = TextFormat.Left;
    private static readonly TextFormat Right = TextFormat.Right;
    private static readonly TextFormat LeftTop = TextFormat.LeftTop;
    private static readonly TextFormat Typographic = TextFormat.Typographic;

    private readonly TelemetryReader _reader;
    private readonly Dictionary<string, Image?> _icons = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Font> _fonts = new(StringComparer.OrdinalIgnoreCase);


    private DateTime? _lastSyncUtc;
    private DateTime _lastFrameUtc = DateTime.UtcNow;
    private readonly Dictionary<string, AnimatedValue> _smoothTargets = new(StringComparer.Ordinal);
    private double _frameDt;
    private SKPicture? _backdropLayer;
    private float _backdropWidth;
    private float _backdropHeight;
    private int _backdropRevision = -1;
    private SKPicture? _panelLayer;
    private float _panelLayerWidth;
    private float _panelLayerHeight;
    private int _panelLayerRevision = -1;
    private LayoutMode _panelLayerMode = LayoutMode.Auto;
    private bool _usingPanelLayer;

    public DashboardRenderer(TelemetryReader reader)
    {
        _reader = reader;
    }

    /// <summary>
    /// Nombre del monitor. Se conserva por compatibilidad con la ventana, pero
    /// ya no se dibuja: la cabecera muestra estado útil (juego, perfil y sync).
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Revela las filas secundarias de los paneles. Desactivado por defecto para
    /// mantener tres datos primarios por panel.
    /// </summary>
    public bool ShowDetails { get; set; }

    /// <summary>
    /// Desactiva las animaciones de aguja y de valores. Se aplica también a los
    /// indicadores pulsantes.
    /// </summary>
    public bool ReduceMotion { get; set; }

    public LayoutMode LayoutMode { get; set; } = LayoutMode.Auto;

    /// <summary>Sistema de unidades activo para velocidad, temperatura, presión y volumen.</summary>
    public UnitSystem Units { get; set; } = UnitSystem.Metric;

    private bool Imperial => Units == UnitSystem.Imperial;
    private double SpeedMax => Imperial ? 112.0 : 180.0;
    private string SpeedUnit => Imperial ? "mph" : "km/h";
    private string TemperatureUnit => Imperial ? "°F" : "°C";
    private string PressureUnit => Imperial ? "PSI" : "bar";
    private string FuelVolumeUnit => Imperial ? "GAL" : "L";
    private double ToDisplaySpeed(double kmh) => Imperial ? kmh * 0.621371192 : kmh;
    private double ToDisplayTemperature(double celsius) => Imperial ? celsius * 9.0 / 5.0 + 32.0 : celsius;
    private double ToDisplayPressure(double psi) => Imperial ? psi : psi / 14.5037738;
    private double ToDisplayVolume(double liters) => Imperial ? liters * 0.2641720524 : liters;
    private double ToDisplayDistance(double km) => Imperial ? km * 0.621371192 : km;

    /// <summary>
    /// Overlay opcional con FPS, tiempo de dibujo y memoria GC. Desactivado por
    /// defecto para no añadir coste ni ruido visual.
    /// </summary>
    public bool ShowDiagnostics { get; set; }

    private readonly Stopwatch _frameWatch = new();
    private double _drawMs;
    private double _frameIntervalMs;

    public enum DashboardAction
    {
        None,
        Settings,
        Credits
    }

    private static readonly RectangleF SettingsButton = new(16f, 10f, 96f, 26f);
    private static readonly RectangleF CreditsButton = new(120f, 10f, 96f, 26f);

    private DashboardLayout ResolveLayout(
        int width, int height,
        out float scale, out float offsetX, out float offsetY)
    {
        var aspect = width / (float)height;
        var portrait = aspect < 1f;
        // En orientación vertical siempre se usa el layout Portrait, aunque el
        // usuario haya fijado otro modo, para evitar reescalados incorrectos.
        var mode = portrait
            ? LayoutMode.Portrait
            : (LayoutMode == LayoutMode.Auto
                ? DashboardLayout.ForSize(width, height, Theme.UiScale)
                : LayoutMode);
        var layout = mode == LayoutMode.Portrait
            ? DashboardLayout.Create(LayoutMode.Portrait)
            : (mode == LayoutMode.Full && (aspect > 1.85f || aspect < 1.72f)
                ? DashboardLayout.CreateFull(
                    aspect > 1.85f ? 1080f * aspect : 1920f,
                    aspect > 1.85f ? 1080f : 1920f / aspect)
                : DashboardLayout.Create(mode));

        scale = Math.Min(width / layout.CanvasWidth, height / layout.CanvasHeight);
        offsetX = (width - layout.CanvasWidth * scale) / 2f;
        offsetY = (height - layout.CanvasHeight * scale) / 2f;
        return layout;
    }

    /// <summary>
    /// Traduce un clic del control a una acción del panel. Solo los layouts
    /// Full muestran los botones de cabecera.
    /// </summary>
    public DashboardAction HitTest(float x, float y, int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            return DashboardAction.None;
        }

        var layout = ResolveLayout(width, height, out var scale, out var offsetX, out var offsetY);
        if (scale <= 0)
        {
            return DashboardAction.None;
        }

        // Los botones de cabecera (Ajustes/Créditos) solo existen en Full y Portrait.
        if (layout.Mode != LayoutMode.Full && layout.Mode != LayoutMode.Portrait)
        {
            return DashboardAction.None;
        }

        var vx = (x - offsetX) / scale;
        var vy = (y - offsetY) / scale;

        if (SettingsButton.Contains(vx, vy))
        {
            return DashboardAction.Settings;
        }

        return CreditsButton.Contains(vx, vy) ? DashboardAction.Credits : DashboardAction.None;
    }

    public void Render(SKCanvas canvas, int width, int height)
    {
        canvas.Clear(SKColors.Black);

        if (width <= 0 || height <= 0)
        {
            return;
        }

        _frameWatch.Restart();
        var layout = ResolveLayout(width, height, out var scale, out var offsetX, out var offsetY);

        var state = canvas.Save();
        canvas.Translate(offsetX, offsetY);
        canvas.Scale(scale, scale);

        EnsureBackdrop(layout.CanvasWidth, layout.CanvasHeight);
        if (_backdropLayer != null)
        {
            canvas.DrawPicture(_backdropLayer);
        }

        EnsurePanelLayer(layout);
        if (_panelLayer != null)
        {
            canvas.DrawPicture(_panelLayer);
        }

        var g = new Graphics(canvas);
        var snap = _reader.Snapshot();
        var now = DateTime.UtcNow;

        var dt = Math.Clamp((now - _lastFrameUtc).TotalSeconds, 0.0, 0.05);
        _lastFrameUtc = now;

        _frameIntervalMs = _frameIntervalMs <= 0
            ? dt * 1000.0
            : _frameIntervalMs * 0.90 + dt * 1000.0 * 0.10;

        _frameDt = dt;

        if (snap.Active)
        {
            _lastSyncUtc = now;
        }

        var ignitionOn = snap.Active && snap.Engine;
        var ignitionElapsed = UpdateIgnitionElapsed(ignitionOn, now);
        var bulbCheck = ignitionOn && ignitionElapsed < IgnitionBulbSeconds;

        // Con movimiento reducido los valores se dibujan en su posición final:
        // se conserva el chequeo de testigos, pero no el barrido ni el pulso.
        var animationElapsed = ReduceMotion ? double.PositiveInfinity : ignitionElapsed;
        UpdateGameMessages(snap, now);

        _usingPanelLayer = true;
        switch (layout.Mode)
        {
            case LayoutMode.Compact:
                DrawCompactDashboard(g, snap, now, ignitionOn, bulbCheck, animationElapsed, layout);
                break;
            case LayoutMode.Minimal:
                DrawMinimalDashboard(g, snap, now, ignitionOn, bulbCheck, animationElapsed, layout);
                break;
            default:
                DrawDashboard(g, snap, now, ignitionOn, bulbCheck, animationElapsed, layout);
                break;
        }
        _usingPanelLayer = false;

        if (ShowDiagnostics)
        {
            DrawDiagnostics(g, layout);
        }

        _frameWatch.Stop();
        var drawMs = _frameWatch.Elapsed.TotalMilliseconds;
        _drawMs = _drawMs <= 0 ? drawMs : _drawMs * 0.90 + drawMs * 0.10;

        canvas.RestoreToCount(state);
    }

    private void DrawDiagnostics(Graphics g, DashboardLayout layout)
    {
        var fps = _frameIntervalMs > 0 ? 1000.0 / _frameIntervalMs : 0;
        var gcMb = GC.GetTotalMemory(false) / (1024.0 * 1024.0);
        var text = $"FPS {fps:0}  ·  FRAME {_drawMs:0.0} ms  ·  GC {gcMb:0.0} MB";
        const float width = 360f;
        var x = layout.CanvasWidth / 2f - width / 2f;
        var y = layout.Footer.Y - 30f;

        DrawSquircle(g, new RectangleF(x, y, width, 22f), 8f,
            Color.FromArgb(150, 8, 10, 12), Theme.Stroke, 1f);
        DrawText(g, text, Mono(TypeScale.Caption, FontStyle.Bold), Theme.TextTertiary,
            new RectangleF(x, y + 3f, width, 16f), Center);
    }

    private double UpdateIgnitionElapsed(bool on, DateTime now)
    {
        if (!_ignitionInitialized)
        {
            _ignitionInitialized = true;
            _ignitionWasOn = on;
            if (on)
            {
                _ignitionStartedUtc = now;
            }
        }
        else if (on && !_ignitionWasOn)
        {
            _ignitionWasOn = true;
            _ignitionStartedUtc = now;
        }
        else if (!on)
        {
            _ignitionWasOn = false;
        }

        return on ? (now - _ignitionStartedUtc).TotalSeconds : double.PositiveInfinity;
    }

    private static double SmoothStep(double value)
    {
        var t = Math.Clamp(value, 0d, 1d);
        return t * t * (3d - 2d * t);
    }

    private void EnsureBackdrop(float width, float height)
    {
        if (_backdropLayer != null
            && Math.Abs(_backdropWidth - width) < 0.5f
            && Math.Abs(_backdropHeight - height) < 0.5f
            && _backdropRevision == Theme.Revision)
        {
            return;
        }

        using var recorder = new SKPictureRecorder();
        var recordingCanvas = recorder.BeginRecording(new SKRect(0f, 0f, width, height));
        var recordingGraphics = new Graphics(recordingCanvas);
        DrawFh6Backdrop(recordingGraphics, width, height);

        _backdropLayer?.Dispose();
        _backdropLayer = recorder.EndRecording();
        _backdropWidth = width;
        _backdropHeight = height;
        _backdropRevision = Theme.Revision;
    }

    private void EnsurePanelLayer(DashboardLayout layout)
    {
        if (_panelLayer != null
            && _panelLayerMode == layout.Mode
            && Math.Abs(_panelLayerWidth - layout.CanvasWidth) < 0.5f
            && Math.Abs(_panelLayerHeight - layout.CanvasHeight) < 0.5f
            && _panelLayerRevision == Theme.Revision)
        {
            return;
        }

        using var recorder = new SKPictureRecorder();
        var recordingCanvas = recorder.BeginRecording(
            new SKRect(0f, 0f, layout.CanvasWidth, layout.CanvasHeight));
        var g = new Graphics(recordingCanvas);

        DrawPanelFrames(g, layout);

        _panelLayer?.Dispose();
        _panelLayer = recorder.EndRecording();
        _panelLayerWidth = layout.CanvasWidth;
        _panelLayerHeight = layout.CanvasHeight;
        _panelLayerRevision = Theme.Revision;
        _panelLayerMode = layout.Mode;
    }

    private static void DrawPanelFrames(Graphics g, DashboardLayout layout)
    {
        switch (layout.Mode)
        {
            case LayoutMode.Compact:
                DrawGlassPanelCore(g, layout.Fuel.X, layout.Fuel.Y,
                    layout.Fuel.Width, layout.Fuel.Height, Theme.RadiusCard);
                DrawGlassPanelCore(g, layout.Weather.X, layout.Weather.Y,
                    layout.Weather.Width, layout.Weather.Height, Theme.RadiusCard);
                DrawGlassPanelCore(g, layout.Finance.X, layout.Finance.Y,
                    layout.Finance.Width, layout.Finance.Height, Theme.RadiusCard);
                DrawGlassPanelCore(g, layout.Damage.X, layout.Damage.Y,
                    layout.Damage.Width, layout.Damage.Height, Theme.RadiusCard);
                DrawGlassPanelCore(g, layout.Cargo.X, layout.Cargo.Y,
                    layout.Cargo.Width, layout.Cargo.Height, Theme.RadiusCard);
                DrawGlassPanelCore(g, layout.Gps.X, layout.Gps.Y,
                    layout.Gps.Width, layout.Gps.Height, Theme.RadiusCard);
                DrawGlassPanelCore(g, layout.Notices.X, layout.Notices.Y,
                    layout.Notices.Width, layout.Notices.Height, Theme.RadiusCard);
                break;

            case LayoutMode.Minimal:
                DrawGlassPanelCore(g, layout.Fuel.X, layout.Fuel.Y,
                    layout.Fuel.Width, layout.Fuel.Height, Theme.RadiusCard);
                DrawGlassPanelCore(g, layout.Trip.X, layout.Trip.Y,
                    layout.Trip.Width, layout.Trip.Height, Theme.RadiusCard);
                DrawGlassPanelCore(g, layout.Notices.X, layout.Notices.Y,
                    layout.Notices.Width, layout.Notices.Height, Theme.RadiusCard);
                break;

            default:
                DrawGlassPanelCore(g, layout.Finance.X, layout.Finance.Y,
                    layout.Finance.Width, layout.Finance.Height, Theme.RadiusPanel);
                DrawGlassPanelCore(g, layout.Cargo.X, layout.Cargo.Y,
                    layout.Cargo.Width, layout.Cargo.Height, Theme.RadiusPanel);
                DrawGlassPanelCore(g, layout.Damage.X, layout.Damage.Y,
                    layout.Damage.Width, layout.Damage.Height, Theme.RadiusPanel);
                DrawGlassPanelCore(g, layout.Gps.X, layout.Gps.Y,
                    layout.Gps.Width, layout.Gps.Height, Theme.RadiusPanel);
                DrawGlassPanelCore(g, layout.Fuel.X, layout.Fuel.Y,
                    layout.Fuel.Width, layout.Fuel.Height, Theme.RadiusCard);
                DrawGlassPanelCore(g, layout.Notices.X, layout.Notices.Y,
                    layout.Notices.Width, layout.Notices.Height, Theme.RadiusCard);
                DrawGlassPanelCore(g, layout.Weather.X, layout.Weather.Y,
                    layout.Weather.Width, layout.Weather.Height, Theme.RadiusCard);
                if (layout.ShowVerticalGauges)
                {
                    DrawGlassPanelCore(g, layout.Water.X, layout.Water.Y,
                        layout.Water.Width, layout.Water.Height, 22f);
                    DrawGlassPanelCore(g, layout.Air.X, layout.Air.Y,
                        layout.Air.Width, layout.Air.Height, 22f);
                }
                break;
        }
    }

    private double SmoothTarget(string key, double target)
    {
        if (!_smoothTargets.TryGetValue(key, out var value))
        {
            value = new AnimatedValue();
            _smoothTargets[key] = value;
        }

        if (ReduceMotion)
        {
            value.Snap(target);
            return target;
        }

        value.Update(target, _frameDt);
        return value.Value;
    }

    private static double AnimatedFraction(double target, double elapsed)
    {
        target = Math.Clamp(target, 0d, 1d);
        if (double.IsPositiveInfinity(elapsed) || elapsed >= IgnitionTotalSeconds)
        {
            return target;
        }

        if (elapsed <= 0d)
        {
            return 0d;
        }

        if (elapsed < IgnitionSweepUpSeconds)
        {
            return SmoothStep(elapsed / IgnitionSweepUpSeconds);
        }

        if (elapsed < IgnitionSweepUpSeconds + IgnitionSweepHoldSeconds)
        {
            return 1d;
        }

        var down = SmoothStep(
            (elapsed - IgnitionSweepUpSeconds - IgnitionSweepHoldSeconds)
            / IgnitionSweepDownSeconds);
        return 1d - down * (1d - target);
    }

    private void UpdateGameMessages(Snap snap, DateTime now)
    {
        if (snap.Demo)
        {
            if (!_messageDemoMode)
            {
                _messageDemoMode = true;
                _demoNextMessageUtc = now;
            }

            if (now >= _demoNextMessageUtc)
            {
                PushMessage(DemoMessages[_demoMessageIndex % DemoMessages.Length]);
                _demoMessageIndex++;
                _demoNextMessageUtc = now.AddSeconds(3.8);
            }
        }
        else
        {
            _messageDemoMode = false;
            if (snap.Active)
            {
                if (_messageBaselineReady)
                {
                    DetectGameMessages(snap);
                }
                else
                {
                    StoreMessageBaseline(snap);
                    _messageBaselineReady = true;
                }
            }
            else
            {
                _messageBaselineReady = false;
            }
        }

        if (_currentMessage != null
            && (now - _messageStartedUtc).TotalSeconds >= MessageTotalSeconds)
        {
            _currentMessage = null;
        }

        if (_currentMessage == null && _messageQueue.Count > 0)
        {
            _currentMessage = _messageQueue.Dequeue();
            _messageStartedUtc = now;
        }

        if (_currentMessage == null)
        {
            _messageOpacity = 0f;
            return;
        }

        var elapsed = (now - _messageStartedUtc).TotalSeconds;
        _messageOpacity = elapsed <= MessageHoldSeconds
            ? 1f
            : (float)Math.Clamp(1d - (elapsed - MessageHoldSeconds) / MessageFadeSeconds, 0d, 1d);
    }

    private void PushMessage(string text, NoticeKind kind = NoticeKind.Default)
    {
        if (string.IsNullOrWhiteSpace(text) || _messageQueue.Count >= MessageQueueLimit)
        {
            return;
        }

        if (kind == NoticeKind.Default)
        {
            kind = InferNoticeKind(text);
        }

        _messageQueue.Enqueue((text, kind));
    }

    private static NoticeKind InferNoticeKind(string text)
    {
        var t = text.ToUpperInvariant();
        if (t.Contains("INGRESO") || t.Contains("ENTREGA") || t.Contains("TRABAJO COMPLETADO") || t.Contains("NUEVO TRABAJO"))
        {
            return NoticeKind.Income;
        }
        if (t.Contains("MULTA") || t.Contains("PEAJE") || t.Contains("FERRY") || t.Contains("TREN") || t.Contains("CANCELADO") || t.Contains("REPOSTAJE") || t.Contains("GASTO"))
        {
            return NoticeKind.Expense;
        }
        if (t.Contains("RUTA") || t.Contains("DESTINO") || t.Contains("BASCULA") || t.Contains("PESAJE") || t.Contains("DESCANSO") || t.Contains("LLEGADA"))
        {
            return NoticeKind.Route;
        }
        return NoticeKind.Default;
    }

    private static (string Header, string Value) SplitNotice(string text)
    {
        var t = text.Trim();
        if (t.StartsWith("RUTA ", StringComparison.OrdinalIgnoreCase))
        {
            return ("RUTA", t.Substring(5).Trim());
        }

        var plus = t.LastIndexOf(" +$", StringComparison.Ordinal);
        var minus = t.LastIndexOf(" -$", StringComparison.Ordinal);
        var index = Math.Max(plus, minus);
        if (index > 0)
        {
            var header = t.Substring(0, index).Trim();
            var value = t.Substring(index + 1).Trim();
            if (header.Length > 0 && value.Length > 0)
            {
                return (header, value);
            }
        }

        return (t, string.Empty);
    }

    private void StoreMessageBaseline(Snap snap)
    {
        _prevBankBalance = snap.BankBalance;
        _prevDeliveredRevenue = snap.JobDeliveredRevenue;
        _prevCancelledPenalty = snap.JobCancelledPenalty;
        _prevFineAmount = snap.FineAmount;
        _prevTollgatePay = snap.TollgatePay;
        _prevFerryPay = snap.FerryPay;
        _prevTrainPay = snap.TrainPay;
        _prevJobIncome = snap.JobIncome;
    }

    // La telemetría no expone el texto de las notificaciones del juego, así que
    // los avisos se reconstruyen con los importes que cambian en la partida.
    private void DetectGameMessages(Snap snap)
    {
        var specific = false;

        if (ChangedPositive(_prevDeliveredRevenue, snap.JobDeliveredRevenue) is { } delivered)
        {
            var cargo = string.IsNullOrWhiteSpace(snap.CargoName) ? string.Empty : $" {snap.CargoName}";
            PushMessage($"ENTREGA COMPLETADA{cargo} +{Money(delivered)}", NoticeKind.Income);
            specific = true;
        }

        if (ChangedPositive(_prevCancelledPenalty, snap.JobCancelledPenalty) is { } cancelled)
        {
            PushMessage($"TRABAJO CANCELADO -{Money(cancelled)}", NoticeKind.Expense);
            specific = true;
        }

        if (ChangedPositive(_prevFineAmount, snap.FineAmount) is { } fine)
        {
            PushMessage($"MULTA -{Money(fine)}", NoticeKind.Expense);
            specific = true;
        }

        if (ChangedPositive(_prevTollgatePay, snap.TollgatePay) is { } toll)
        {
            PushMessage($"PEAJE -{Money(toll)}", NoticeKind.Expense);
            specific = true;
        }

        if (ChangedPositive(_prevFerryPay, snap.FerryPay) is { } ferry)
        {
            PushMessage($"FERRY -{Money(ferry)}", NoticeKind.Expense);
            specific = true;
        }

        if (ChangedPositive(_prevTrainPay, snap.TrainPay) is { } train)
        {
            PushMessage($"TREN -{Money(train)}", NoticeKind.Expense);
            specific = true;
        }

        if (!specific
            && _prevJobIncome is { } previousIncome
            && snap.JobIncome is { } currentIncome
            && currentIncome != previousIncome
            && currentIncome > 0)
        {
            var dest = string.IsNullOrWhiteSpace(snap.CityDst) ? string.Empty : $" {snap.CityDst}";
            PushMessage($"NUEVO TRABAJO{dest} +{Money((long)currentIncome)}", NoticeKind.Income);
        }

        // El saldo solo se refresca al guardar; si no hubo un evento concreto,
        // un aumento se muestra como ingreso (sueldos de conductores, etc.).
        if (!specific
            && Increased(_prevBankBalance, snap.BankBalance) is { } income)
        {
            PushMessage($"INGRESO +{Money(income)}", NoticeKind.Income);
        }

        // Mensajes de ruta: cambio de destino.
        if (!string.IsNullOrWhiteSpace(snap.CityDst)
            && !string.Equals(_prevCityDst, snap.CityDst, StringComparison.Ordinal))
        {
            var origin = string.IsNullOrWhiteSpace(snap.CitySrc) ? string.Empty : $"{snap.CitySrc} → ";
            PushMessage($"RUTA {origin}{snap.CityDst}", NoticeKind.Route);
        }

        StoreMessageBaseline(snap);
        _prevCityDst = snap.CityDst;
    }

    private static long? ChangedPositive(long? previous, long? current)
    {
        if (previous is not { } before || current is not { } after)
        {
            return null;
        }

        return after > 0 && after != before ? after : null;
    }

    private static long? Increased(long? previous, long? current)
    {
        if (previous is not { } before || current is not { } after)
        {
            return null;
        }

        return after > before ? after - before : null;
    }

    private void DrawDashboard(
        Graphics g,
        Snap snap,
        DateTime now,
        bool ignitionOn,
        bool bulbCheck,
        double ignitionElapsed,
        DashboardLayout layout)
    {
        DrawFh6Header(g, snap, now, layout.CanvasWidth);
        DrawFh6IndicatorRow(g, snap, ignitionOn, bulbCheck, layout.CanvasWidth / 2f);
        DrawFh6SpeedCluster(g, snap, ignitionOn, bulbCheck, ignitionElapsed,
            layout.ClusterCenter, layout.ClusterRadius);
        DrawFh6VerticalGauges(g, snap, ignitionOn, ignitionElapsed, layout);

        DrawFh6FinancePanel(g, snap, layout.Finance.X, layout.Finance.Y,
            layout.Finance.Width, layout.Finance.Height);
        DrawFh6CargoPanel(g, snap, layout.Cargo.X, layout.Cargo.Y,
            layout.Cargo.Width, layout.Cargo.Height);
        DrawFh6DamagePanel(g, snap, layout.Damage.X, layout.Damage.Y,
            layout.Damage.Width, layout.Damage.Height);
        DrawFh6GpsPanel(g, snap, layout.Gps.X, layout.Gps.Y,
            layout.Gps.Width, layout.Gps.Height);

        DrawFh6FuelCard(g, snap, ignitionElapsed, layout.Fuel.X, layout.Fuel.Y,
            layout.Fuel.Width, layout.Fuel.Height);
        DrawFh6ReservedPanel(g, snap, layout.Notices.X, layout.Notices.Y,
            layout.Notices.Width, layout.Notices.Height);
        DrawFh6WeatherCard(g, snap, layout.Weather.X, layout.Weather.Y,
            layout.Weather.Width, layout.Weather.Height);
        DrawFh6Footer(g, snap, now, layout);

        if (!snap.Active && !snap.Demo)
        {
            DrawOnboardingCard(g, layout.ClusterCenter.X, layout.ClusterCenter.Y - 108f);
        }
    }
    // ---------------------------------------------------------------------
    // FH6-inspired layout
    // ---------------------------------------------------------------------

    private static void DrawFh6Backdrop(Graphics g, float width, float height)
    {
        using var background = new LinearGradientBrush(
            new RectangleF(0f, 0f, width, height),
            Theme.BackgroundTop,
            Theme.BackgroundBottom,
            LinearGradientMode.Vertical);
        g.FillRectangle(background, 0f, 0f, width, height);

        // Halo muy tenue detrás del cluster, como el cristal del FH6.
        using var halo = new SolidBrush(Theme.Alpha(Theme.Accent, 0.02f));
        var haloWidth = width * 0.406f;
        var haloHeight = height * 0.704f;
        g.FillEllipse(halo, (width - haloWidth) / 2f, height * 0.148f, haloWidth, haloHeight);
    }

    private void DrawFh6Header(Graphics g, Snap snap, DateTime now, float canvasWidth)
    {
        var statusRight = canvasWidth - 64f;
        var centerX = canvasWidth / 2f;
        var truck = TruckReference(snap);
        var brandText = (snap.TruckBrand ?? string.Empty).Trim().ToUpperInvariant();
        var displacement = EngineDisplacement(snap.Active ? snap.TruckId ?? snap.TruckName : null);
        var nameFont = Rounded(28f, FontStyle.Bold);
        var subFont = Rounded(13f, FontStyle.Regular);
        var logo = BrandLogoAsset(snap) is { } logoAsset ? BrandLogo(logoAsset) : null;

        // El logotipo ya muestra la marca; evita "KENWORTH KENWORTH T800".
        if (logo != null && brandText.Length > 0
            && truck.StartsWith(brandText + " ", StringComparison.OrdinalIgnoreCase))
        {
            var model = truck.Substring(brandText.Length).TrimStart();
            if (model.Length > 0)
            {
                truck = model;
            }
        }

        const float logoHeight = 32f;
        const float logoGap = 18f;
        var logoWidth = logo == null ? 0f : logoHeight * logo.Width / logo.Height;
        var nameWidth = g.MeasureString(truck, nameFont).Width;
        var subWidth = displacement == null ? 0f : g.MeasureString(displacement, subFont).Width;
        var textWidth = Math.Max(nameWidth, subWidth) + 2f;
        var groupWidth = logoWidth + (logo == null ? 0f : logoGap) + textWidth;
        var groupLeft = centerX - groupWidth / 2f;
        var textLeft = groupLeft + logoWidth + (logo == null ? 0f : logoGap);

        if (logo != null)
        {
            DrawBrandLogo(g, logo, new RectangleF(groupLeft, 21f, logoWidth, logoHeight));
        }

        DrawText(g, truck, nameFont, Theme.TextPrimary,
            new RectangleF(textLeft, 15f, textWidth, 36f), Center);
        if (displacement != null)
        {
            DrawText(g, displacement, subFont, Theme.TextTertiary,
                new RectangleF(textLeft, 49f, textWidth, 22f), Center);
        }

        var (statusText, statusColor) = Status(snap);
        var statusFont = Rounded(TypeScale.Caption, FontStyle.Bold);
        var statusWidth = g.MeasureString(statusText, statusFont).Width;
        using (var dot = new SolidBrush(statusColor))
        {
            g.FillEllipse(dot, statusRight - statusWidth - 14f, 30f, 7f, 7f);
        }
        DrawText(g, statusText, statusFont, WithAlpha(statusColor, 0.95f),
            new RectangleF(statusRight - statusWidth, 23f, statusWidth + 2f, 20f), Right);

        var context = $"{GameLabel(snap)}  ·  {ProfileLabel(snap)}";
        DrawText(g, context, Ui(TypeScale.Caption), Theme.TextSecondary,
            new RectangleF(statusRight - 456f, 46f, 456f, 18f), Right);
        DrawText(g, SyncLabel(now), Mono(TypeScale.Caption, FontStyle.Bold), Theme.TextTertiary,
            new RectangleF(statusRight - 456f, 64f, 456f, 18f), Right);

        DrawHairline(g, 64f, 92f, statusRight, 18);

        DrawHeaderActions(g);
    }

    private void DrawHeaderActions(Graphics g)
    {
        DrawHeaderAction(g, SettingsButton, "AJUSTES", settings: true);
        DrawHeaderAction(g, CreditsButton, "CRÉDITOS", settings: false);
    }

    private void DrawHeaderAction(Graphics g, RectangleF rect, string label, bool settings)
    {
        DrawSquircle(g, rect, 10f, Theme.SurfaceRaised, Theme.StrokeStrong, 1f);

        var icon = new RectangleF(rect.X + 8f, rect.Y + 7f, 12f, 12f);
        if (settings)
        {
            DrawGearIcon(g, icon, Theme.TextSecondary);
        }
        else
        {
            DrawInfoIcon(g, icon, Theme.TextSecondary);
        }

        DrawText(g, label, Rounded(TypeScale.Caption, FontStyle.Bold), Theme.TextSecondary,
            new RectangleF(rect.X + 26f, rect.Y + 4f, rect.Width - 30f, 18f), Left);
    }

    private static void DrawGearIcon(Graphics g, RectangleF rect, Color color)
    {
        var cx = rect.Left + rect.Width / 2f;
        var cy = rect.Top + rect.Height / 2f;
        var radius = rect.Width * 0.30f;

        using var pen = new Pen(color, 1.4f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        g.DrawEllipse(pen, cx - radius, cy - radius, radius * 2f, radius * 2f);

        for (var i = 0; i < 6; i++)
        {
            var angle = i * 60.0 + 15.0;
            g.DrawLine(pen,
                PointAt(cx, cy, radius * 0.85f, angle),
                PointAt(cx, cy, radius * 1.45f, angle));
        }
    }

    private static void DrawInfoIcon(Graphics g, RectangleF rect, Color color)
    {
        var cx = rect.Left + rect.Width / 2f;
        var cy = rect.Top + rect.Height / 2f;
        var radius = rect.Width * 0.34f;

        using var pen = new Pen(color, 1.3f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        g.DrawEllipse(pen, cx - radius, cy - radius, radius * 2f, radius * 2f);
        g.DrawLine(pen, cx, cy - radius * 0.10f, cx, cy + radius * 0.55f);

        using var dot = new SolidBrush(color);
        g.FillEllipse(dot, cx - 0.8f, cy - radius * 0.62f, 1.6f, 1.6f);
    }

    private void DrawFh6IndicatorRow(
        Graphics g, Snap snap, bool ignitionOn, bool bulbCheck, float centerX)
    {
        var checkEngine = ignitionOn && snap.Engine
                          && snap.EngineDmg is { } engineDamage && engineDamage >= 5;
        var indicators = new (string Asset, bool Active, Color Color, bool Pulses, bool Framed, float Width)[]
        {
            ("beacon-c", ignitionOn && snap.Beacon, Yellow, true, false, 32f),
            ("side-markers-c", ignitionOn && snap.Parking, Green, false, false, 32f),
            ("low-beam-c", ignitionOn && snap.LowB, Green, false, false, 32f),
            ("high-beam-c", ignitionOn && snap.HighB, Blue, false, false, 32f),
            ("brake-c1", ignitionOn && snap.Brake, Red, false, false, 32f),
            ("turn-lights-left-c", ignitionOn && snap.BlinkL, Green, false, false, 32f),
            ("check-engine-line", checkEngine, Yellow, true, true, 88f),
            ("turn-lights-right-c", ignitionOn && snap.BlinkR, Green, false, false, 32f),
            ("parking-brake-2-c", ignitionOn && snap.Park, Red, true, false, 32f),
            ("retarder-c", ignitionOn && snap.Retarder, Green, false, false, 32f),
            ("engine-brake-round", ignitionOn && snap.Motor, Green, false, false, 32f),
            ("wiper-c", ignitionOn && snap.Wipers > 0, Cyan, true, false, 32f),
            ("diff-lock-c", ignitionOn && snap.DiffLock, Orange, true, false, 32f)
        };

        const float iconSize = 40f;
        const float spacing = 18f;
        var total = -spacing;
        foreach (var indicator in indicators)
        {
            total += indicator.Width + spacing;
        }
        var startX = centerX - total / 2f;

        DrawSquircle(g,
            new RectangleF(startX - 18f, 108f, total + 36f, 58f),
            18f,
            Ink(4),
            Ink(12),
            1f);

        var cursor = startX;
        for (var i = 0; i < indicators.Length; i++)
        {
            var item = indicators[i];
            var ix = cursor;
            var lit = item.Active || bulbCheck;

            if (ignitionOn && item.Framed)
            {
                // El CHECK solo aparece con el motor encendido. Apagado queda
                // en blanco, como el resto de testigos, y se ilumina cuando
                // el juego marca aver��a.
                var checkPulse = lit && item.Pulses && !bulbCheck
                    ? 0.55f + 0.45f * (float)Math.Abs(Math.Sin(DateTime.UtcNow.TimeOfDay.TotalSeconds * Math.PI * 2.0))
                    : 1f;
                DrawSquircle(g, new RectangleF(ix + 1f, 118f, item.Width - 2f, iconSize - 2f), 7f,
                    lit ? WithAlpha(item.Color, 0.07f * checkPulse) : Color.Transparent,
                    lit ? WithAlpha(item.Color, 0.58f) : Ink(22),
                    1f);
                DrawTintedIcon(g, item.Asset,
                    new RectangleF(ix + 10f, 128f, 22f, 22f),
                    lit ? WithAlpha(item.Color, checkPulse) : White(0.40f));
                DrawFitted(g, "CHECK", "Bahnschrift", TypeScale.Caption, FontStyle.Bold,
                    lit ? WithAlpha(item.Color, 0.82f * checkPulse) : Theme.TextTertiary,
                    new RectangleF(ix + 36f, 123f, item.Width - 40f, 14f), Center, 8f);
                DrawFitted(g, "ENGINE", "Bahnschrift", TypeScale.Caption, FontStyle.Bold,
                    lit ? WithAlpha(item.Color, checkPulse) : Theme.TextTertiary,
                    new RectangleF(ix + 36f, 137f, item.Width - 40f, 14f), Center, 8f);
            }
            else if (ignitionOn)
            {
                // Sin motor, la tira de testigos queda apagada.
                DrawIcon(g, item.Asset, new RectangleF(ix + (item.Width - iconSize) / 2f, 117f, iconSize, iconSize),
                    lit, item.Color, item.Pulses && !bulbCheck);
            }

            cursor += item.Width + spacing;
        }
    }

    private void DrawFh6SpeedCluster(
        Graphics g,
        Snap snap,
        bool ignitionOn,
        bool bulbCheck,
        double ignitionElapsed,
        PointF center,
        float radius)
    {
        var cx = center.X;
        var cy = center.Y;
        const double startAngle = 158.0;
        const double sweep = 224.0;
        var maxSpeed = SpeedMax;

        var active = snap.Active;
        var speedKmh = active ? ToDisplaySpeed(Math.Abs(snap.Speed ?? 0f) * 3.6) : 0;
        var limitKmh = active && snap.Limit is { } limit && limit > 0.5f
            ? ToDisplaySpeed(limit * 3.6)
            : (double?)null;
        var cruiseSetKmh = ignitionOn && snap.CruiseOn && snap.Cruise is { } cruise && cruise > 0.5f
            ? ToDisplaySpeed(cruise * 3.6)
            : (double?)null;
        var oilLimit = snap.OilWarn is > 0 ? snap.OilWarn.Value : 14f;
        var batteryLimit = snap.BatteryWarn is > 0 ? snap.BatteryWarn.Value : 23.76f;
        var oilWarning = ignitionOn && snap.OilPressure is { } oilPressure
                         && oilPressure < oilLimit;
        var batteryWarning = ignitionOn && snap.BatteryVoltage is { } batteryVoltage
                             && batteryVoltage < batteryLimit;
        var overLimit = limitKmh is { } limitValue && speedKmh > limitValue + 1.0;
        var accent = !active
            ? Theme.TextDisabled
            : overLimit ? Orange : Cyan;
        var targetFraction = Math.Clamp(speedKmh / maxSpeed, 0, 1);
        var fraction = AnimatedFraction(SmoothTarget("speed", targetFraction), ignitionElapsed);
        var displayedSpeedKmh = fraction * maxSpeed;

        // Halo eliminado: no combinaba con el tacometro en el layout vertical.

        using (var track = new Pen(Ink(16), 16f)
               {
                   StartCap = LineCap.Round,
                   EndCap = LineCap.Round
               })
        {
            g.DrawArc(track, cx - radius, cy - radius, radius * 2f, radius * 2f,
                (float)startAngle, (float)sweep);
        }

        if (fraction > 0.002)
        {
            using (var glow = new Pen(WithAlpha(accent, 0.12f), 22f)
                   {
                       StartCap = LineCap.Round,
                       EndCap = LineCap.Round
                   })
            {
                g.DrawArc(glow, cx - radius, cy - radius, radius * 2f, radius * 2f,
                    (float)startAngle, (float)(sweep * fraction));
            }

            DrawFh6SpeedArc(g, cx, cy, radius, startAngle, sweep, fraction, accent, Mint, 16f);
        }

        var tickCount = (int)Math.Round(maxSpeed / 10.0);
        for (var i = 0; i <= tickCount; i++)
        {
            var value = i * 10.0;
            var angle = startAngle + sweep * (value / maxSpeed);
            var major = i % 2 == 0;
            var outer = radius + (major ? 22f : 14f);
            var inner = radius + 5f;
            using var tick = new Pen(
                Ink(major ? 150 : 50),
                major ? 1.7f : 1.1f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };
            g.DrawLine(tick, PointAt(cx, cy, inner, angle), PointAt(cx, cy, outer, angle));

            if (major)
            {
                var p = PointAt(cx, cy, radius + 44f, angle);
                DrawText(g, Math.Round(value).ToString("0", Es),
                    Rounded(12f, FontStyle.Bold), Theme.TextTertiary,
                    new RectangleF(p.X - 26f, p.Y - 11f, 52f, 22f), Center);
            }
        }

        if (fraction > 0.002)
        {
            var angle = startAngle + sweep * fraction;
            using var marker = new Pen(accent, 4f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };
            g.DrawLine(marker,
                PointAt(cx, cy, radius - 16f, angle),
                PointAt(cx, cy, radius + 16f, angle));
        }

        var valueText = active && snap.Speed != null
            ? Math.Round(displayedSpeedKmh).ToString("0", Es)
            : "--";
        var speedColor = !active ? Theme.TextDisabled : overLimit ? Orange : Theme.TextPrimary;
        DrawFitted(g, valueText, "Bahnschrift Light", 204f, FontStyle.Regular, speedColor,
            new RectangleF(cx - 260f, cy - 194f, 520f, 214f), Center, 110f);

        DrawFh6CruiseChip(g, cx, cy - (radius - 64f), cruiseSetKmh);

        if (ignitionOn)
        {
            DrawFh6WarningLamp(g, new RectangleF(cx - 93.5f, cy - (radius - 56f), 32f, 32f),
                batteryWarning || bulbCheck, WarningLampKind.Battery);
            DrawFh6WarningLamp(g, new RectangleF(cx + 61.5f, cy - (radius - 56f), 32f, 32f),
                oilWarning || bulbCheck, WarningLampKind.Oil);
        }

        var gearText = !active || snap.Gear is not { } gear
            ? "--"
            : gear > 0 ? gear.ToString(Es) : gear == 0 ? "N" : "R";
        var gearColor = !active || snap.Gear is not { } gearValue
            ? Theme.TextDisabled
            : gearValue < 0 ? Red : gearValue == 0 ? Cyan : Theme.TextPrimary;

        DrawSquircle(g, new RectangleF(cx - 35f, cy + 53f, 70f, 64f), 14f,
            Ink(5),
            Ink(18),
            1f);
        DrawTracked(g, "MARCHA", Rounded(TypeScale.Caption, FontStyle.Bold), Theme.TextTertiary,
            cx, cy + 63f, 1.3f, Center);
        DrawText(g, gearText, Rounded(42f, FontStyle.Bold), gearColor,
            new RectangleF(cx - 35f, cy + 69f, 70f, 44f), Center);

        if (ignitionOn)
        {
            var gaugeMax = snap.RpmMax is { } rpmMax && rpmMax >= 800 ? (double)rpmMax : 2500.0;
            var hasRpm = snap.Rpm != null;
            var targetRpm = Math.Clamp(snap.Rpm ?? 0, 0, (float)gaugeMax);
            var targetRpmFraction = gaugeMax > 0 ? targetRpm / gaugeMax : 0;
            var rpmFraction = hasRpm
                ? AnimatedFraction(SmoothTarget("rpm", targetRpmFraction), ignitionElapsed)
                : 0;
            var rpm = rpmFraction * gaugeMax;
            var rpmColor = rpmFraction >= 0.92 ? Red
                : rpmFraction >= 0.84 ? Orange
                : rpmFraction >= 0.76 ? Yellow
                : Green;

            var rpmLeft = cx - 165f;
            DrawTracked(g, "RPM", Rounded(TypeScale.Caption, FontStyle.Bold), Theme.TextTertiary,
                rpmLeft, cy + 141f, 2f);
            DrawText(g, hasRpm ? Math.Round(rpm).ToString("N0", Es) : "--",
                Rounded(15f, FontStyle.Bold), rpmColor,
                new RectangleF(rpmLeft + 34f, cy + 130f, 140f, 24f), Left);
            DrawText(g, $"MÁX {Math.Round(gaugeMax).ToString("N0", Es)}",
                Mono(TypeScale.Caption, FontStyle.Bold), Theme.TextTertiary,
                new RectangleF(rpmLeft + 190f, cy + 133f, 140f, 18f), Right);

            DrawFh6RpmBar(g, rpmLeft, cy + 162f, 330f, 14f, rpmFraction, rpmColor);

            var redlineX = rpmLeft + 330f * 0.92f;
            using var redline = new Pen(WithAlpha(Red, 0.58f), 2f);
            g.DrawLine(redline, redlineX, cy + 156f, redlineX, cy + 182f);
        }

        var odometerValue = active && snap.Odometer is { } totalOdometer && totalOdometer >= 0
            ? $"{Math.Round(totalOdometer).ToString("N0", Es)} km"
            : "-- km";
        var odometerActive = active && snap.Odometer != null;

        // Sin pill ni tinte azul: lectura tabular limpia, como el resto del cluster.
        DrawText(g, "ODÓMETRO", Rounded(TypeScale.Caption, FontStyle.Bold), Theme.TextTertiary,
            new RectangleF(cx - 110f, cy + 188f, 220f, 16f), Center);
        DrawFitted(g, odometerValue, "Consolas", 22f, FontStyle.Bold,
            odometerActive ? Theme.TextSecondary : Theme.TextDisabled,
            new RectangleF(cx - 110f, cy + 202f, 220f, 26f), Center, 14f);
    }

    private static void DrawFh6SpeedArc(
        Graphics g,
        float cx,
        float cy,
        float radius,
        double startAngle,
        double sweep,
        double fraction,
        Color from,
        Color to,
        float width)
    {
        var litSweep = sweep * fraction;
        if (litSweep <= 0)
        {
            return;
        }

        var segments = Math.Max(12, (int)Math.Ceiling(litSweep / 7.0));
        for (var i = 0; i < segments; i++)
        {
            var t0 = (double)i / segments;
            var t1 = (double)(i + 1) / segments;
            var color = Blend(from, to, (t0 + t1) * 0.5);

            using var pen = new Pen(color, width)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };
            g.DrawArc(
                pen,
                cx - radius,
                cy - radius,
                radius * 2f,
                radius * 2f,
                (float)(startAngle + litSweep * t0),
                (float)(litSweep * (t1 - t0) + 0.8));
        }
    }

    private static Color Blend(Color from, Color to, double amount)
    {
        var t = Math.Clamp(amount, 0, 1);
        return Color.FromArgb(
            (int)Math.Round(from.R + (to.R - from.R) * t),
            (int)Math.Round(from.G + (to.G - from.G) * t),
            (int)Math.Round(from.B + (to.B - from.B) * t));
    }

    private void DrawFh6CruiseChip(Graphics g, float cx, float top, double? setSpeed)
    {
        const float width = 95f;
        const float height = 48f;
        var x = cx - width / 2f;
        var active = setSpeed is not null;

        // El chip del crucero queda siempre visible. Con el crucero activo
        // mantiene el tinte verde; inactivo se muestra en gris tenue y "--".
        DrawSquircle(g, new RectangleF(x, top, width, height), 14f,
            active ? WithAlpha(Green, 0.10f) : White(0.045f),
            active ? WithAlpha(Green, 0.42f) : White(0.22f),
            1f);

        DrawTintedIcon(g, "cruise-control-c",
            new RectangleF(x + 9f, top + 16f, 16f, 16f),
            active ? Green : White(0.40f), active ? 0.92f : 1f);
        DrawTracked(g, "CRUCERO", Rounded(TypeScale.Caption, FontStyle.Bold),
            active ? Theme.TextSecondary : Theme.TextTertiary,
            x + 31f, top + 14f, 0.9f);
        var speedText = setSpeed is { } value
            ? $"{Math.Round(value).ToString("0", Es)} {SpeedUnit}"
            : $"-- {SpeedUnit}";
        DrawText(g, speedText, Rounded(13f, FontStyle.Bold),
            active ? WithAlpha(Green, 0.92f) : Theme.TextDisabled,
            new RectangleF(x + 30f, top + 24f, width - 37f, 19f), Left);
    }

    private enum WarningLampKind
    {
        Battery,
        Oil
    }

    private static void DrawFh6WarningLamp(
        Graphics g,
        RectangleF rect,
        bool active,
        WarningLampKind kind)
    {
        if (active)
        {
            using var glow = new SolidBrush(Color.FromArgb(36, Red));
            g.FillEllipse(glow, rect.Left - 4f, rect.Top - 4f,
                rect.Width + 8f, rect.Height + 8f);
        }

        var color = active ? Red : Color.FromArgb(255, 36, 36, 36);
        if (kind == WarningLampKind.Battery)
        {
            DrawBatteryWarningIcon(g, rect, color, active);
        }
        else
        {
            DrawOilWarningIcon(g, rect, color);
        }
    }

    private static void DrawBatteryWarningIcon(
        Graphics g,
        RectangleF rect,
        Color color,
        bool active)
    {
        var w = rect.Width;
        var h = rect.Height;
        var stroke = Math.Max(1.4f, w * 0.065f);
        using var pen = new Pen(color, stroke)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };

        var body = new RectangleF(
            rect.Left + w * 0.12f,
            rect.Top + h * 0.34f,
            w * 0.76f,
            h * 0.42f);
        using (var path = SquirclePath(body, w * 0.07f))
        {
            if (active)
            {
                using var fill = new SolidBrush(Color.FromArgb(48, Red));
                g.FillPath(fill, path);
            }

            g.DrawPath(pen, path);
        }

        var terminalWidth = w * 0.14f;
        var terminalHeight = h * 0.12f;
        using (var terminalPen = new Pen(color, Math.Max(1.2f, stroke * 0.9f))
               {
                   StartCap = LineCap.Round,
                   EndCap = LineCap.Round
               })
        {
            using var leftTerminal = SquirclePath(new RectangleF(
                rect.Left + w * 0.25f,
                rect.Top + h * 0.24f,
                terminalWidth,
                terminalHeight), w * 0.025f);
            using var rightTerminal = SquirclePath(new RectangleF(
                rect.Left + w * 0.61f,
                rect.Top + h * 0.24f,
                terminalWidth,
                terminalHeight), w * 0.025f);
            g.DrawPath(terminalPen, leftTerminal);
            g.DrawPath(terminalPen, rightTerminal);
        }

        var midY = rect.Top + h * 0.55f;
        var plusX = rect.Left + w * 0.355f;
        using var detailPen = new Pen(color, Math.Max(1.1f, stroke * 0.8f))
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        g.DrawLine(detailPen, rect.Left + w * 0.28f, midY,
            rect.Left + w * 0.43f, midY);
        g.DrawLine(detailPen, plusX, midY - h * 0.075f,
            plusX, midY + h * 0.075f);
        g.DrawLine(detailPen, rect.Left + w * 0.58f, midY,
            rect.Left + w * 0.73f, midY);
    }

    private static void DrawOilWarningIcon(Graphics g, RectangleF rect, Color color)
    {
        var w = rect.Width;
        var h = rect.Height;
        var stroke = Math.Max(1.5f, w * 0.07f);
        using var brush = new SolidBrush(color);
        using var pen = new Pen(color, stroke)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };

        var body = new RectangleF(
            rect.Left + w * 0.10f,
            rect.Top + h * 0.44f,
            w * 0.40f,
            h * 0.30f);
        using (var bodyPath = SquirclePath(body, w * 0.06f))
        {
            g.FillPath(brush, bodyPath);
        }

        g.DrawArc(pen,
            rect.Left + w * 0.16f,
            rect.Top + h * 0.27f,
            w * 0.28f,
            h * 0.28f,
            180f,
            180f);

        g.DrawLine(pen,
            rect.Left + w * 0.48f,
            rect.Top + h * 0.52f,
            rect.Left + w * 0.82f,
            rect.Top + h * 0.33f);

        var dropCx = rect.Left + w * 0.82f;
        var dropTop = rect.Top + h * 0.52f;
        var dropRadius = w * 0.055f;
        var dropBottom = rect.Top + h * 0.73f;
        g.FillEllipse(brush,
            dropCx - dropRadius,
            dropBottom - dropRadius * 2f,
            dropRadius * 2f,
            dropRadius * 2f);
        g.FillPolygon(brush, new[]
        {
            new PointF(dropCx, dropTop),
            new PointF(dropCx - dropRadius, dropBottom - dropRadius),
            new PointF(dropCx + dropRadius, dropBottom - dropRadius)
        });
    }

    private static void DrawFh6RpmBar(
        Graphics g,
        float x,
        float y,
        float width,
        float height,
        double fraction,
        Color color)
    {
        const int segments = 24;
        const float gap = 2f;
        var segmentWidth = (width - gap * (segments - 1)) / segments;
        var lit = fraction * segments;

        for (var i = 0; i < segments; i++)
        {
            var fill = i + 0.5 < lit ? color : Ink(17);
            var rect = new RectangleF(x + i * (segmentWidth + gap), y, segmentWidth, height);
            DrawRounded(g, rect, 2.5f, fill, Color.Transparent, 0f);
        }
    }

    private void DrawFh6FinancePanel(
        Graphics g,
        Snap snap,
        float x,
        float y,
        float width,
        float height)
    {
        DrawGlassPanel(g, x, y, width, height, Theme.RadiusPanel);

        var contentW = (width - 48f) * 0.90f;
        var contentX = x + (width - contentW) / 2f;

        // El logotipo real del perfil del juego va a la derecha, con el mismo
        // margen interior que el resto del panel. El nombre conserva la
        // alineación izquierda original.
        const float companyLogoSize = 62f;
        const float companyLogoGap = 14f;
        var companyLogo = CompanyLogoImage(snap.CompanyLogo);
        var hasCompanyName = !string.IsNullOrWhiteSpace(snap.CompanyName);
        var showCompanyBadge = companyLogo != null || hasCompanyName;
        var companyTextW = showCompanyBadge
            ? contentW - companyLogoSize - companyLogoGap
            : contentW;

        var company = hasCompanyName ? snap.CompanyName! : "SIN EMPRESA";
        DrawFitted(g, company, "Bahnschrift", 30f, FontStyle.Bold,
            hasCompanyName ? Theme.TextPrimary : Theme.TextDisabled,
            new RectangleF(contentX, y + 18f, companyTextW, 40f), Left, 16f);

        var logoRect = new RectangleF(x + width - 24f - companyLogoSize, y + 18f,
            companyLogoSize, companyLogoSize);
        if (companyLogo != null)
        {
            DrawFh6CompanyLogo(g, companyLogo, logoRect);
        }
        else if (hasCompanyName)
        {
            DrawCompanyMonogram(g, company, logoRect);
        }


        var income = Income(snap);
        var net = Net(snap);
        var margin = income is { } incomeValue && incomeValue > 0 && net is { } netValue
            ? Math.Clamp((double)netValue / incomeValue, 0, 1)
            : (double?)null;

        // Tinte sutil segun la ganancia neta (verde si positiva, rojo si negativa).
        {
            var tintColor = net is { } netVal && netVal >= 0 ? Green : Red;
            var alpha = net == null ? 10 : 18;
            using (var brush = new SolidBrush(Color.FromArgb(alpha, tintColor.R, tintColor.G, tintColor.B)))
            using (var path = RoundedPath(new RectangleF(x, y, width, height), Theme.RadiusPanel))
            {
                g.FillPath(brush, path);
            }
        }

        if (ShowDetails)
        {
            var driversLabel = snap.HiredDrivers is { } driverCount
                ? $"CONDUCTORES ({driverCount})"
                : "CONDUCTORES";

            DrawFh6LedgerRow(g, contentX, y + 112f, contentW, "TRABAJO ACTUAL", Money(income), Green, 21f);
            DrawFh6LedgerRow(g, contentX, y + 148f, contentW, driversLabel, Money(snap.DriverIncome), Green, 21f);
            DrawFh6LedgerRow(g, contentX, y + 184f, contentW, "SALDO DEL BANCO", Money(snap.BankBalance), Cyan, 21f);


            // Tres iconos centrados: garage, camion, conductores.
            var iconSize = 40f;
            var iconY = y + 230f;
            var spacing = 20f;
            var groupWidth = 4 * iconSize + 3 * spacing;
            var startX = contentX + (contentW - groupWidth) / 2f;
            var cx1 = startX + iconSize / 2f;
            var cx2 = cx1 + iconSize + spacing;
            var cx3 = cx2 + iconSize + spacing;
            var cx4 = cx3 + iconSize + spacing;
            var garageIcon = CompanyIcon("garaje");
            if (garageIcon != null)
            {
                using (var attributes = new ImageAttributes())
                {
                    attributes.SetColorMatrix(ColorTintMatrix(Color.White, 1f));
                    var rect = new RectangleF(cx1 - iconSize / 2f, iconY, iconSize, iconSize);
                    g.DrawImage(garageIcon, Rectangle.Round(rect), 0, 0, garageIcon.Width, garageIcon.Height, GraphicsUnit.Pixel, attributes);
                }
            }
            DrawText(g, snap.Garages?.ToString() ?? "--", Rounded(28f, FontStyle.Bold), Theme.TextPrimary,
                new RectangleF(cx1 - 40f, iconY + iconSize + 8f, 80f, 34f), Center);
            DrawText(g, "GARAGE", Rounded(9f, FontStyle.Bold), Theme.TextTertiary,
                new RectangleF(cx1 - 40f, iconY + iconSize + 46f, 80f, 16f), Center);

            var truckIcon = CompanyIcon("camion");
            if (truckIcon != null)
            {
                using (var attributes = new ImageAttributes())
                {
                    attributes.SetColorMatrix(ColorTintMatrix(Color.White, 1f));
                    var rect = new RectangleF(cx2 - iconSize / 2f, iconY, iconSize, iconSize);
                    g.DrawImage(truckIcon, Rectangle.Round(rect), 0, 0, truckIcon.Width, truckIcon.Height, GraphicsUnit.Pixel, attributes);
                }
            }
            DrawText(g, snap.Trucks?.ToString() ?? "--", Rounded(28f, FontStyle.Bold), Theme.TextPrimary,
                new RectangleF(cx2 - 40f, iconY + iconSize + 8f, 80f, 34f), Center);
            DrawText(g, "CAMIONES", Rounded(9f, FontStyle.Bold), Theme.TextTertiary,
                new RectangleF(cx2 - 40f, iconY + iconSize + 46f, 80f, 16f), Center);

            var personIcon = CompanyIcon("conductor");
            if (personIcon != null)
            {
                using (var attributes = new ImageAttributes())
                {
                    attributes.SetColorMatrix(ColorTintMatrix(Color.White, 1f));
                    var rect = new RectangleF(cx3 - iconSize / 2f, iconY, iconSize, iconSize);
                    g.DrawImage(personIcon, Rectangle.Round(rect), 0, 0, personIcon.Width, personIcon.Height, GraphicsUnit.Pixel, attributes);
                }
            }
            DrawText(g, snap.HiredDrivers?.ToString() ?? "--", Rounded(28f, FontStyle.Bold), Theme.TextPrimary,
                new RectangleF(cx3 - 40f, iconY + iconSize + 8f, 80f, 34f), Center);
            DrawText(g, "CONDUCTORES", Rounded(9f, FontStyle.Bold), Theme.TextTertiary,
                new RectangleF(cx3 - 40f, iconY + iconSize + 46f, 80f, 16f), Center);

            var mailIcon = CompanyIcon("correo-electronico");
            if (mailIcon != null)
            {
                using (var attributes = new ImageAttributes())
                {
                    attributes.SetColorMatrix(ColorTintMatrix(Color.White, 1f));
                    var rect = new RectangleF(cx4 - iconSize / 2f, iconY, iconSize, iconSize);
                    g.DrawImage(mailIcon, Rectangle.Round(rect), 0, 0, mailIcon.Width, mailIcon.Height, GraphicsUnit.Pixel, attributes);
                }
            }
            DrawText(g, "0", Rounded(28f, FontStyle.Bold), Theme.TextPrimary,
                new RectangleF(cx4 - 40f, iconY + iconSize + 8f, 80f, 34f), Center);
            DrawText(g, "CORREOS", Rounded(9f, FontStyle.Bold), Theme.TextTertiary,
                new RectangleF(cx4 - 40f, iconY + iconSize + 46f, 80f, 16f), Center);
        }
        else
        {
            // Tres datos primarios: trabajo, ganancia y saldo. El resto vive en
            // el detalle (ShowDetails) para no competir con el cluster.
            DrawFh6LedgerRow(g, contentX, y + 118f, contentW, "TRABAJO ACTUAL", Money(income), Green, 28f);
            DrawFh6LedgerRow(g, contentX, y + 178f, contentW, "GANANCIA NETA", Money(net), Cyan, 28f);

            DrawTracked(g, "SALDO DEL BANCO", Rounded(TypeScale.Caption, FontStyle.Bold), Theme.TextTertiary,
                contentX, y + 256f, 1.2f);
            DrawFitted(g, Money(snap.BankBalance), "Bahnschrift", 38f, FontStyle.Bold, Cyan,
                new RectangleF(contentX, y + 266f, contentW, 52f), Right, 22f);

            DrawMetricRow(g, contentX, y + 332f, contentW, "MARGEN",
                margin == null ? "--" : $"{Math.Round(margin.Value * 100).ToString("0", Es)}%",
                margin == null ? Theme.TextDisabled : Cyan);
            DrawProgressBar(g, contentX, y + 348f, contentW, 6f, margin ?? 0, Cyan);
        }
    }

    private void DrawFh6LedgerRow(
        Graphics g,
        float x,
        float y,
        float width,
        string label,
        string value,
        Color color,
        float valueSize)
    {
        DrawTracked(g, label, Rounded(TypeScale.Caption, FontStyle.Bold), Theme.TextTertiary, x, y, 1.2f);
        DrawFitted(g, value, "Bahnschrift", valueSize, FontStyle.Bold, color,
            new RectangleF(x, y - 14f, width, valueSize + 18f), Right, TypeScale.Caption);
    }

    private void DrawFh6CargoPanel(
        Graphics g,
        Snap snap,
        float x,
        float y,
        float width,
        float height)
    {
        DrawGlassPanel(g, x, y, width, height, Theme.RadiusPanel);

        var contentW = (width - 48f) * 0.90f;
        var contentX = x + (width - contentW) / 2f;
        var loaded = snap.Active && snap.CargoLoaded;
        var cargoName = loaded && !string.IsNullOrWhiteSpace(snap.CargoName) ? snap.CargoName : null;
        var cargoId = loaded && !string.IsNullOrWhiteSpace(snap.CargoId) ? snap.CargoId : null;
        var mass = loaded && snap.CargoMass is { } cargoMass && cargoMass > 0 ? (double?)cargoMass : null;
        var damage = loaded && snap.CargoDmg is { } cargoDamage ? (double?)cargoDamage : null;
        var accent = !snap.Active || !loaded ? Theme.TextDisabled : DamageColor(damage ?? 0);
        var damageColor = DamageColor(damage);

        DrawFitted(g, Localization.L("CARGO"), "Bahnschrift", 24f, FontStyle.Bold, accent,
            new RectangleF(contentX, y + 18f, contentW, 32f), Center, 14f);

        DrawFitted(g, cargoName ?? "SIN CARGA", "Bahnschrift", 27f, FontStyle.Bold, Theme.TextPrimary,
            new RectangleF(contentX, y + 60f, contentW, 38f), LeftTop, 15f);

        if (ShowDetails)
        {
            DrawTracked(g, "TRABAJO", Rounded(TypeScale.Caption, FontStyle.Bold), Theme.TextTertiary,
                contentX, y + 102f, 1.2f);
            DrawText(g, snap.SpecialJob ? "ESPECIAL" : "ESTÁNDAR", Rounded(TypeScale.Body, FontStyle.Bold),
                snap.SpecialJob ? WithAlpha(Yellow, 0.88f) : Theme.TextSecondary,
                new RectangleF(contentX, y + 114f, 160f, 20f), Left);

            // Imagen de la carga: 88 % del espacio libre, centrada.
            var topEnd = y + 128f;
            var bottomStart = y + height - 100f;
            var freeHeight = bottomStart - topEnd;
            var imageSize = freeHeight * 0.88f;
            var imageY = topEnd + (freeHeight - imageSize) / 2f;
            DrawFh6CargoImage(g, snap,
                new RectangleF(contentX, imageY, contentW, imageSize));

            // PESO encima de la barra de integridad (al final).
            DrawTracked(g, "PESO", Rounded(TypeScale.Caption, FontStyle.Bold), Theme.TextTertiary,
                contentX, y + height - 96f, 1.2f);
            var massText = mass == null ? "-- kg" : $"{Math.Round(mass.Value).ToString("N0", Es)} kg";
            DrawFitted(g, massText, "Bahnschrift", 22f, FontStyle.Bold, Theme.TextPrimary,
                new RectangleF(contentX, y + height - 80f, contentW - 70f, 28f), Left, 14f);
            if (mass != null)
            {
                DrawText(g, $"{(mass.Value / 1000).ToString("N1", Es)} t",
                    Mono(TypeScale.Caption, FontStyle.Bold), Theme.TextTertiary,
                    new RectangleF(x + width - 94f, y + height - 72f, 70f, 16f), Right);
            }

            // Integridad en una fila: texto a la izquierda, barra en medio y porcentaje a la derecha.
            var integrityY = y + height - 42f;
            const float labelWidth = 92f;
            const float percentWidth = 44f;
            const float gap = 8f;
            DrawTracked(g, "INTEGRIDAD", Rounded(TypeScale.Caption, FontStyle.Bold), Theme.TextTertiary,
                contentX, integrityY + 12f, 1.2f);
            DrawProgressBar(g, contentX + labelWidth + gap, integrityY + 9f,
                contentW - labelWidth - percentWidth - gap * 2f, 6f,
                damage == null ? 0 : Math.Clamp(1 - damage.Value / 100, 0, 1), damageColor);
            DrawText(g, damage == null ? "--" : $"{Math.Round(100 - damage.Value).ToString("0", Es)}%",
                Mono(13f, FontStyle.Bold), damageColor,
                new RectangleF(contentX + contentW - percentWidth - 40f, integrityY - 2f, percentWidth + 40f, 26f), Right);
        }
        else
        {
            var topEnd = y + 120f;
            var bottomStart = y + height - 110f;
            var freeHeight = bottomStart - topEnd;
            var imageSize = freeHeight * 0.80f;
            var imageY = topEnd + (freeHeight - imageSize) / 2f;
            DrawFh6CargoImage(g, snap,
                new RectangleF(contentX, imageY, contentW, imageSize));

            // PESO encima de la barra de integridad (al final).
            DrawTracked(g, "PESO", Rounded(TypeScale.Caption, FontStyle.Bold), Theme.TextTertiary,
                contentX, y + height - 96f, 1.2f);
            var massText = mass == null ? "-- kg" : $"{Math.Round(mass.Value).ToString("N0", Es)} kg";
            DrawFitted(g, massText, "Bahnschrift", 22f, FontStyle.Bold, Theme.TextPrimary,
                new RectangleF(contentX, y + height - 80f, contentW - 70f, 28f), Left, 14f);
            if (mass != null)
            {
                DrawText(g, $"{(mass.Value / 1000).ToString("N1", Es)} t",
                    Mono(TypeScale.Caption, FontStyle.Bold), Theme.TextTertiary,
                    new RectangleF(x + width - 94f, y + height - 72f, 70f, 16f), Right);
            }

            // Integridad en una fila: texto a la izquierda, barra en medio y porcentaje a la derecha.
            var integrityY = y + height - 42f;
            const float labelWidth = 92f;
            const float percentWidth = 44f;
            const float gap = 8f;
            DrawTracked(g, "INTEGRIDAD", Rounded(TypeScale.Caption, FontStyle.Bold), Theme.TextTertiary,
                contentX, integrityY + 12f, 1.2f);
            DrawProgressBar(g, contentX + labelWidth + gap, integrityY + 9f,
                contentW - labelWidth - percentWidth - gap * 2f, 6f,
                damage == null ? 0 : Math.Clamp(1 - damage.Value / 100, 0, 1), damageColor);
            DrawText(g, damage == null ? "--" : $"{Math.Round(100 - damage.Value).ToString("0", Es)}%",
                Mono(13f, FontStyle.Bold), damageColor,
                new RectangleF(contentX + contentW - percentWidth - 40f, integrityY - 2f, percentWidth + 40f, 26f), Right);
        }
    }
    private static void DrawFh6CompanyLogo(Graphics g, Image image, RectangleF rect)
    {
        // El PNG ya contiene el logotipo tal como lo dibuja el juego. Se centra
        // en el hueco de 62 x 62 sin deformarlo.
        var scale = Math.Min(rect.Width / image.Width, rect.Height / image.Height);
        var width = image.Width * scale;
        var height = image.Height * scale;
        var target = new RectangleF(
            rect.X + (rect.Width - width) / 2f,
            rect.Y + (rect.Height - height) / 2f,
            width,
            height);

        using var attributes = new ImageAttributes();
        attributes.SetColorMatrix(AlphaMatrix(1f));
        g.DrawImage(
            image,
            Rectangle.Round(target),
            0,
            0,
            image.Width,
            image.Height,
            GraphicsUnit.Pixel,
            attributes);
    }

    private void DrawCompanyMonogram(Graphics g, string company, RectangleF rect)
    {
        var trimmed = company.Trim();
        var initial = trimmed.Length > 0 ? trimmed[0].ToString().ToUpperInvariant() : "?";
        DrawSquircle(g, rect, 12f, Ink(12), WithAlpha(Cyan, 0.35f), 1f);
        DrawFitted(g, initial, "Bahnschrift", 34f, FontStyle.Bold, Cyan, rect, Center, 18f);
    }

    private void DrawFh6DamagePanel(
        Graphics g,
        Snap snap,
        float x,
        float y,
        float width,
        float height)
    {
        var truck = snap.Active && snap.TruckDmg is { } truckValue ? (double?)truckValue : null;
        var trailer = snap.Active && snap.TrailerDmg is { } trailerValue ? (double?)trailerValue : null;
        var cargo = snap.Active && snap.CargoDmg is { } cargoValue ? (double?)cargoValue : null;
        var max = new[] { truck, trailer, cargo }
            .Where(v => v.HasValue).Select(v => v!.Value).DefaultIfEmpty(0).Max();
        var hasData = truck != null || trailer != null || cargo != null;

        var headerColor = !hasData
            ? Theme.TextDisabled
            : max >= 50 ? Red
            : max >= 20 ? Orange
            : max >= 5 ? Yellow
            : Green;

        var status = !hasData
            ? "SIN DATOS"
            : max >= 50 ? "CRÍTICO"
            : max >= 20 ? "REVISAR"
            : max >= 5 ? "LEVE"
            : "OK";

        DrawGlassPanel(g, x, y, width, height, Theme.RadiusPanel);

        DrawFitted(g, Localization.L("DAMAGE"), "Bahnschrift", 24f, FontStyle.Bold, headerColor,
            new RectangleF(x + 16f, y + 18f, width - 32f, 32f), Center, 14f);

        // Contenido reducido un 15 % lateralmente y centrado, sin alterar el panel.
        var contentWidth = (width - 48f) * 0.90f;
        var contentX = x + (width - contentWidth) / 2f;
        DrawDamageRow(g, contentX, y + 58f, contentWidth, "CAMIÓN", truck);
        DrawDamageRow(g, contentX, y + 90f, contentWidth, "TRÁILER", trailer);
        DrawDamageRow(g, contentX, y + 122f, contentWidth, "CARGA", cargo);

        // La imagen ocupa el resto del panel. Se centra dentro del área y
        // conserva un margen interior en los cuatro lados. No se modifica la
        // lógica de carga/fundido de la imagen.
        DrawFh6TruckImage(g, snap,
            new RectangleF(x + 24f, y + 146f, width - 48f, height - 158f));
    }

    private void DrawFh6TruckImage(Graphics g, Snap snap, RectangleF area)
    {
        if (!snap.Active)
        {
            ResetTruckImageFade();
            return;
        }

        var image = TruckImage(snap, out var key);
        if (image == null)
        {
            ResetTruckImageFade();
            DrawText(g, Localization.L("NO_IMAGE").ToUpperInvariant(), Mono(TypeScale.Caption, FontStyle.Bold),
                Theme.TextDisabled, area, TextFormat.Center);
            return;
        }

        var now = DateTime.UtcNow;
        if (!string.Equals(key, _truckImageKey, StringComparison.OrdinalIgnoreCase))
        {
            // La primera imagen aparece de inmediato; el fundido cruzado solo
            // se aplica cuando ya había un modelo visible.
            _truckImagePrevious = _truckImageCurrent;
            _truckImageCurrent = image;
            _truckImageKey = key;
            _truckImageFadeStartedUtc = now;
        }

        const float inset = 8f;
        var availableWidth = Math.Max(1f, area.Width - inset * 2f);
        var availableHeight = Math.Max(1f, area.Height - inset * 2f);
        var scale = Math.Min(availableWidth / image.Width, availableHeight / image.Height);
        var width = image.Width * scale;
        var height = image.Height * scale;
        var target = new RectangleF(
            area.X + (area.Width - width) / 2f,
            area.Y + (area.Height - height) / 2f,
            width,
            height);

        var fade = _truckImagePrevious == null
            ? 1f
            : (float)SmoothStep(
                (now - _truckImageFadeStartedUtc).TotalSeconds / TruckImageFadeSeconds);

        using (var clip = RoundedPath(target, 8f))
        {
            var state = g.Save();
            g.SetClip(clip);

            if (_truckImagePrevious is { } previous && fade < 1f)
            {
                DrawTruckImageAlpha(g, previous, target, 1f - fade);
            }

            DrawTruckImageAlpha(g, image, target, fade);

            // Degradado inferior: separa la imagen del fondo y da contraste al
            // nombre del modelo sin recurrir a un rectángulo opaco.
            var shadeHeight = target.Height * 0.42f;
            using (var shade = new LinearGradientBrush(
                       new RectangleF(target.X, target.Bottom - shadeHeight,
                           target.Width, shadeHeight),
                       WithAlpha(Color.Black, 0f),
                       WithAlpha(Color.Black, 0.78f),
                       LinearGradientMode.Vertical))
            {
                g.FillRectangle(shade, target.X, target.Bottom - shadeHeight,
                    target.Width, shadeHeight);
            }

            g.Restore(state);
        }

        if (fade >= 1f)
        {
            _truckImagePrevious = null;
        }

        var model = TruckReference(snap);
        DrawFitted(g, model, "Bahnschrift", TypeScale.Label, FontStyle.Bold,
            Color.FromArgb(255, 232, 238, 245),
            new RectangleF(target.X + 10f, target.Bottom - 26f,
                target.Width - 20f, 18f),
            Left, TypeScale.Caption);

        using (var border = new Pen(Ink(30), 1f))
        using (var path = RoundedPath(target, 8f))
        {
            g.DrawPath(border, path);
        }
    }

    private static void DrawTruckImageAlpha(
        Graphics g,
        Image image,
        RectangleF target,
        float alpha)
    {
        using var attributes = new ImageAttributes();
        attributes.SetColorMatrix(AlphaMatrix(Math.Clamp(alpha, 0f, 1f)));
        g.DrawImage(
            image,
            Rectangle.Round(target),
            0,
            0,
            image.Width,
            image.Height,
            GraphicsUnit.Pixel,
            attributes);
    }

    private void ResetTruckImageFade()
    {
        _truckImageKey = null;
        _truckImageCurrent = null;
        _truckImagePrevious = null;
    }

    private void ResetCargoImageFade()
    {
        _cargoImageKey = null;
        _cargoImageCurrent = null;
        _cargoImagePrevious = null;
    }

    private void DrawFh6CargoImage(Graphics g, Snap snap, RectangleF area)
    {
        if (!snap.Active || !snap.CargoLoaded)
        {
            ResetCargoImageFade();
            DrawText(g, "SIN IMAGEN DE CARGA", Mono(TypeScale.Caption, FontStyle.Bold),
                Theme.TextDisabled, area, TextFormat.Center);
            return;
        }

        var image = CargoImage(snap, out var key);
        if (image == null)
        {
            ResetCargoImageFade();
            DrawText(g, "SIN IMAGEN DE CARGA", Mono(TypeScale.Caption, FontStyle.Bold),
                Theme.TextDisabled, area, TextFormat.Center);
            return;
        }

        var now = DateTime.UtcNow;
        if (!string.Equals(key, _cargoImageKey, StringComparison.OrdinalIgnoreCase))
        {
            _cargoImagePrevious = _cargoImageCurrent;
            _cargoImageCurrent = image;
            _cargoImageKey = key;
            _cargoImageFadeStartedUtc = now;
        }

        const float inset = 8f;
        var availableWidth = Math.Max(1f, area.Width - inset * 2f);
        var availableHeight = Math.Max(1f, area.Height - inset * 2f);
        var scale = Math.Min(availableWidth / image.Width, availableHeight / image.Height);
        var width = image.Width * scale;
        var height = image.Height * scale;
        var target = new RectangleF(
            area.X + (area.Width - width) / 2f,
            area.Y + (area.Height - height) / 2f,
            width,
            height);

        var fade = _cargoImagePrevious == null
            ? 1f
            : (float)SmoothStep(
                (now - _cargoImageFadeStartedUtc).TotalSeconds / TruckImageFadeSeconds);

        using (var clip = RoundedPath(target, 8f))
        {
            var state = g.Save();
            g.SetClip(clip);

            if (_cargoImagePrevious is { } previous && fade < 1f)
            {
                DrawTruckImageAlpha(g, previous, target, 1f - fade);
            }

            DrawTruckImageAlpha(g, image, target, fade);

            var shadeHeight = target.Height * 0.42f;
            using (var shade = new LinearGradientBrush(
                       new RectangleF(target.X, target.Bottom - shadeHeight,
                           target.Width, shadeHeight),
                       WithAlpha(Color.Black, 0f),
                       WithAlpha(Color.Black, 0.78f),
                       LinearGradientMode.Vertical))
            {
                g.FillRectangle(shade, target.X, target.Bottom - shadeHeight,
                    target.Width, shadeHeight);
            }

            g.Restore(state);
        }

        if (fade >= 1f)
        {
            _cargoImagePrevious = null;
        }

        var label = snap.CargoName ?? "CARGA";
        DrawFitted(g, label, "Bahnschrift", TypeScale.Label, FontStyle.Bold,
            Color.FromArgb(255, 232, 238, 245),
            new RectangleF(target.X + 10f, target.Bottom - 26f,
                target.Width - 20f, 18f),
            Left, TypeScale.Caption);

        using (var border = new Pen(Ink(30), 1f))
        using (var path = RoundedPath(target, 8f))
        {
            g.DrawPath(border, path);
        }
    }

    private void DrawFh6GpsPanel(
        Graphics g,
        Snap snap,
        float x,
        float y,
        float width,
        float height)
    {
        var active = snap.Active;
        var heading = active && snap.Heading is { } headingValue
            ? (double?)((headingValue % 1.0 + 1.0) % 1.0)
            : null;
        var bearing = heading == null ? (double?)null : ((1.0 - heading.Value) * 360.0 % 360.0 + 360.0) % 360.0;
        var accent = active ? Cyan : Theme.TextDisabled;
        var destination = active && snap.OnJob && !string.IsNullOrWhiteSpace(snap.CityDst)
            ? snap.CityDst!
            : "SIN DESTINO";
        var origin = active && !string.IsNullOrWhiteSpace(snap.CitySrc)
            ? snap.CitySrc!
            : "SIN ORIGEN";
        var trip = BuildTrip(snap);

        DrawGlassPanel(g, x, y, width, height, Theme.RadiusPanel);

        // Tinte sutil segun el estado de la ruta.
        if (active)
        {
            var tintColor = snap.OnJob ? Green : Cyan;
            using (var brush = new SolidBrush(Color.FromArgb(14, tintColor.R, tintColor.G, tintColor.B)))
            using (var path = RoundedPath(new RectangleF(x, y, width, height), Theme.RadiusPanel))
            {
                g.FillPath(brush, path);
            }
        }

        var gpsStatusColor = !active ? Theme.TextDisabled
            : snap.OnJob ? WithAlpha(Green, 0.82f)
            : Theme.TextDisabled;
        DrawFitted(g, Localization.L("GPS"), "Bahnschrift", 24f, FontStyle.Bold, gpsStatusColor,
            new RectangleF(x + 16f, y + 18f, width - 32f, 32f), Center, 14f);

        var speedKmh = active && snap.Speed is { } speed ? speed * 3.6 : 0;
        var limitKmh = active && snap.Limit is { } limit && limit > 0.5f
            ? limit * 3.6
            : (double?)null;
        if (limitKmh is { } shownLimit)
        {
            DrawSpeedLimitSign(g, shownLimit, x + width - 16f, y + 68f, 88f);
        }

        DrawCompass(g, x + 14f, y + 64f, 104f, bearing, accent);

        var centerX = x + width / 2f;
        var columnX = x + 122f;
        const float columnWidth = 156f;

        DrawTracked(g, "DESTINO", Rounded(TypeScale.Caption, FontStyle.Bold), Theme.TextTertiary,
            centerX, y + 86f, 1.2f, Center);
        DrawFitted(g, destination, "Bahnschrift", 20f, FontStyle.Bold, Theme.TextPrimary,
            new RectangleF(columnX, y + 91f, columnWidth, 26f), Center, TypeScale.Caption);

        if (ShowDetails)
        {
            DrawTracked(g, "ORIGEN", Rounded(TypeScale.Caption, FontStyle.Bold), Theme.TextTertiary,
                centerX, y + 124f, 1.2f, Center);
            DrawFitted(g, origin, "Bahnschrift", 20f, FontStyle.Bold, Theme.TextPrimary,
                new RectangleF(columnX, y + 130f, columnWidth, 24f), Center, TypeScale.Caption);

            DrawTracked(g, "FALTAN", Rounded(TypeScale.Caption, FontStyle.Bold), Theme.TextTertiary,
                x + 20f, y + 185f, 1.2f);
            var remaining = trip?.Remaining ?? "--";
            var remainingFont = Rounded(34f, FontStyle.Bold);
            DrawText(g, remaining, remainingFont, trip == null ? Theme.TextDisabled : Green,
                new RectangleF(x + 20f, y + 191f, 158f, 44f), Left);
            DrawText(g, "km", Rounded(TypeScale.Caption, FontStyle.Bold), Theme.TextTertiary,
                new RectangleF(x + 24f + g.MeasureString(remaining, remainingFont).Width,
                    y + 205f, 40f, 18f), Left);

            DrawTracked(g, "RECORRIDO", Rounded(TypeScale.Caption, FontStyle.Bold), Theme.TextTertiary,
                x + width - 20f, y + 185f, 1.2f, Right);
            DrawText(g, trip == null ? "-- / -- km" : $"{trip.Travelled} / {trip.Planned} km",
                Mono(13f, FontStyle.Bold), trip == null ? Theme.TextDisabled : Theme.TextSecondary,
                new RectangleF(x + 190f, y + 193f, width - 210f, 22f), Right);

            var progress = RouteProgress(snap);
            DrawTracked(g, "PROGRESO DE RUTA", Rounded(TypeScale.Caption, FontStyle.Bold), Theme.TextTertiary,
                x + 20f, y + 270f, 1.2f);
            DrawText(g, progress > 0
                    ? $"{Math.Round(progress * 100).ToString("0", Es)}%"
                    : "--",
                Mono(TypeScale.Caption, FontStyle.Bold), Theme.TextSecondary,
                new RectangleF(x + width - 170f, y + 261f, 150f, 18f), Right);
            DrawProgressBar(g, x + 20f, y + 292f, width - 40f, 8f, progress, accent);

            var route = active && snap.OnJob && !string.IsNullOrWhiteSpace(snap.CitySrc)
                        && !string.IsNullOrWhiteSpace(snap.CityDst)
                ? $"{snap.CitySrc}  →  {snap.CityDst}"
                : "--";
            DrawFitted(g, route, "Segoe UI", TypeScale.Label, FontStyle.Regular, Theme.TextTertiary,
                new RectangleF(x + 20f, y + 330f, width - 40f, 20f), Left, TypeScale.Caption);
        }
        else
        {
            // Tres datos primarios: faltan, llegada y progreso. La brújula y el
            // destino quedan como contexto visual.
            DrawTracked(g, "FALTAN", Rounded(TypeScale.Caption, FontStyle.Bold), Theme.TextTertiary,
                x + 20f, y + 185f, 1.2f);
            var remaining = trip?.Remaining ?? "--";
            var remainingFont = Rounded(34f, FontStyle.Bold);
            DrawText(g, remaining, remainingFont, trip == null ? Theme.TextDisabled : Green,
                new RectangleF(x + 20f, y + 191f, 158f, 44f), Left);
            DrawText(g, "km", Rounded(TypeScale.Caption, FontStyle.Bold), Theme.TextTertiary,
                new RectangleF(x + 24f + g.MeasureString(remaining, remainingFont).Width,
                    y + 205f, 40f, 18f), Left);

            var progress = RouteProgress(snap);
            DrawTracked(g, "PROGRESO DE RUTA", Rounded(TypeScale.Caption, FontStyle.Bold), Theme.TextTertiary,
                x + 20f, y + 270f, 1.2f);
            DrawText(g, progress > 0
                    ? $"{Math.Round(progress * 100).ToString("0", Es)}%"
                    : "--",
                Mono(TypeScale.Caption, FontStyle.Bold), Theme.TextSecondary,
                new RectangleF(x + width - 170f, y + 261f, 150f, 18f), Right);
            DrawProgressBar(g, x + 20f, y + 292f, width - 40f, 8f, progress, accent);

            var route = active && snap.OnJob && !string.IsNullOrWhiteSpace(snap.CitySrc)
                        && !string.IsNullOrWhiteSpace(snap.CityDst)
                ? $"{snap.CitySrc}  →  {snap.CityDst}"
                : "--";
            DrawFitted(g, route, "Segoe UI", TypeScale.Label, FontStyle.Regular, Theme.TextTertiary,
                new RectangleF(x + 20f, y + 330f, width - 40f, 20f), Left, TypeScale.Caption);
        }
    }

    private static double RouteProgress(Snap snap)
    {
        return snap.Active && snap.OnJob && snap.Planned is { } planned && planned > 0
               && snap.RouteD is { } remainingDistance
            ? Math.Clamp(1.0 - remainingDistance / (planned * 1000.0), 0, 1)
            : 0;
    }

    private void DrawFh6FuelCard(
        Graphics g,
        Snap snap,
        double ignitionElapsed,
        float x,
        float y,
        float width,
        float height)
    {
        DrawGlassPanel(g, x, y, width, height, Theme.RadiusCard);

        var fuel = snap.Active && snap.Fuel is { } fuelValue ? (double?)fuelValue : null;
        var capacity = snap.Active && snap.FuelCap is { } cap && cap > 0 ? (double?)cap : null;
        var targetFraction = fuel.HasValue && capacity.HasValue && capacity > 0
            ? Math.Clamp(fuel.Value / capacity.Value, 0, 1)
            : 0;
        var fraction = AnimatedFraction(SmoothTarget("fuel", targetFraction), ignitionElapsed);
        var lowFuel = snap.FuelWarn || (snap.Active && targetFraction < 0.12);

        var color = !snap.Active || fuel == null || capacity == null
            ? Theme.TextDisabled
            : lowFuel ? Red
            : targetFraction < 0.30 ? Orange
            : Green;

        // Tinte sutil segun el nivel de combustible.
        if (snap.Active)
        {
            using (var brush = new SolidBrush(Color.FromArgb(20, color.R, color.G, color.B)))
            using (var path = RoundedPath(new RectangleF(x, y, width, height), Theme.RadiusCard))
            {
                g.FillPath(brush, path);
            }
        }

        DrawFuelIcon(g, x + width - 68f, y + height / 2f - 43.8f, 41.6f, color);

        DrawFitted(g, fuel == null || capacity == null
                ? "--"
                : $"{Math.Round(fraction * 100).ToString("0", Es)}%",
            "Bahnschrift", 52f, FontStyle.Bold, color,
            new RectangleF(x + 20f, y + 20f, width - 92f, height - 70f), Center, 28f);

        DrawFitted(g, snap.Active && snap.Range is { } range
                ? $"AUT. {Math.Round(ToDisplayDistance(range)).ToString("N0", Es)} {(Imperial ? "MI" : "KM")}"
                : $"AUT. -- {(Imperial ? "MI" : "KM")}",
            "Consolas", TypeScale.Body, FontStyle.Bold, Theme.TextTertiary,
            new RectangleF(x + 20f, y + height - 52f, width - 40f, 22f), Center, 12f);

        var barY = y + height - 22f;

        DrawProgressBar(g, x + 24f, barY, width - 48f, 7.2f, fraction, color);

        var reserveX = x + 24f + (width - 48f) * 0.12f;
        using (var reserve = new Pen(WithAlpha(Red, 0.75f), 2f))
        {
            g.DrawLine(reserve, reserveX, barY - 4f, reserveX, barY + 10f);
        }
    }

    private void DrawFh6VerticalGauges(
        Graphics g,
        Snap snap,
        bool ignitionOn,
        double ignitionElapsed,
        DashboardLayout layout)
    {
        var water = ignitionOn && snap.WaterTemp is { } waterTemp && waterTemp > 0
            ? (double?)waterTemp
            : null;
        var air = ignitionOn && snap.AirPressure is { } airPressure && airPressure >= 0
            ? (double?)airPressure
            : null;

        var waterTarget = water == null ? 0 : Math.Clamp((water.Value - 40) / 80, 0, 1);
        var airTarget = air == null ? 0 : Math.Clamp(air.Value / 120, 0, 1);
        var waterFraction = water == null ? 0 : AnimatedFraction(SmoothTarget("water", waterTarget), ignitionElapsed);
        var airFraction = air == null ? 0 : AnimatedFraction(SmoothTarget("air", airTarget), ignitionElapsed);

        DrawFh6VerticalGauge(
            g, layout.Water.X, layout.Water.Y, layout.Water.Width, layout.Water.Height, ignitionOn,
            "TEMPERATURA",
            water == null
                ? "--"
                : Math.Round(ToDisplayTemperature(40 + 80 * waterFraction)).ToString("0", Es),
            TemperatureUnit, waterFraction,
            WaterTemperatureColor(water), Imperial ? "248" : "120", Imperial ? "104" : "40");

        DrawFh6VerticalGauge(
            g, layout.Air.X, layout.Air.Y, layout.Air.Width, layout.Air.Height, ignitionOn,
            "PRESIÓN",
            air == null
                ? "--"
                : Imperial
                    ? Math.Round(120 * airFraction).ToString("0", Es)
                    : (120 * airFraction / 14.5037738).ToString("0.0", Es),
            PressureUnit, airFraction,
            AirPressureColor(air), Imperial ? "120" : "8.3", "0");
    }

    private void DrawFh6VerticalGauge(
        Graphics g,
        float x,
        float y,
        float width,
        float height,
        bool powered,
        string caption,
        string valueText,
        string unit,
        double fraction,
        Color accent,
        string maxText,
        string minText)
    {
        DrawGlassPanel(g, x, y, width, height, 18f);

        // Con el motor apagado solo queda el marco; no se dibujan escala ni lectura.
        if (!powered)
        {
            return;
        }

        var barWidth = 10f;
        var barX = x + width / 2f - barWidth / 2f;
        var trackTop = y + 18f;
        var trackBottom = y + height - 72f;
        var trackHeight = trackBottom - trackTop;

        // Track simple.
        DrawRounded(g, new RectangleF(barX, trackTop, barWidth, trackHeight),
            barWidth / 2f, Ink(24), Color.Transparent, 0f);

        var fillHeight = (float)(trackHeight * Math.Clamp(fraction, 0, 1));
        if (fillHeight > 0.5f)
        {
            DrawRounded(g, new RectangleF(barX, trackBottom - fillHeight, barWidth, fillHeight),
                barWidth / 2f, accent, Color.Transparent, 0f);
        }

        DrawHairline(g, x + 16f, y + height - 62f, x + width - 16f, 8);

        DrawFitted(g, valueText, "Bahnschrift", 32f, FontStyle.Bold, accent,
            new RectangleF(x + 4f, y + height - 62f, width - 8f, 36f), Center, 18f);
        DrawText(g, unit, Rounded(TypeScale.Caption, FontStyle.Bold), Theme.TextSecondary,
            new RectangleF(x, y + height - 24f, width, 16f), Center);
    }

    private void DrawFh6ReservedPanel(
        Graphics g,
        Snap snap,
        float x,
        float y,
        float width,
        float height)
    {
        DrawGlassPanel(g, x, y, width, height, Theme.RadiusCard);

        // Tinte según el tipo de aviso activo; se desvanece con el aviso.
        if (_currentMessage is { } && _messageOpacity > 0.01f)
        {
            var kind = _currentMessage.Value.Kind;
            var tint = kind switch
            {
                NoticeKind.Income => Color.FromArgb(255, 0, 200, 80),
                NoticeKind.Expense => Color.FromArgb(255, 220, 40, 40),
                NoticeKind.Route => Color.FromArgb(255, 0, 200, 220),
                _ => Color.FromArgb(255, 255, 133, 26)
            };
            var alpha = (int)Math.Clamp(_messageOpacity * 40f, 0f, 40f);
            using (var brush = new SolidBrush(Color.FromArgb(alpha, tint.R, tint.G, tint.B)))
            using (var path = RoundedPath(new RectangleF(x, y, width, height), Theme.RadiusCard))
            {
                g.FillPath(brush, path);
            }
        }

        if (_currentMessage is { } message && _messageOpacity > 0.01f)
        {
            var alpha = _messageOpacity;
            var color = message.Kind switch
            {
                NoticeKind.Income => Green,
                NoticeKind.Expense => Red,
                NoticeKind.Route => Cyan,
                _ => Orange
            };
            var (header, value) = SplitNotice(message.Text);
            if (value.Length == 0)
            {
                DrawFitted(g, header, "Bahnschrift", 26f, FontStyle.Bold,
                    WithAlpha(color, 0.96f * alpha),
                    new RectangleF(x + 16f, y + 16f, width - 32f, height - 32f), Center, 16f);
            }
            else
            {
                DrawFitted(g, header, "Bahnschrift", 18f, FontStyle.Bold,
                    WithAlpha(color, 0.96f * alpha),
                    new RectangleF(x + 16f, y + 14f, width - 32f, 28f), Center, 11f);
                DrawFitted(g, value, "Bahnschrift", 40f, FontStyle.Bold,
                    WithAlpha(color, 0.96f * alpha),
                    new RectangleF(x + 16f, y + 42f, width - 32f, height - 56f), Center, 20f);
            }
        }
        else
        {
            DrawEmptyState(g, new RectangleF(x + 20f, y + 20f, width - 40f, height - 40f),
                "SIN AVISOS", "Todo en orden");
        }
    }

    private void DrawFh6WeatherCard(
        Graphics g,
        Snap snap,
        float x,
        float y,
        float width,
        float height)
    {
        DrawGlassPanel(g, x, y, width, height, Theme.RadiusCard);

        var minutes = snap.Active && snap.GameTime is { } gameTime ? (int?)gameTime : null;
        var isDay = minutes == null || ((minutes.Value / 60) % 24) is >= 6 and < 20;
        var condition = snap.Active ? snap.Weather : WeatherCondition.Clear;
        var conditionText = !snap.Active
            ? "SIN DATOS"
            : condition switch
            {
                WeatherCondition.Clear => "DESPEJADO",
                WeatherCondition.Cloudy => "NUBLADO",
                WeatherCondition.Rain => "LLUVIA",
                _ => "TORMENTA"
            };
        var accent = !snap.Active
            ? Theme.TextDisabled
            : condition switch
            {
                WeatherCondition.Clear => isDay ? Yellow : MoonLight,
                WeatherCondition.Cloudy => Theme.TextSecondary,
                WeatherCondition.Rain => Cyan,
                _ => Yellow
            };

        // Tinte sutil segun la hora del dia.
        if (snap.Active)
        {
            var tint = isDay
                ? Color.FromArgb(16, 255, 214, 56)   // dia: ambar suave
                : Color.FromArgb(20, 64, 107, 255);  // noche: azul
            using (var brush = new SolidBrush(tint))
            using (var path = RoundedPath(new RectangleF(x, y, width, height), Theme.RadiusCard))
            {
                g.FillPath(brush, path);
            }
        }

        DrawCompactWeatherIcon(g, condition, isDay, x + width / 2f - 36f, y + 8f, 72f, accent, snap.Active);

        DrawFitted(g, conditionText, "Bahnschrift", 22f, FontStyle.Bold,
            snap.Active ? accent : Theme.TextDisabled,
            new RectangleF(x + 20f, y + 80f, width - 40f, 24f), Center, TypeScale.Caption);

        var timeText = minutes == null
            ? "--:--"
            : $"{((minutes.Value / 60) % 24):00}:{minutes.Value % 60:00}";
        DrawText(g, timeText, Mono(30f, FontStyle.Bold),
            snap.Active ? Theme.TextPrimary : Theme.TextDisabled,
            new RectangleF(x + 20f, y + 102f, width - 40f, 32f), Center);
    }

    private void DrawFh6Footer(Graphics g, Snap snap, DateTime now, DashboardLayout layout)
    {
        var textY = layout.Footer.Y + 24f;
        DrawHairline(g, layout.Footer.X + 64f, layout.Footer.Y,
            layout.Footer.Right - 64f, 14);

        var left = $"{AppInfo.Name} {AppInfo.Version}  ·  {ProfileLabel(snap)}";
        DrawText(g, left, Ui(TypeScale.Caption), Theme.TextTertiary,
            new RectangleF(layout.Footer.X + 64f, textY, 900f, 20f), Left);

        var (statusText, statusColor) = Status(snap);
        var right = $"{statusText}  ·  {SyncLabel(now)}";
        DrawText(g, right, Mono(TypeScale.Caption, FontStyle.Bold), WithAlpha(statusColor, 0.80f),
            new RectangleF(layout.Footer.Right - 464f, textY, 400f, 20f), Right);
    }

    private void DrawGlassPanel(
        Graphics g,
        float x,
        float y,
        float width,
        float height,
        float radius)
    {
        if (_usingPanelLayer)
        {
            return;
        }

        DrawGlassPanelCore(g, x, y, width, height, radius);
    }

    private static void DrawGlassPanelCore(
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
    }

    private static void DrawHairline(Graphics g, float x1, float y, float x2, int alpha)
    {
        using var pen = new Pen(Ink(alpha), 1f);
        g.DrawLine(pen, x1, y, x2, y);
    }

    private static long? Net(Snap snap)
    {
        if (!snap.Active)
        {
            return null;
        }

        var income = Income(snap);
        var expenses = Expenses(snap);
        return income is { } i && expenses is { } e ? i - e : null;
    }

    // ---------------------------------------------------------------------
    // Header / footer
    // ---------------------------------------------------------------------

    private void DrawHeader(Graphics g, Snap snap)
    {
        var (statusText, statusColor) = Status(snap);

        using (var dot = new SolidBrush(statusColor))
        using (var glow = new SolidBrush(Color.FromArgb(50, statusColor)))
        {
            g.FillEllipse(glow, 68f, 44f, 17f, 17f);
            g.FillEllipse(dot, 72f, 48f, 9f, 9f);
        }

        var atsFont = Rounded(24f, FontStyle.Bold);
        var atsWidth = MeasureTracked(g, "ATS", atsFont, 8f);
        DrawTracked(g, "ATS", atsFont, Theme.TextPrimary, 93f, 53f, 8f);

        DrawTracked(
            g,
            "TELEMETRÍA",
            Rounded(15f, FontStyle.Bold),
            Theme.TextTertiary,
            93f + atsWidth + 14f,
            53f,
            4f);

        var truck = TruckReference(snap);
        var displacement = EngineDisplacement(snap.Active ? snap.TruckId ?? snap.TruckName : null);

        // El logotipo de la marca acompaña al nombre, ambos centrados como un
        // único bloque. El logo ocupa la altura de la línea del nombre.
        var nameFont = Rounded(34f, FontStyle.Bold);
        var subFont = Rounded(21f, FontStyle.Regular);
        var logo = BrandLogoAsset(snap) is { } logoAsset ? BrandLogo(logoAsset) : null;
        var logoWidth = logo == null ? 0f : BrandLogoHeight * logo.Width / logo.Height;
        var logoGap = logo == null ? 0f : BrandLogoGap;
        var nameWidth = g.MeasureString(truck, nameFont).Width;
        var subWidth = displacement == null ? 0f : g.MeasureString(displacement, subFont).Width;
        var textWidth = Math.Max(nameWidth, subWidth) + 2f;
        var groupWidth = logoWidth + logoGap + textWidth;
        var groupLeft = 960f - groupWidth / 2f;
        var textLeft = groupLeft + logoWidth + logoGap;

        if (logo != null)
        {
            DrawBrandLogo(g, logo, new RectangleF(
                groupLeft,
                39f - BrandLogoHeight / 2f,
                logoWidth,
                BrandLogoHeight));
        }

        // Nombre del camión e información adicional al 200 % del tamaño original.
        DrawText(g, truck, nameFont, Theme.TextPrimary,
            new RectangleF(textLeft, 18f, textWidth, 42f), Center);

        if (displacement != null)
        {
            DrawText(g, displacement, subFont, Theme.TextSecondary,
                new RectangleF(textLeft, 61f, textWidth, 28f), Center);
        }

        // El nombre del monitor ya no se dibuja en la vista principal: vive en
        // Ajustes → Diagnóstico. La cabecera reserva ese espacio para el estado.
        if (snap.Demo)
        {
            // El modo demo se marca con un chip visible en la cabecera, no solo
            // en el pie, para que no se confunda con datos reales.
            DrawChip(g, new RectangleF(1786f, 40f, 64f, 24f), "DEMO", Cyan, true);
        }
        else
        {
            var statusFont = Rounded(14f, FontStyle.Bold);
            var statusWidth = g.MeasureString(statusText, statusFont).Width;
            DrawText(g, statusText, statusFont, Theme.TextSecondary,
                new RectangleF(1850f - statusWidth, 38f, statusWidth + 2f, 30f), Right);
        }
    }

    private void DrawFooter(Graphics g, Snap snap)
    {
        var left = $"{AppInfo.Name} {AppInfo.Version}  ·  {ProfileLabel(snap)}";
        DrawText(g, left, Ui(12f, FontStyle.Bold), Theme.TextTertiary,
            new RectangleF(70f, 1010f, 900f, 30f), Left);

        var (statusText, statusColor) = Status(snap);
        var right = $"{statusText}  ·  {SyncLabel(DateTime.UtcNow)}";
        DrawText(g, right, Mono(11f, FontStyle.Bold), WithAlpha(statusColor, 0.80f),
            new RectangleF(1350f, 1010f, 500f, 30f), Right);
    }

    private static (string Text, Color Color) Status(Snap snap)
    {
        if (snap.Demo)
        {
            return ("DEMO", Cyan);
        }
        if (!snap.Active)
        {
            return ("SIN SEÑAL", Orange);
        }
        if (snap.Paused)
        {
            return ("PAUSA", Yellow);
        }
        return ("EN VIVO", Green);
    }

    private static string GameLabel(Snap snap)
    {
        if (snap.Demo)
        {
            return "DEMO";
        }

        if (!snap.Active)
        {
            return "SIN JUEGO";
        }

        return snap.Ats ? "ATS" : "ETS2";
    }

    private static string ProfileLabel(Snap snap)
    {
        if (!string.IsNullOrWhiteSpace(snap.CompanyName))
        {
            return snap.CompanyName.Trim().ToUpperInvariant();
        }

        return snap.Demo ? "DEMO" : "SIN PERFIL";
    }

    private string SyncLabel(DateTime now)
    {
        if (_lastSyncUtc is not { } last)
        {
            return "SIN SINCRONIZAR";
        }

        var seconds = Math.Max(0, (int)(now - last).TotalSeconds);
        if (seconds < 2)
        {
            return "SINC. AHORA";
        }

        if (seconds < 60)
        {
            return $"SINC. HACE {seconds} S";
        }

        return $"SINC. {last.ToLocalTime():HH:mm:ss}";
    }

    private static string TruckReference(Snap snap)
    {
        if (!snap.Active)
        {
            return "SIN CAMIÓN";
        }

        var brand = snap.TruckBrand?.Trim();
        var name = snap.TruckName?.Trim();

        if (!string.IsNullOrWhiteSpace(brand) && !string.IsNullOrWhiteSpace(name))
        {
            return name.Contains(brand, StringComparison.OrdinalIgnoreCase)
                ? name.ToUpperInvariant()
                : $"{brand} {name}".ToUpperInvariant();
        }

        return (name ?? brand ?? "SIN CAMIÓN").ToUpperInvariant();
    }

    private static string? BrandLogoAsset(Snap snap)
    {
        if (!snap.Active)
        {
            return null;
        }

        var raw = snap.TruckBrand ?? snap.TruckId ?? snap.TruckName;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "modded";
        }

        var key = new string(raw.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        if (key.Length == 0)
        {
            return "modded";
        }

        if (key.Contains("kenworth")) return "kenworth";
        if (key.Contains("peterbilt")) return "peterbilt";
        if (key.Contains("freightliner")) return "freightliner";
        if (key.Contains("international") || key.Contains("intnational")) return "international";
        if (key.Contains("westernstar")) return "westernstar";
        if (key.Contains("mack")) return "mack";
        if (key.Contains("volvo")) return "volvo";
        if (key.Contains("scania")) return "scania";
        if (key.Contains("mercedes")) return "mercedes";
        if (key.Contains("daf")) return "daf";
        if (key.Contains("iveco")) return "iveco";
        if (key.Contains("renault")) return "renault";
        if (key.Contains("man")) return "man";
        return "modded";
    }

    private static string? EngineDisplacement(string? value)
    {
        var raw = value?.ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        if (raw.Contains("t800")) return "11.9L";
        if (raw.Contains("w900")) return "14.9L";
        if (raw.Contains("t680")) return "12.9L";
        if (raw.Contains("389")) return "12.9L";
        if (raw.Contains("579")) return "12.9L";
        if (raw.Contains("cascadia")) return "12.8L";
        if (raw.Contains("49x")) return "12.8L";
        if (raw.Contains("5700")) return "12.8L";
        if (raw.Contains("57x")) return "12.8L";
        if (raw.Contains("anthem")) return "12.8L";
        if (raw.Contains("pinnacle")) return "12.8L";
        if (raw.Contains("vnl")) return "12.8L";
        if (raw.Contains("lonestar")) return "12.4L";
        if (raw.Contains("9900")) return "14.9L";
        if (raw.Contains(".lt") || raw == "lt") return "12.4L";
        return null;
    }

    // ---------------------------------------------------------------------
    // Indicator row
    // ---------------------------------------------------------------------

    private void DrawIndicatorRow(Graphics g, Snap snap, float x, float y)
    {
        var indicators = new (string Asset, bool Active, Color Color, bool Pulses)[]
        {
            ("beacon-c", snap.Beacon, Yellow, true),
            ("side-markers-c", snap.Parking, Green, false),
            ("low-beam-c", snap.LowB, Green, false),
            ("high-beam-c", snap.HighB, Blue, false),
            ("brake-c1", snap.Brake, Red, false),
            ("turn-lights-left-c", snap.BlinkL, Green, false),
            ("turn-lights-right-c", snap.BlinkR, Green, false),
            ("parking-brake-2-c", snap.Park, Red, true),
            ("retarder-c", snap.Retarder, Green, false),
            ("engine-brake-round", snap.Motor, Green, false),
            ("wiper-c", snap.Wipers > 0, Cyan, true),
            ("diff-lock-c", snap.DiffLock, Orange, true)
        };

        const float iconSize = 40f;
        const float spacing = 22f;
        var total = indicators.Length * iconSize + (indicators.Length - 1) * spacing;
        var startX = x + (1224f - total) / 2f;
        var iconY = y + 3f;

        // Encuadre de los 12 testigos, con el mismo trazo que los paneles.
        const float framePaddingX = 22f;
        const float framePaddingY = 14f;
        DrawPanelFrame(
            g,
            startX - framePaddingX,
            iconY - framePaddingY,
            total + framePaddingX * 2f,
            iconSize + framePaddingY * 2f,
            18f);

        for (var i = 0; i < indicators.Length; i++)
        {
            var item = indicators[i];
            var ix = startX + i * (iconSize + spacing);
            var rect = new RectangleF(ix, iconY, iconSize, iconSize);
            DrawIcon(g, item.Asset, rect, item.Active, item.Color, item.Pulses);
        }
    }

    private void DrawIcon(Graphics g, string asset, RectangleF rect, bool active, Color color, bool pulses)
    {
        var image = Icon(asset);
        if (image == null)
        {
            return;
        }

        var opacity = 1f;

        if (active && pulses && !ReduceMotion)
        {
            var phase = (Math.Sin(DateTime.UtcNow.TimeOfDay.TotalSeconds * 2 * Math.PI / 1.05) + 1) / 2;
            opacity = (float)(0.60 + 0.40 * phase);
        }

        using var attributes = new ImageAttributes();

        attributes.SetColorMatrix(active
            ? AlphaMatrix(opacity)
            : ColorTintMatrix(Theme.TextPrimary, InactiveIndicatorOpacity));

        g.DrawImage(
            image,
            Rectangle.Round(rect),
            0,
            0,
            image.Width,
            image.Height,
            GraphicsUnit.Pixel,
            attributes);
    }

    private void DrawTintedIcon(Graphics g, string asset, RectangleF rect, Color color, float opacity = 1f)
    {
        var image = Icon(asset);
        if (image == null)
        {
            return;
        }

        using var attributes = new ImageAttributes();
        attributes.SetColorMatrix(ColorTintMatrix(color, opacity));
        g.DrawImage(
            image,
            Rectangle.Round(rect),
            0,
            0,
            image.Width,
            image.Height,
            GraphicsUnit.Pixel,
            attributes);
    }

    private static void DrawBrandLogo(Graphics g, Image image, RectangleF rect)
    {
        using var attributes = new ImageAttributes();
        attributes.SetColorMatrix(ColorTintMatrix(Theme.TextPrimary, 0.88f));
        g.DrawImage(
            image,
            Rectangle.Round(rect),
            0,
            0,
            image.Width,
            image.Height,
            GraphicsUnit.Pixel,
            attributes);
    }

    private static void DrawManometerIcon(Graphics g, RectangleF rect, Color color)
    {
        var width = rect.Width;
        var height = rect.Height;
        var diameter = Math.Min(width, height) * 0.82f;
        var cx = rect.Left + width / 2f;
        var cy = rect.Top + height / 2f;
        var radius = diameter / 2f;
        var stroke = Math.Max(1.2f, width * 0.075f);

        using var pen = new Pen(color, stroke)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };

        g.DrawEllipse(pen, cx - radius, cy - radius, diameter, diameter);

        for (var i = 0; i < 3; i++)
        {
            var angle = -90.0 + i * 45.0;
            var start = PointAt(cx, cy, radius * 0.94f, angle);
            var end = PointAt(cx, cy, radius * 0.68f, angle);
            g.DrawLine(pen, start.X, start.Y, end.X, end.Y);
        }

        var needle = PointAt(cx, cy, radius * 0.62f, -38.0);
        g.DrawLine(pen, cx, cy, needle.X, needle.Y);

        using var hub = new SolidBrush(color);
        var hubRadius = Math.Max(1.2f, width * 0.055f);
        g.FillEllipse(hub, cx - hubRadius, cy - hubRadius, hubRadius * 2f, hubRadius * 2f);
    }

    private static void DrawThermometerIcon(Graphics g, RectangleF rect, Color color)
    {
        var width = rect.Width;
        var height = rect.Height;
        var cx = rect.Left + width / 2f;
        var stroke = Math.Max(1.2f, width * 0.075f);
        var bulbRadius = width * 0.24f;
        var bulbCy = rect.Bottom - bulbRadius - height * 0.04f;
        var stemWidth = width * 0.26f;
        var stemTop = rect.Top + height * 0.06f;
        var stemRect = new RectangleF(
            cx - stemWidth / 2f,
            stemTop,
            stemWidth,
            bulbCy - stemTop + bulbRadius * 0.40f);

        // Tubo y bulbo del termómetro.
        using (var pen = new Pen(color, stroke))
        {
            using var stem = SquirclePath(stemRect, stemWidth / 2f);
            g.DrawPath(pen, stem);
            g.DrawEllipse(pen, cx - bulbRadius, bulbCy - bulbRadius, bulbRadius * 2f, bulbRadius * 2f);
        }

        // Depósito lleno y columna.
        using (var brush = new SolidBrush(color))
        {
            var fillRadius = bulbRadius - stroke * 0.40f;
            g.FillEllipse(brush, cx - fillRadius, bulbCy - fillRadius, fillRadius * 2f, fillRadius * 2f);
        }

        using (var column = new Pen(color, Math.Max(1.1f, width * 0.080f)))
        {
            g.DrawLine(column, cx, rect.Top + height * 0.36f, cx, bulbCy);
        }

        // Escala lateral del testigo.
        using var tick = new Pen(WithAlpha(color, 0.75f), Math.Max(1f, width * 0.055f))
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        for (var i = 0; i < 3; i++)
        {
            var ty = rect.Top + height * (0.20f + i * 0.15f);
            g.DrawLine(tick, cx + stemWidth * 0.55f + width * 0.05f, ty, cx + width * 0.34f, ty);
        }
    }

    // ---------------------------------------------------------------------
    // Trip
    // ---------------------------------------------------------------------

    private void DrawTripBadge(Graphics g, Snap snap, float x, float y, float width, float height)
    {
        // Fondo verde tenue al 5 % para dar sensación de pantalla sin restar legibilidad.
        DrawSquircle(g, new RectangleF(x, y, width, height), 18f,
            WithAlpha(Green, 0.05f), WithAlpha(Cyan, 0.16f), 1f);

        var trip = BuildTrip(snap);
        if (trip != null)
        {
            var remainingFont = Rounded(24f, FontStyle.Bold);
            var unitFont = Rounded(11f, FontStyle.Regular);
            var travelledFont = Rounded(14f, FontStyle.Bold);
            var plannedFont = Rounded(8.5f, FontStyle.Regular);
            var labelFont = Rounded(6.8f, FontStyle.Bold);

            var remainingW = g.MeasureString(trip.Remaining, remainingFont).Width
                             + 4f
                             + g.MeasureString("km", unitFont).Width;
            var travelledText = $"{trip.Travelled} km";
            var plannedText = $"de {trip.Planned} km";
            var rightW = Math.Max(
                g.MeasureString(travelledText, travelledFont).Width,
                g.MeasureString(plannedText, plannedFont).Width);
            var remainingLabelW = MeasureTracked(g, "FALTAN", labelFont, 1.5f);
            var travelledLabelW = MeasureTracked(g, "RECORRIDO", labelFont, 1.5f);
            var remainingColumnW = Math.Max(remainingW, remainingLabelW);
            var rightColumnW = Math.Max(rightW, travelledLabelW);

            const float iconSize = 20f;
            const float iconGap = 8f;
            const float columnGap = 16f;
            var totalW = iconSize + iconGap + remainingColumnW + columnGap + rightColumnW;
            var startX = x + (width - totalW) / 2f;
            var remainingX = startX + iconSize + iconGap;
            var travelledX = remainingX + remainingColumnW + columnGap;

            DrawRouteIcon(g, startX, y + 26f, iconSize, WithAlpha(Cyan, 0.90f));

            DrawTracked(g, "FALTAN", labelFont, White(0.38f), remainingX, y + 15f, 1.5f);
            DrawTracked(g, "RECORRIDO", labelFont, White(0.38f), travelledX, y + 15f, 1.5f);

            DrawFitted(g, trip.Remaining, "Bahnschrift", 24f, FontStyle.Bold, Green,
                new RectangleF(remainingX, y + 20f, remainingColumnW, 32f), Left, 16f);
            DrawText(g, "km", unitFont, White(0.42f),
                new RectangleF(remainingX + g.MeasureString(trip.Remaining, remainingFont).Width + 4f,
                    y + 31f, 20f, 18f), Left);

            DrawText(g, travelledText, travelledFont, White(0.88f),
                new RectangleF(travelledX, y + 20f, rightColumnW, 32f), Left);
            DrawText(g, plannedText, plannedFont, White(0.34f),
                new RectangleF(travelledX, y + 49f, rightColumnW, 12f), Left);

            var arrival = trip.Eta != null ? $"LLEGADA {trip.Eta}" : "EN RUTA";
            DrawText(g, arrival, Mono(10.5f, FontStyle.Bold), White(trip.Eta != null ? 0.56f : 0.40f),
                new RectangleF(x + 12f, y + 64f, width - 24f, 16f), Center);
        }
        else
        {
            var title = "SIN VIAJE";
            var subtitle = snap.Active ? "SIN RUTA ACTIVA" : "SIN SEÑAL";
            var titleFont = Rounded(13f, FontStyle.Bold);
            var subtitleFont = Mono(10.5f, FontStyle.Bold);
            var textW = Math.Max(
                g.MeasureString(title, titleFont).Width,
                g.MeasureString(subtitle, subtitleFont).Width);
            const float iconSize = 20f;
            const float iconGap = 8f;
            var startX = x + (width - (iconSize + iconGap + textW)) / 2f;

            DrawRouteIcon(g, startX, y + 36f, iconSize, White(0.22f));
            DrawText(g, title, titleFont, White(0.34f),
                new RectangleF(startX + iconSize + iconGap, y + 26f, textW + 2f, 20f), Left);
            DrawText(g, subtitle, subtitleFont, White(0.42f),
                new RectangleF(startX + iconSize + iconGap, y + 49f, textW + 2f, 18f), Left);
        }
    }

    private static void DrawRouteIcon(Graphics g, float x, float y, float size, Color color)
    {
        var s = size;
        using var pen = new Pen(color, Math.Max(1.2f, s * 0.08f))
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };

        // Camino sinuoso hasta el destino.
        g.DrawLine(pen, x + s * 0.10f, y + s * 0.86f, x + s * 0.30f, y + s * 0.66f);
        g.DrawLine(pen, x + s * 0.30f, y + s * 0.66f, x + s * 0.47f, y + s * 0.72f);

        using (var dot = new SolidBrush(color))
        {
            g.FillEllipse(dot, x + s * 0.03f, y + s * 0.80f, s * 0.15f, s * 0.15f);
        }

        // Bandera de destino al final de la ruta.
        using (var brush = new SolidBrush(color))
        {
            g.FillRectangle(brush, x + s * 0.56f, y + s * 0.16f,
                Math.Max(1.5f, s * 0.055f), s * 0.56f);
            g.FillPolygon(brush, new[]
            {
                new PointF(x + s * 0.61f, y + s * 0.18f),
                new PointF(x + s * 0.92f, y + s * 0.25f),
                new PointF(x + s * 0.61f, y + s * 0.43f)
            });
        }
    }

    private sealed record TripInfo(string Travelled, string Planned, string Remaining, string? Eta);

    private static TripInfo? BuildTrip(Snap snap)
    {
        if (!snap.Active || !snap.OnJob || snap.Planned is not { } planned || planned == 0 || snap.RouteD is not { } route)
        {
            return null;
        }

        var plannedKm = (int)planned;
        var remainingKm = Math.Max(0, (int)Math.Round(route / 1000.0));
        var travelledKm = Math.Max(0, plannedKm - remainingKm);
        return new TripInfo(
            travelledKm.ToString("N0", Es),
            plannedKm.ToString("N0", Es),
            remainingKm.ToString("N0", Es),
            FormatEta(snap.RouteT));
    }

    private static string? FormatEta(float? seconds)
    {
        if (seconds is not { } value || value <= 0 || value >= 172_800)
        {
            return null;
        }

        var minutes = Math.Max(0, (int)Math.Round(value / 60.0));
        var hours = minutes / 60;
        var rest = minutes % 60;
        return hours > 0 ? $"{hours}h {rest}m" : $"{rest} min";
    }

    // ---------------------------------------------------------------------
    // Gauges
    // ---------------------------------------------------------------------

    private void DrawSpeedometer(Graphics g, Snap snap, float x, float y, float side)
    {
        const double maxSpeed = 180.0;
        var speedKmh = snap.Active ? Math.Abs(snap.Speed ?? 0f) * 3.6 : 0;
        var limitKmh = snap.Active && snap.Limit is { } limit && limit > 0.5f
            ? limit * 3.6
            : (double?)null;

        var accent = !snap.Active
            ? White(0.30f)
            : (limitKmh is { } limitValue && speedKmh > limitValue + 1.0 ? Orange : Cyan);

        var valueText = snap.Active && snap.Speed != null
            ? Math.Round(speedKmh).ToString("0", Es)
            : "--";

        var subtitle = limitKmh is { } shownLimit
            ? $"LÍMITE {Math.Round(shownLimit).ToString("0", Es)}"
            : "MÁX 160";

        var odometerText = snap.Active && snap.Odometer is { } odometer && odometer >= 0
            ? Math.Round(odometer).ToString("N0", Es)
            : "--";

        var odometerColor = snap.Active ? Cyan : White(0.25f);

        DrawDial(
            g, x, y, side,
            Math.Min(speedKmh, maxSpeed), maxSpeed, 20,
            accent, null,
            valueText, "KM/H", subtitle,
            null, odometerText, "km", odometerColor,
            limitKmh, Red);

        var cruiseSetKmh = snap.Active && snap.CruiseOn && snap.Cruise is { } cruise && cruise > 0.5f
            ? cruise * 3.6
            : (double?)null;

        var badgeX = x + side / 2f - 59f;
        var badgeY = y + side / 2f + side * 0.012f - 128f - 29f;
        DrawGaugeBadge(g, badgeX, badgeY, 118f, 58f, cruiseSetKmh != null, Green, () =>
        {
            if (cruiseSetKmh is not { } setSpeed)
            {
                return;
            }

            var speedText = Math.Round(setSpeed).ToString("0", Es);
            var labelFont = Rounded(7f, FontStyle.Bold);
            var speedFont = Rounded(19f, FontStyle.Bold);
            var unitFont = Rounded(7.5f, FontStyle.Regular);
            var labelWidth = g.MeasureString("CRUCERO", labelFont).Width;
            var speedWidth = g.MeasureString(speedText, speedFont).Width;
            var unitWidth = g.MeasureString("KM/H", unitFont).Width;
            var textWidth = Math.Max(labelWidth, speedWidth + 3f + unitWidth);

            // Misma altura de grupo e icono que el testigo de check engine.
            const float iconSize = 32f;
            const float iconGap = 8f;
            const float groupHeight = 42f;
            var groupWidth = iconSize + iconGap + textWidth;
            var startX = badgeX + (118f - groupWidth) / 2f;
            var startY = badgeY + (58f - groupHeight) / 2f;

            DrawIconOrFallback(g, "cruise-control-c",
                new RectangleF(startX, startY + (groupHeight - iconSize) / 2f, iconSize, iconSize), Green);

            var textX = startX + iconSize + iconGap;
            DrawText(g, "CRUCERO", labelFont, White(0.48f),
                new RectangleF(textX, startY + 2f, textWidth + 2f, 12f), Left);
            DrawText(g, speedText, speedFont, Green,
                new RectangleF(textX, startY + 14f, speedWidth + 4f, 26f), Left);
            DrawText(g, "KM/H", unitFont, White(0.40f),
                new RectangleF(textX + speedWidth + 3f, startY + 24f, unitWidth + 4f, 13f), Left);
        });
    }

    private void DrawTachometer(Graphics g, Snap snap, float x, float y, float side)
    {
        var gaugeMax = snap.RpmMax is { } rpmMax && rpmMax >= 800 ? (double)rpmMax : 2500.0;
        var rpm = Math.Clamp(snap.Rpm ?? 0, 0, (float)gaugeMax);
        var fraction = gaugeMax > 0 ? rpm / gaugeMax : 0;

        var majorStep = gaugeMax switch
        {
            <= 2600 => 250,
            <= 5200 => 500,
            _ => 1000
        };

        var accent = !snap.Active
            ? White(0.30f)
            : fraction >= 0.90 ? Red
            : fraction >= 0.78 ? Orange
            : fraction >= 0.62 ? Yellow
            : Green;

        var valueText = snap.Active && snap.Rpm != null
            ? Math.Round(rpm).ToString("N0", Es)
            : "--";

        var gearText = !snap.Active || snap.Gear is not { } gear
            ? "--"
            : gear > 0 ? gear.ToString(Es) : gear == 0 ? "N" : "R";

        var gearColor = !snap.Active || snap.Gear is not { } gearValue
            ? White(0.30f)
            : gearValue < 0 ? Red : gearValue == 0 ? Cyan : Green;

        DrawDial(
            g, x, y, side,
            rpm, gaugeMax, majorStep,
            accent, 0.86,
            valueText, "RPM", $"MÁX {Math.Round(gaugeMax).ToString("N0", Es)}",
            "MARCHA", gearText, null, gearColor,
            null, Color.Transparent);

        var checkEngine = snap.Active && snap.Engine && snap.EngineDmg is { } damage && damage >= 5;
        var badgeX = x + side / 2f - 59f;
        var badgeY = y + side / 2f + side * 0.012f - 128f - 29f;

        DrawGaugeBadge(g, badgeX, badgeY, 118f, 58f, checkEngine, Yellow, () =>
        {
            if (!checkEngine)
            {
                return;
            }

            // Misma estructura que el indicador de crucero: icono + texto en dos niveles.
            const float iconSize = 32f;
            const float iconGap = 8f;
            var labelFont = Rounded(6.5f, FontStyle.Bold);
            var valueFont = Rounded(15f, FontStyle.Bold);
            var labelWidth = MeasureTracked(g, "CHECK", labelFont, 0.8f);
            var valueWidth = g.MeasureString("ENGINE", valueFont).Width;
            var textWidth = Math.Max(labelWidth, valueWidth);
            var groupWidth = iconSize + iconGap + textWidth;
            var startX = badgeX + (118f - groupWidth) / 2f;
            var textX = startX + iconSize + iconGap;

            DrawPulsingTintedIcon(
                g,
                "check-engine-line",
                new RectangleF(startX, badgeY + 13f, iconSize, iconSize),
                Yellow);

            DrawTracked(g, "CHECK", labelFont, White(0.48f),
                textX, badgeY + 19f, 0.8f);
            DrawText(g, "ENGINE", valueFont, Yellow,
                new RectangleF(textX, badgeY + 21f, textWidth + 4f, 26f), Left);
        });
    }

    private void DrawDial(
        Graphics g,
        float x,
        float y,
        float side,
        double value,
        double maxValue,
        double majorStep,
        Color accent,
        double? redlineStart,
        string valueText,
        string unitText,
        string? subtitle,
        string? badgeLabel,
        string? badgeValue,
        string? badgeUnit,
        Color badgeColor,
        double? markerValue,
        Color markerColor)
    {
        var fraction = maxValue > 0 ? Math.Clamp(value / maxValue, 0, 1) : 0;
        var centerX = x + side / 2f;
        var centerY = y + side / 2f + side * 0.012f;
        var radius = side * 0.385f;
        const double startAngle = 135.0;
        const double sweep = 270.0;
        var arcWidth = side * 0.028f;

        using (var outer = new Pen(Ink(9), 1f))
        {
            var d = side * 0.985f;
            g.DrawEllipse(outer, centerX - d / 2f, centerY - d / 2f, d, d);
        }

        using (var track = new Pen(Ink(23), arcWidth)
               {
                   StartCap = LineCap.Round,
                   EndCap = LineCap.Round
               })
        {
            g.DrawArc(track, centerX - radius, centerY - radius, radius * 2f, radius * 2f,
                (float)startAngle, (float)sweep);
        }

        if (redlineStart is { } red)
        {
            var redStart = startAngle + sweep * red;
            using var redPen = new Pen(WithAlpha(Red, 0.52f), arcWidth)
            {
                StartCap = LineCap.Flat,
                EndCap = LineCap.Flat
            };
            g.DrawArc(redPen, centerX - radius, centerY - radius, radius * 2f, radius * 2f,
                (float)redStart, (float)(sweep * (1 - red)));
        }

        if (fraction > 0.002)
        {
            using var live = new Pen(accent, arcWidth)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };
            // El arco termina en la última marca del dial (160 km/h), sin cerrar
            // el hueco inferior con un círculo completo al alcanzar el máximo.
            g.DrawArc(live, centerX - radius, centerY - radius, radius * 2f, radius * 2f,
                (float)startAngle, (float)(sweep * fraction));
        }

        if (markerValue is { } marker && marker > 0 && marker < maxValue)
        {
            var angle = startAngle + sweep * (marker / maxValue);
            var inner = PointAt(centerX, centerY, radius - arcWidth * 0.7f, angle);
            var outer = PointAt(centerX, centerY, radius + arcWidth * 0.7f, angle);
            using var markerPen = new Pen(markerColor, side * 0.0055f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };
            g.DrawLine(markerPen, inner, outer);
        }

        var minorStep = majorStep / 2.0;
        var tickCount = (int)Math.Round(maxValue / minorStep);
        for (var i = 0; i <= tickCount; i++)
        {
            var tickValue = i * minorStep;
            if (tickValue > maxValue + 0.1)
            {
                break;
            }

            var angle = startAngle + sweep * (tickValue / maxValue);
            var isMajor = i % 2 == 0;
            var outer = radius + side * 0.020f;
            var inner = radius + (isMajor ? side * 0.045f : side * 0.034f);
            var p1 = PointAt(centerX, centerY, outer, angle);
            var p2 = PointAt(centerX, centerY, inner, angle);

            using var tickPen = new Pen(
                Ink(isMajor ? 158 : 56),
                isMajor ? side * 0.0037f : side * 0.0022f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };
            g.DrawLine(tickPen, p1, p2);

            if (isMajor)
            {
                var labelPoint = PointAt(centerX, centerY, radius + side * 0.082f, angle);
                DrawText(g, Math.Round(tickValue).ToString("0", Es), Rounded(side * 0.029f, FontStyle.Bold),
                    White(0.48f),
                    new RectangleF(labelPoint.X - 30f, labelPoint.Y - 12f, 60f, 24f), Center);
            }
        }

        var needleAngle = startAngle + sweep * fraction;
        var tip = PointAt(centerX, centerY, radius * 0.77f, needleAngle);
        var tail = PointAt(centerX, centerY, -side * 0.021f, needleAngle);

        using (var glow = new Pen(WithAlpha(accent, 0.18f), side * 0.034f)
               {
                   StartCap = LineCap.Round,
                   EndCap = LineCap.Round
               })
        {
            g.DrawLine(glow, tail, tip);
        }

        using (var needle = new Pen(accent, side * 0.0092f)
               {
                   StartCap = LineCap.Round,
                   EndCap = LineCap.Round
               })
        {
            g.DrawLine(needle, tail, tip);
        }

        var hubSize = side * 0.0295f;
        using (var black = new SolidBrush(Color.Black))
        {
            g.FillEllipse(black, centerX - hubSize, centerY - hubSize, hubSize * 2f, hubSize * 2f);
        }
        using (var hubPen = new Pen(WithAlpha(accent, 0.85f), side * 0.0046f))
        {
            g.DrawEllipse(hubPen, centerX - hubSize, centerY - hubSize, hubSize * 2f, hubSize * 2f);
        }
        using (var hubFill = new SolidBrush(WithAlpha(accent, 0.90f)))
        {
            var inner = side * 0.0108f;
            g.FillEllipse(hubFill, centerX - inner, centerY - inner, inner * 2f, inner * 2f);
        }

        var valueRect = new RectangleF(centerX - 160f, centerY + 45f, 320f, 105f);
        DrawFitted(g, valueText, "Bahnschrift", side * 0.145f, FontStyle.Bold, accent,
            valueRect, Center, side * 0.085f);

        DrawText(g, unitText, Rounded(side * 0.035f, FontStyle.Bold), White(0.52f),
            new RectangleF(centerX - 150f, centerY + 153f, 300f, 28f), Center);

        if (subtitle != null)
        {
            DrawText(g, subtitle, Mono(side * 0.018f, FontStyle.Bold), White(0.25f),
                new RectangleF(centerX - 150f, centerY + 181f, 300f, 16f), Center);
        }

        if (badgeValue != null)
        {
            var badgeRect = new RectangleF(centerX - 105f, centerY + 210f, 210f, 44f);
            DrawSquircle(g, badgeRect, 14f, Ink(11),
                WithAlpha(badgeColor, 0.25f), 1f);

            if (badgeLabel != null && badgeUnit != null)
            {
                var labelWidth = g.MeasureString(badgeLabel, Rounded(13.2f, FontStyle.Bold)).Width;
                var valueWidth = g.MeasureString(badgeValue, Rounded(34.8f, FontStyle.Bold)).Width;
                var unitWidth = g.MeasureString(badgeUnit, Rounded(14.4f, FontStyle.Bold)).Width;
                var total = labelWidth + 6f + valueWidth + 6f + unitWidth;
                var start = centerX - total / 2f;
                DrawText(g, badgeLabel, Rounded(13.2f, FontStyle.Bold), White(0.38f),
                    new RectangleF(start, centerY + 221f, labelWidth + 2f, 24f), Left);
                DrawText(g, badgeValue, Rounded(34.8f, FontStyle.Bold), badgeColor,
                    new RectangleF(start + labelWidth + 6f, centerY + 216f, valueWidth + 4f, 34f), Left);
                DrawText(g, badgeUnit, Rounded(14.4f, FontStyle.Bold), White(0.40f),
                    new RectangleF(start + labelWidth + 6f + valueWidth + 6f, centerY + 222f, unitWidth + 4f, 22f), Left);
            }
            else if (badgeLabel != null)
            {
                var labelWidth = g.MeasureString(badgeLabel, Rounded(13.2f, FontStyle.Bold)).Width;
                var valueWidth = g.MeasureString(badgeValue, Rounded(34.8f, FontStyle.Bold)).Width;
                var total = labelWidth + 6f + valueWidth;
                var start = centerX - total / 2f;
                DrawText(g, badgeLabel, Rounded(13.2f, FontStyle.Bold), White(0.38f),
                    new RectangleF(start, centerY + 221f, labelWidth + 2f, 24f), Left);
                DrawText(g, badgeValue, Rounded(34.8f, FontStyle.Bold), badgeColor,
                    new RectangleF(start + labelWidth + 6f, centerY + 216f, valueWidth + 4f, 34f), Left);
            }
            else
            {
                var valueWidth = g.MeasureString(badgeValue, Rounded(34.8f, FontStyle.Bold)).Width;
                var unitWidth = badgeUnit == null
                    ? 0f
                    : g.MeasureString(badgeUnit, Rounded(14.4f, FontStyle.Bold)).Width;
                var total = valueWidth + (badgeUnit == null ? 0f : 6f + unitWidth);
                var start = centerX - total / 2f;
                DrawText(g, badgeValue, Rounded(34.8f, FontStyle.Bold), badgeColor,
                    new RectangleF(start, centerY + 216f, valueWidth + 4f, 34f), Left);
                if (badgeUnit != null)
                {
                    DrawText(g, badgeUnit, Rounded(14.4f, FontStyle.Bold), White(0.40f),
                        new RectangleF(start + valueWidth + 6f, centerY + 222f, unitWidth + 4f, 22f), Left);
                }
            }
        }
    }

    private void DrawGaugeBadge(Graphics g, float x, float y, float width, float height, bool active, Color accent, Action content)
    {
        DrawSquircle(g, new RectangleF(x, y, width, height), 14f,
            Ink(active ? 13 : 6),
            active ? WithAlpha(accent, 0.42f) : Ink(41),
            1f);

        if (active)
        {
            content();
        }
    }

    private void DrawPulsingTintedIcon(Graphics g, string asset, RectangleF rect, Color color)
    {
        var image = Icon(asset);
        if (image == null)
        {
            return;
        }

        var phase = ReduceMotion
            ? 0.5
            : (Math.Sin(DateTime.UtcNow.TimeOfDay.TotalSeconds * 2 * Math.PI / 1.10) + 1) / 2;
        var centerX = rect.Left + rect.Width / 2f;
        var centerY = rect.Top + rect.Height / 2f;

        // Halo radial suave: refuerza el pulso sin reescalar el bitmap del testigo.
        var glowPad = ReduceMotion ? 5f : 5f + (float)(2f * phase);
        var glow = RectangleF.Inflate(rect, glowPad, glowPad);

        using (var path = new GraphicsPath())
        {
            path.AddEllipse(glow.X, glow.Y, glow.Width, glow.Height);
            using var brush = new PathGradientBrush(path)
            {
                CenterColor = WithAlpha(color, (float)(0.10 + 0.08 * phase)),
                SurroundColors = new[] { WithAlpha(color, 0f) }
            };
            g.FillEllipse(brush, glow);
        }

        // Icono a tamaño fijo y alineado al píxel: se mantiene nítido durante todo el pulso.
        var drawRect = new Rectangle(
            (int)Math.Round(rect.X),
            (int)Math.Round(rect.Y),
            Math.Max(1, (int)Math.Round(rect.Width)),
            Math.Max(1, (int)Math.Round(rect.Height)));
        using var attributes = new ImageAttributes();
        attributes.SetColorMatrix(ColorTintMatrix(color, (float)(0.70 + 0.30 * phase)));
        g.DrawImage(
            image,
            drawRect,
            0,
            0,
            image.Width,
            image.Height,
            GraphicsUnit.Pixel,
            attributes);
    }

    private void DrawIconOrFallback(Graphics g, string asset, RectangleF rect, Color color)
    {
        var image = Icon(asset);
        if (image == null)
        {
            using var pen = new Pen(color, 2f);
            g.DrawEllipse(pen, rect);
            return;
        }

        g.DrawImage(image, rect);
    }

    // ---------------------------------------------------------------------
    // Mini gauge row
    // ---------------------------------------------------------------------

    private void DrawMiniGaugeRow(Graphics g, Snap snap, float y)
    {
        const float size = 200f;
        const float financeWidth = size * 1.8f;
        const float gap = 16f;
        const float rowX = 131f;

        var xs = new float[5];
        xs[0] = rowX;
        xs[1] = xs[0] + size + gap;
        xs[2] = xs[1] + size + gap;
        xs[3] = xs[2] + financeWidth + gap;
        xs[4] = xs[3] + size + gap;

        var water = snap.Active && snap.WaterTemp is { } waterTemp && waterTemp > 0
            ? (double?)waterTemp
            : null;
        var air = snap.Active && snap.AirPressure is { } airPressure && airPressure >= 0
            ? (double?)airPressure
            : null;

        var waterAccent = WaterTemperatureColor(water);
        var airAccent = AirPressureColor(air);

        DrawMiniGauge(g, xs[0], y, size, "AGUA", water == null ? "--" : Math.Round(water.Value).ToString("0", Es),
            "°C", water == null ? 0 : Math.Clamp((water.Value - 40) / 80, 0, 1), waterAccent,
            showTemperatureIcon: true);

        DrawMiniGauge(g, xs[1], y, size, string.Empty, air == null ? "--" : Math.Round(air.Value).ToString("0", Es),
            "PSI", air == null ? 0 : Math.Clamp(air.Value / 120, 0, 1), airAccent,
            showAirIcon: true);

        DrawFinancePanel(g, snap, xs[2], y, financeWidth, size);
        DrawFuelPanel(g, snap, xs[3], y, size, size);
        DrawWeatherPanel(g, snap, xs[4], y, size, size);
    }

    // Azul por debajo de 40 °C y rojo a los 100 °C, pasando por cian, verde,
    // amarillo y naranja para que la lectura acompañe la subida real.
    private static Color WaterTemperatureColor(double? water)
    {
        if (water is not { } value)
        {
            return White(0.25f);
        }

        var t = Math.Clamp((value - 40.0) / 80.0, 0.0, 1.0);
        (double Stop, Color Color)[] stops =
        {
            (0.00, Blue),
            (0.18, Cyan),
            (0.72, Green),
            (0.82, Yellow),
            (0.92, Orange),
            (1.00, Red)
        };

        for (var i = 1; i < stops.Length; i++)
        {
            if (t > stops[i].Stop)
            {
                continue;
            }

            var from = stops[i - 1];
            var to = stops[i];
            var f = (t - from.Stop) / (to.Stop - from.Stop);
            return Color.FromArgb(
                (int)Math.Round(from.Color.R + (to.Color.R - from.Color.R) * f),
                (int)Math.Round(from.Color.G + (to.Color.G - from.Color.G) * f),
                (int)Math.Round(from.Color.B + (to.Color.B - from.Color.B) * f));
        }

        return Red;
    }

    // El extremo vacío es el estado crítico; a más PSI el color avanza por
    // naranja, amarillo y verde hasta llegar al cian de la presión máxima.
    private static Color AirPressureColor(double? air)
    {
        if (air is not { } value)
        {
            return White(0.25f);
        }

        var t = Math.Clamp(value / 120.0, 0.0, 1.0);
        (double Stop, Color Color)[] stops =
        {
            (0.00, Red),
            (0.25, Orange),
            (0.50, Yellow),
            (0.70, Green),
            (0.90, Cyan),
            (1.00, Cyan)
        };

        for (var i = 1; i < stops.Length; i++)
        {
            if (t > stops[i].Stop)
            {
                continue;
            }

            var from = stops[i - 1];
            var to = stops[i];
            var f = (t - from.Stop) / (to.Stop - from.Stop);
            return Color.FromArgb(
                (int)Math.Round(from.Color.R + (to.Color.R - from.Color.R) * f),
                (int)Math.Round(from.Color.G + (to.Color.G - from.Color.G) * f),
                (int)Math.Round(from.Color.B + (to.Color.B - from.Color.B) * f));
        }

        return Cyan;
    }

    private void DrawMiniGauge(
        Graphics g,
        float x,
        float y,
        float size,
        string label,
        string valueText,
        string unit,
        double fraction,
        Color accent,
        bool showTemperatureIcon = false,
        bool showAirIcon = false)
    {
        var centerX = x + size / 2f;
        var centerY = y + size / 2f;
        var radius = size * 0.405f;
        var lineWidth = size * 0.034f;

        using (var background = new SolidBrush(Ink(5)))
        {
            var r = radius + lineWidth;
            g.FillEllipse(background, centerX - r, centerY - r, r * 2f, r * 2f);
        }

        using (var track = new Pen(Ink(19), lineWidth))
        {
            g.DrawEllipse(track, centerX - radius, centerY - radius, radius * 2f, radius * 2f);
        }

        if (fraction > 0.002)
        {
            using var live = new Pen(accent, lineWidth)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };

            if (fraction >= 0.9995)
            {
                g.DrawEllipse(live, centerX - radius, centerY - radius, radius * 2f, radius * 2f);
            }
            else
            {
                g.DrawArc(live, centerX - radius, centerY - radius, radius * 2f, radius * 2f,
                    -90f, (float)(360 * fraction));
            }
        }

        for (var i = 0; i < 8; i++)
        {
            var angle = -90.0 + i * 45.0;
            var outer = radius + lineWidth * 0.72f;
            var inner = outer - size * 0.022f;
            using var tick = new Pen(Ink(i % 2 == 0 ? 61 : 26), 1.2f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };
            g.DrawLine(tick, PointAt(centerX, centerY, outer, angle), PointAt(centerX, centerY, inner, angle));
        }

        if (showTemperatureIcon || showAirIcon)
        {
            // Testigo junto a la lectura, alineado con el centro del número.
            const float iconSize = 22f;
            const float iconGap = 8f;
            var valueFont = GetFont("Bahnschrift", size * 0.205f, FontStyle.Bold);
            var valueWidth = g.MeasureString(valueText, valueFont).Width;
            var iconX = centerX - valueWidth / 2f - iconGap - iconSize;
            var iconY = centerY - 13f - iconSize / 2f;

            if (showTemperatureIcon)
            {
                DrawThermometerIcon(g, new RectangleF(iconX, iconY, iconSize, iconSize), accent);
            }
            else
            {
                DrawManometerIcon(g, new RectangleF(iconX, iconY, iconSize, iconSize), accent);
            }
        }

        DrawFitted(g, valueText, "Bahnschrift", size * 0.205f, FontStyle.Bold, accent,
            new RectangleF(centerX - 82f, centerY - 38f, 164f, 50f), Center, size * 0.10f);
        DrawText(g, unit, Rounded(size * 0.060f, FontStyle.Bold), White(0.46f),
            new RectangleF(centerX - 60f, centerY + 17f, 120f, 18f), Center);

        if (!string.IsNullOrEmpty(label))
        {
            DrawText(g, label, Rounded(size * 0.050f, FontStyle.Bold), White(0.36f),
                new RectangleF(centerX - 60f, centerY + 39f, 120f, 16f), Center);
        }
    }

    // ---------------------------------------------------------------------
    // Compact panels
    // ---------------------------------------------------------------------

    private void DrawFinancePanel(Graphics g, Snap snap, float x, float y, float width, float height)
    {
        DrawPanelFrame(g, x, y, width, height, 22f);

        var contentX = x + width * 0.045f;
        var contentW = width * 0.91f;

        DrawMoneyIcon(g, contentX, y + 13f, 16f, Cyan);

        var company = string.IsNullOrWhiteSpace(snap.CompanyName) ? "EMPRESA" : snap.CompanyName;
        DrawFitted(g, company, "Bahnschrift", 16f, FontStyle.Bold,
            White(string.IsNullOrWhiteSpace(snap.CompanyName) ? 0.42f : 0.90f),
            new RectangleF(contentX + 24f, y + 10f, contentW - 24f, 22f), Left, 10f);

        using (var divider = new Pen(Ink(18), 1f))
        {
            g.DrawLine(divider, contentX, y + 38f, contentX + contentW, y + 38f);
        }

        var driversLabel = snap.HiredDrivers is { } driverCount
            ? $"CONDUCTORES ({driverCount})"
            : "CONDUCTORES";

        DrawFinanceRow(g, contentX, y + 49f, contentW, "TRABAJO ACTUAL", Money(Income(snap)), Green, 22f);
        DrawFinanceRow(g, contentX, y + 78f, contentW, driversLabel, Money(snap.DriverIncome), Green, 22f);
        DrawFinanceRow(g, contentX, y + 107f, contentW, "EGRESOS RUTA", Money(Expenses(snap)), Red, 22f);

        using (var divider = new Pen(Ink(18), 1f))
        {
            g.DrawLine(divider, contentX, y + 137f, contentX + contentW, y + 137f);
        }

        DrawFinanceRow(g, contentX, y + 149f, contentW, "DINERO TOTAL", Money(snap.BankBalance), Cyan, 29f);
    }

    private void DrawMoneyIcon(Graphics g, float x, float y, float size, Color color)
    {
        using (var coin = new SolidBrush(color))
        {
            g.FillEllipse(coin, x, y, size, size);
        }

        DrawText(g, "$", Rounded(size * 0.68f, FontStyle.Bold), Color.Black,
            new RectangleF(x, y, size, size), Center);
    }

    private void DrawFinanceRow(
        Graphics g,
        float x,
        float y,
        float width,
        string label,
        string value,
        Color color,
        float valueSize)
    {
        DrawText(g, label, Rounded(8f, FontStyle.Bold), White(0.40f),
            new RectangleF(x, y + 2f, width * 0.38f, 18f), Left);
        DrawFitted(g, value, "Bahnschrift", valueSize, FontStyle.Bold, color,
            new RectangleF(x + width * 0.38f, y - 3f, width * 0.62f, valueSize + 8f), Right, 9f);
    }

    private string Money(long? value)
    {
        if (value == null)
        {
            return "--";
        }

        var magnitude = Math.Abs(value.Value);
        return (value.Value < 0 ? "-$" : "$") + magnitude.ToString("N0", Es);
    }

    private static long? Income(Snap snap)
    {
        if (!snap.Active)
        {
            return null;
        }

        if (snap.OnJob && snap.JobIncome is { } jobIncome)
        {
            return (long)jobIncome;
        }

        if (snap.JobDeliveredRevenue is { } delivered && delivered > 0)
        {
            return delivered;
        }

        return snap.JobIncome is { } income ? (long)income : 0;
    }

    private static long? Expenses(Snap snap)
    {
        if (!snap.Active)
        {
            return null;
        }

        long total = 0;
        foreach (var value in new[]
                 {
                     snap.FineAmount,
                     snap.TollgatePay,
                     snap.FerryPay,
                     snap.TrainPay,
                     snap.JobCancelledPenalty
                 })
        {
            if (value is { } amount)
            {
                total += Math.Max(0, amount);
            }
        }

        return total;
    }

    private void DrawFuelPanel(Graphics g, Snap snap, float x, float y, float width, float height)
    {
        DrawPanelFrame(g, x, y, width, height, 22f);

        var fuel = snap.Active && snap.Fuel is { } fuelValue ? (double?)fuelValue : null;
        var capacity = snap.Active && snap.FuelCap is { } cap && cap > 0 ? (double?)cap : null;
        var fraction = fuel.HasValue && capacity.HasValue && capacity > 0
            ? Math.Clamp(fuel.Value / capacity.Value, 0, 1)
            : 0;
        var lowFuel = snap.FuelWarn || (snap.Active && fraction < 0.12);

        var color = !snap.Active || fuel == null || capacity == null
            ? White(0.25f)
            : lowFuel ? Red
            : fraction < 0.30 ? Orange
            : Green;

        var contentX = x + width * 0.075f;

        DrawFuelIcon(g, contentX, y + height * 0.055f, width * 0.09f, color);
        DrawTracked(g, Localization.L("FUEL"), Rounded(16f, FontStyle.Bold), White(0.52f),
            contentX + width * 0.12f, y + height * 0.105f, 1f);

        // Conjunto barra + datos centrado en el panel.
        var barW = width * 0.12f;
        var gap = width * 0.07f;
        var dataW = width * 0.26f;
        var groupW = barW + gap + dataW;
        var groupX = x + (width - groupW) / 2f;

        // La barra abarca exactamente el alto visible del bloque de texto.
        var barX = groupX;
        var barY = y + height * 0.30f;
        var barH = height * 0.48f;
        DrawFuelBar(g, barX, barY, barW, barH, fraction, color);

        var dataX = barX + barW + gap;
        DrawText(g, fuel == null || capacity == null ? "--" : $"{Math.Round(fraction * 100).ToString("0", Es)}%",
            Rounded(27f, FontStyle.Bold), color,
            new RectangleF(dataX, y + height * 0.235f, dataW, height * 0.19f), Center);
        DrawText(g, fuel == null ? "-- gal" : $"{Math.Round(fuel.Value * 0.2641720524).ToString("N0", Es)} gal",
            Rounded(14f, FontStyle.Bold), White(0.92f),
            new RectangleF(dataX, y + height * 0.445f, dataW, height * 0.10f), Center);
        DrawText(g, capacity == null ? string.Empty : $"de {Math.Round(capacity.Value * 0.2641720524).ToString("N0", Es)} gal",
            Rounded(8.5f, FontStyle.Regular), White(0.32f),
            new RectangleF(dataX, y + height * 0.545f, dataW, height * 0.07f), Center);

        DrawText(g, "AUTONOMÍA", Rounded(6.8f, FontStyle.Bold), White(0.38f),
            new RectangleF(dataX, y + height * 0.66f, dataW, height * 0.055f), Center);
        DrawText(g, snap.Range is { } range ? $"{Math.Round(range).ToString("N0", Es)} km" : "-- km",
            Rounded(13f, FontStyle.Bold), White(0.90f),
            new RectangleF(dataX, y + height * 0.72f, dataW, height * 0.115f), Center);

        if (lowFuel && snap.Active)
        {
            var reserveW = width * 0.26f;
            var reserve = new RectangleF(dataX + (dataW - reserveW) / 2f, y + height * 0.85f, reserveW, height * 0.06f);
            DrawRounded(g, reserve, height * 0.03f, Red, Color.Transparent, 0f);
            DrawText(g, "RESERVA", Rounded(6f, FontStyle.Bold), Color.Black,
                new RectangleF(reserve.X, reserve.Y, reserve.Width, reserve.Height), Center);
        }
    }

    private static void DrawFuelIcon(Graphics g, float x, float y, float size, Color color)
    {
        var body = new RectangleF(x + size * 0.08f, y + size * 0.08f, size * 0.55f, size * 0.84f);
        using (var path = RoundedPath(body, size * 0.08f))
        using (var brush = new SolidBrush(color))
        {
            g.FillPath(brush, path);
        }

        using (var window = new SolidBrush(Color.Black))
        {
            g.FillRectangle(window, x + size * 0.19f, y + size * 0.20f, size * 0.33f, size * 0.16f);
        }

        using var pen = new Pen(color, Math.Max(1.4f, size * 0.09f))
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        g.DrawLine(pen, x + size * 0.63f, y + size * 0.24f, x + size * 0.82f, y + size * 0.36f);
        g.DrawLine(pen, x + size * 0.82f, y + size * 0.36f, x + size * 0.82f, y + size * 0.70f);
        g.DrawLine(pen, x + size * 0.82f, y + size * 0.70f, x + size * 0.72f, y + size * 0.79f);
        g.DrawLine(pen, x + size * 0.02f, y + size * 0.94f, x + size * 0.66f, y + size * 0.94f);
    }

    private void DrawFuelBar(Graphics g, float x, float y, float width, float height, double fraction, Color color)
    {
        var bar = new RectangleF(x, y, width, height);
        using (var path = RoundedPath(bar, width / 2f))
        {
            using var background = new SolidBrush(Ink(12));
            g.FillPath(background, path);
        }

        var fillHeight = (float)(height * Math.Clamp(fraction, 0, 1));
        if (fillHeight > 1f)
        {
            var state = g.Save();
            using (var clipPath = RoundedPath(bar, width / 2f))
            {
                g.SetClip(clipPath);
            }

            var fillRect = new RectangleF(x, y + height - fillHeight, width, fillHeight);
            using var gradient = new LinearGradientBrush(
                fillRect,
                color,
                WithAlpha(color, 0.52f),
                LinearGradientMode.Vertical);
            using var glow = new SolidBrush(WithAlpha(color, 0.18f));
            g.FillRectangle(glow, new RectangleF(x - 3f, fillRect.Y - 3f, width + 6f, fillRect.Height + 6f));
            g.FillRectangle(gradient, fillRect);
            g.Restore(state);
        }

        using (var divider = new Pen(Ink(46), 1f))
        {
            foreach (var q in new[] { 0.25, 0.50, 0.75 })
            {
                var lineY = y + (float)(height * (1 - q));
                g.DrawLine(divider, x, lineY, x + width, lineY);
            }
        }

        using (var outline = new Pen(Ink(26), 1f))
        using (var path = RoundedPath(bar, width / 2f))
        {
            g.DrawPath(outline, path);
        }
    }

    private void DrawWeatherPanel(Graphics g, Snap snap, float x, float y, float width, float height)
    {
        DrawPanelFrame(g, x, y, width, height, 22f);

        var minutes = snap.Active && snap.GameTime is { } gameTime ? (int?)gameTime : null;
        var isDay = minutes == null || ((minutes.Value / 60) % 24) is >= 6 and < 20;
        var condition = snap.Active ? snap.Weather : WeatherCondition.Clear;

        var conditionText = !snap.Active
            ? "SIN SEÑAL"
            : condition switch
            {
                WeatherCondition.Clear => "DESPEJADO",
                WeatherCondition.Cloudy => "NUBLADO",
                WeatherCondition.Rain => "LLUVIA",
                _ => "TORMENTA"
            };

        var accent = !snap.Active
            ? White(0.25f)
            : condition switch
            {
                WeatherCondition.Clear => isDay ? Yellow : MoonLight,
                WeatherCondition.Cloudy => White(0.62f),
                WeatherCondition.Rain => Cyan,
                _ => Yellow
            };

        var contentX = x + width * 0.075f;
        var contentW = width * 0.85f;

        DrawCompactWeatherIcon(g, condition, isDay, contentX, y + height * 0.07f, width * 0.09f, accent, snap.Active);
        DrawText(g, snap.Active ? (isDay ? "DÍA" : "NOCHE") : "—",
            Mono(7.5f, FontStyle.Bold), White(0.32f),
            new RectangleF(x + width * 0.70f, y + height * 0.08f, width * 0.225f, 14f), Right);

        DrawWeatherGlyph(g, condition, isDay, x + width * 0.29f, y + height * 0.14f,
            width * 0.42f, height * 0.36f, snap.Active);

        var timeText = minutes == null
            ? "--:--"
            : $"{((minutes.Value / 60) % 24):00}:{minutes.Value % 60:00}";
        DrawText(g, timeText, Rounded(28f, FontStyle.Bold), White(0.92f),
            new RectangleF(contentX, y + height * 0.51f, contentW, height * 0.17f), Center);
        DrawText(g, "HORA DEL JUEGO", Rounded(6.5f, FontStyle.Bold), White(0.28f),
            new RectangleF(contentX, y + height * 0.685f, contentW, height * 0.06f), Center);
        DrawText(g, conditionText, Rounded(9f, FontStyle.Bold), accent,
            new RectangleF(contentX, y + height * 0.77f, contentW, height * 0.09f), Center);
    }

    private static void DrawCompactWeatherIcon(
        Graphics g,
        WeatherCondition condition,
        bool isDay,
        float x,
        float y,
        float size,
        Color accent,
        bool active)
    {
        var opacity = active ? 1f : 0.28f;
        var cx = x + size / 2f;
        var cy = y + size / 2f;

        if (condition == WeatherCondition.Clear)
        {
            if (isDay)
            {
                DrawSun(g, cx, cy, size * 0.24f, opacity);
            }
            else
            {
                DrawMoon(g, cx, cy, size * 0.29f, opacity);
            }
            return;
        }

        var w = size * 0.86f;
        var h = size * 0.46f;
        var left = x + size * 0.07f;
        var top = y + size * 0.34f;
        using var brush = new SolidBrush(WithAlpha(accent, opacity));
        g.FillEllipse(brush, left, top + h * 0.22f, w * 0.48f, h * 0.78f);
        g.FillEllipse(brush, left + w * 0.24f, top, w * 0.55f, h * 1.05f);
        g.FillEllipse(brush, left + w * 0.50f, top + h * 0.22f, w * 0.44f, h * 0.78f);
        g.FillRectangle(brush, left + w * 0.14f, top + h * 0.62f, w * 0.72f, h * 0.36f);

        if (condition is WeatherCondition.Rain or WeatherCondition.Storm)
        {
            using var drop = new Pen(
                WithAlpha(condition == WeatherCondition.Storm ? Yellow : Cyan, 0.82f * opacity),
                Math.Max(1.1f, size * 0.075f))
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };
            g.DrawLine(drop, left + w * 0.33f, top + h * 1.05f, left + w * 0.27f, top + h * 1.35f);
            g.DrawLine(drop, left + w * 0.62f, top + h * 1.05f, left + w * 0.56f, top + h * 1.35f);
        }
    }

    private void DrawWeatherGlyph(
        Graphics g,
        WeatherCondition condition,
        bool isDay,
        float x,
        float y,
        float width,
        float height,
        bool active)
    {
        var cx = x + width / 2f;
        var cy = y + height / 2f;
        var opacity = active ? 1f : 0.28f;

        if (condition == WeatherCondition.Clear)
        {
            if (isDay)
            {
                DrawSun(g, cx, cy, 23f, opacity);
            }
            else
            {
                DrawMoon(g, cx, cy, 27f, opacity);
            }
            return;
        }

        var celestialX = cx + 17f;
        var celestialY = cy - 17f;
        if (isDay)
        {
            DrawSun(g, celestialX, celestialY, 13f, opacity);
        }
        else
        {
            DrawMoon(g, celestialX, celestialY, 15f, opacity);
        }

        var cloudColor = condition switch
        {
            WeatherCondition.Rain => Color.FromArgb((int)(0.48f * 255), (int)(0.68f * 255), (int)(0.86f * 255)),
            WeatherCondition.Storm => Color.FromArgb((int)(0.56f * 255), (int)(0.62f * 255), (int)(0.72f * 255)),
            _ => White(0.58f)
        };

        DrawCloud(g, cx - 8f, cy + 10f, 1.25f, WithAlpha(cloudColor, opacity), condition != WeatherCondition.Cloudy);

        if (condition == WeatherCondition.Rain)
        {
            DrawRain(g, cx - 8f, cy + 25f, opacity);
        }
        else if (condition == WeatherCondition.Storm)
        {
            DrawRain(g, cx - 8f, cy + 25f, opacity * 0.7f);
            DrawLightning(g, cx + 4f, cy + 12f, opacity);
        }
    }

    private static void DrawSun(Graphics g, float cx, float cy, float radius, float opacity)
    {
        using (var glow = new SolidBrush(WithAlpha(Yellow, 0.08f * opacity)))
        {
            g.FillEllipse(glow, cx - radius * 1.7f, cy - radius * 1.7f, radius * 3.4f, radius * 3.4f);
            g.FillEllipse(glow, cx - radius * 1.35f, cy - radius * 1.35f, radius * 2.7f, radius * 2.7f);
        }

        using var pen = new Pen(WithAlpha(Yellow, 0.85f * opacity), radius * 0.16f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        for (var i = 0; i < 8; i++)
        {
            var angle = i * 45.0;
            var inner = radius * 1.30f;
            var outer = radius * 1.75f;
            g.DrawLine(pen, PointAt(cx, cy, inner, angle), PointAt(cx, cy, outer, angle));
        }

        using var fill = new SolidBrush(WithAlpha(Yellow, opacity));
        g.FillEllipse(fill, cx - radius, cy - radius, radius * 2f, radius * 2f);
    }

    private static void DrawMoon(Graphics g, float cx, float cy, float radius, float opacity)
    {
        using (var glow = new SolidBrush(WithAlpha(MoonLight, 0.10f * opacity)))
        {
            g.FillEllipse(glow, cx - radius * 1.65f, cy - radius * 1.65f, radius * 3.3f, radius * 3.3f);
        }

        using (var path = new GraphicsPath())
        {
            path.AddEllipse(cx - radius, cy - radius, radius * 2f, radius * 2f);
            using var brush = new PathGradientBrush(path)
            {
                CenterColor = WithAlpha(Color.FromArgb(250, 252, 255), opacity),
                SurroundColors = new[] { WithAlpha(Color.FromArgb(204, 219, 250), opacity) }
            };
            g.FillPath(brush, path);
        }

        using (var crater = new SolidBrush(WithAlpha(Color.FromArgb(72, 84, 112), 0.18f * opacity)))
        {
            g.FillEllipse(crater, cx - radius * 0.55f, cy - radius * 0.50f, radius * 0.42f, radius * 0.42f);
            g.FillEllipse(crater, cx + radius * 0.15f, cy - radius * 0.10f, radius * 0.25f, radius * 0.25f);
            g.FillEllipse(crater, cx - radius * 0.10f, cy + radius * 0.35f, radius * 0.18f, radius * 0.18f);
        }

        using var outline = new Pen(WithAlpha(Theme.TextPrimary, 0.20f * opacity), 0.7f);
        g.DrawEllipse(outline, cx - radius, cy - radius, radius * 2f, radius * 2f);
    }

    private static void DrawCloud(Graphics g, float cx, float cy, float scale, Color color, bool rainCloud)
    {
        using var brush = new SolidBrush(color);
        var w = 26f * scale;
        var h = 17f * scale;
        g.FillEllipse(brush, cx - w, cy - h * 0.55f, w * 1.15f, h * 1.1f);
        g.FillEllipse(brush, cx - w * 0.35f, cy - h * 0.95f, w * 0.95f, h * 1.15f);
        g.FillEllipse(brush, cx + w * 0.20f, cy - h * 0.55f, w * 0.9f, h * 1.0f);
        g.FillRectangle(brush, cx - w * 0.95f, cy, w * 1.95f, h * 0.9f);

        if (rainCloud)
        {
            using var shade = new SolidBrush(WithAlpha(Color.Black, 0.08f));
            g.FillEllipse(shade, cx - w * 0.75f, cy + h * 0.55f, w * 1.55f, h * 0.55f);
        }
    }

    private static void DrawRain(Graphics g, float cx, float y, float opacity)
    {
        using var pen = new Pen(WithAlpha(Cyan, 0.72f * opacity), 2.4f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        g.DrawLine(pen, cx - 15f, y + 4f, cx - 20f, y + 17f);
        g.DrawLine(pen, cx - 1f, y + 4f, cx - 6f, y + 17f);
        g.DrawLine(pen, cx + 13f, y + 4f, cx + 8f, y + 17f);
    }

    private static void DrawLightning(Graphics g, float cx, float y, float opacity)
    {
        using var brush = new SolidBrush(WithAlpha(Yellow, opacity));
        var points = new[]
        {
            new PointF(cx + 6f, y),
            new PointF(cx - 7f, y + 17f),
            new PointF(cx + 1f, y + 17f),
            new PointF(cx - 6f, y + 32f),
            new PointF(cx + 14f, y + 12f),
            new PointF(cx + 4f, y + 12f)
        };
        g.FillPolygon(brush, points);
    }

    // ---------------------------------------------------------------------
    // Large panels
    // ---------------------------------------------------------------------

    private void DrawDamagePanel(Graphics g, Snap snap, float x, float y, float width, float height)
    {
        var truck = snap.Active && snap.TruckDmg is { } truckValue ? (double?)truckValue : null;
        var trailer = snap.Active && snap.TrailerDmg is { } trailerValue ? (double?)trailerValue : null;
        var cargo = snap.Active && snap.CargoDmg is { } cargoValue ? (double?)cargoValue : null;
        var max = new[] { truck, trailer, cargo }.Where(v => v.HasValue).Select(v => v!.Value).DefaultIfEmpty(0).Max();
        var hasData = truck != null || trailer != null || cargo != null;

        var headerColor = !hasData
            ? White(0.32f)
            : max >= 50 ? Red
            : max >= 20 ? Orange
            : max >= 5 ? Yellow
            : Green;

        var status = !hasData
            ? "SIN DATOS"
            : max >= 50 ? "CRÍTICO"
            : max >= 20 ? "REVISAR"
            : max >= 5 ? "LEVE"
            : "OK";

        DrawPanelFrame(g, x, y, width, height, 28f);

        DrawTintedIcon(g, "advisor-diagnostics", new RectangleF(x + 22f, y + 22f, 20f, 20f), headerColor, 1f);
        DrawTracked(g, Localization.L("DAMAGE"), Rounded(17f, FontStyle.Bold), White(0.62f), x + 54f, y + 34f, 3f);
        DrawText(g, status, Mono(12f, FontStyle.Bold), WithAlpha(headerColor, 0.85f),
            new RectangleF(x + width - 130f, y + 24f, 106f, 20f), Right);

        DrawDamageRow(g, x + 24f, y + 68f, width - 48f, "CAMIÓN", truck);
        DrawDamageRow(g, x + 24f, y + 119f, width - 48f, "TRÁILER", trailer);
        DrawDamageRow(g, x + 24f, y + 170f, width - 48f, "CARGA", cargo);

        DrawText(g, "DAÑO", Rounded(9f, FontStyle.Bold), White(0.28f),
            new RectangleF(x + 24f, y + 242f, 50f, 14f), Left);
        DrawText(g, "5% · 20% · 50%", Mono(9f, FontStyle.Regular), Theme.TextTertiary,
            new RectangleF(x + 74f, y + 242f, 160f, 14f), Left);
    }

    private void DrawDamageRow(Graphics g, float x, float y, float width, string label, double? value)
    {
        const float labelWidth = 64f;
        const float percentWidth = 44f;
        const float gap = 8f;

        var fraction = value == null ? 0 : Math.Clamp(value.Value / 100, 0, 1);
        var color = DamageColor(value);
        var text = value == null ? "--" : $"{Math.Round(value.Value).ToString("0", Es)}%";
        var centerY = y + 12f;

        DrawTracked(g, label, Rounded(11f, FontStyle.Bold), Theme.TextSecondary,
            x, centerY, 1.5f);
        DrawProgressBar(g, x + labelWidth + gap, y + 9f,
            width - labelWidth - percentWidth - gap * 2f, 6f, fraction, color);
        DrawText(g, text, Mono(14f, FontStyle.Bold), color,
            new RectangleF(x + width - percentWidth - 40f, y - 2f, percentWidth + 40f, 26f), Right);
    }

    private void DrawCargoPanel(Graphics g, Snap snap, float x, float y, float width, float height)
    {
        var loaded = snap.Active && snap.CargoLoaded;
        var cargoName = loaded && !string.IsNullOrWhiteSpace(snap.CargoName) ? snap.CargoName : null;
        var cargoId = loaded && !string.IsNullOrWhiteSpace(snap.CargoId) ? snap.CargoId : null;
        var mass = loaded && snap.CargoMass is { } cargoMass && cargoMass > 0 ? (double?)cargoMass : null;
        var damage = loaded && snap.CargoDmg is { } cargoDamage ? (double?)cargoDamage : null;
        var accent = !snap.Active || !loaded ? White(0.25f) : DamageColor(damage ?? 0);
        var damageColor = DamageColor(damage);

        DrawPanelFrame(g, x, y, width, height, 28f);

        DrawBoxIcon(g, x + 22f, y + 22f, 20f, accent);
        DrawTracked(g, Localization.L("CARGO"), Rounded(17f, FontStyle.Bold), White(0.62f), x + 54f, y + 34f, 3f);
        DrawText(g, !snap.Active ? "SIN SEÑAL" : loaded ? "CARGADA" : "VACÍA",
            Mono(12f, FontStyle.Bold), WithAlpha(accent, 0.85f),
            new RectangleF(x + width - 130f, y + 24f, 106f, 20f), Right);

        DrawFitted(g, cargoName ?? "SIN CARGA", "Bahnschrift", 26f, FontStyle.Bold, White(0.92f),
            new RectangleF(x + 22f, y + 58f, width - 44f, 36f), LeftTop, 14f);
        DrawFitted(g, cargoId == null ? "TIPO · --" : $"TIPO · {cargoId}", "Consolas", 11f, FontStyle.Bold,
            White(0.34f), new RectangleF(x + 22f, y + 90f, width - 44f, 18f), Left, 7f);

        DrawText(g, "PESO", Rounded(10f, FontStyle.Bold), White(0.38f),
            new RectangleF(x + 22f, y + 120f, 70f, 26f), Left);
        var massText = mass == null ? "-- kg" : $"{Math.Round(mass.Value).ToString("N0", Es)} kg";
        DrawFitted(g, massText, "Bahnschrift", 22f, FontStyle.Bold, White(0.92f),
            new RectangleF(x + width - 230f, y + 118f, 160f, 30f), Right, 12f);
        if (mass != null)
        {
            DrawText(g, $"{(mass.Value / 1000).ToString("N1", Es)} t", Mono(11f, FontStyle.Bold), White(0.38f),
                new RectangleF(x + width - 66f, y + 124f, 44f, 18f), Right);
        }

        DrawText(g, "INTEGRIDAD", Rounded(9f, FontStyle.Bold), White(0.34f),
            new RectangleF(x + 22f, y + 158f, 120f, 16f), Left);
        DrawText(g, damage == null ? "--" : $"{Math.Round((100 - damage.Value)).ToString("0", Es)}%",
            Mono(13f, FontStyle.Bold), damageColor,
            new RectangleF(x + width - 100f, y + 156f, 78f, 18f), Right);
        DrawProgressBar(g, x + 22f, y + 178f, width - 44f, 6f,
            damage == null ? 0 : Math.Clamp(1 - damage.Value / 100, 0, 1), damageColor);

        DrawText(g, snap.SpecialJob ? "TRABAJO ESPECIAL" : "TRABAJO ESTÁNDAR",
            Rounded(9f, FontStyle.Bold),
            snap.SpecialJob ? WithAlpha(Yellow, 0.85f) : White(0.30f),
            new RectangleF(x + 22f, y + 244f, 190f, 16f), Left);
        DrawText(g, loaded ? "EN REMOLQUE" : "SIN REMOLQUE", Rounded(9f, FontStyle.Bold), White(0.30f),
            new RectangleF(x + width - 140f, y + 244f, 118f, 16f), Right);
    }

    private void DrawGpsPanel(Graphics g, Snap snap, float x, float y, float width, float height)
    {
        var heading = snap.Active && snap.Heading is { } headingValue
            ? (double?)((headingValue % 1.0 + 1.0) % 1.0)
            : null;
        var bearing = heading == null ? (double?)null : ((1.0 - heading.Value) * 360.0 % 360.0 + 360.0) % 360.0;
        var accent = snap.Active ? Cyan : White(0.25f);
        var destination = snap.Active && snap.OnJob && !string.IsNullOrWhiteSpace(snap.CityDst)
            ? snap.CityDst!
            : "SIN DESTINO";
        var origin = snap.Active && !string.IsNullOrWhiteSpace(snap.CitySrc)
            ? snap.CitySrc!
            : "SIN ORIGEN";

        DrawPanelFrame(g, x, y, width, height, 28f);

        DrawLocationIcon(g, x + 22f, y + 22f, 20f, accent);
        DrawTracked(g, Localization.L("GPS"), Rounded(17f, FontStyle.Bold), White(0.62f), x + 54f, y + 34f, 3f);
        DrawText(g, !snap.Active ? "SIN SEÑAL" : snap.OnJob ? "EN RUTA" : "SIN RUTA",
            Mono(12f, FontStyle.Bold),
            !snap.Active ? White(0.25f) : snap.OnJob ? WithAlpha(Green, 0.80f) : White(0.25f),
            new RectangleF(x + width - 130f, y + 24f, 106f, 20f), Right);

        var limitKmh = snap.Active && snap.Limit is { } limit && limit > 0.5f
            ? limit * 3.6
            : (double?)null;
        if (limitKmh is { } shownLimit)
        {
            DrawSpeedLimitSign(g, shownLimit, x + width - 20f, y + 52f, 50f);
        }

        DrawCompass(g, x + 20f, y + 54f, 108f, bearing, accent);

        var textX = x + 144f;
        DrawText(g, "RUMBO", Rounded(10f, FontStyle.Bold), White(0.35f),
            new RectangleF(textX, y + 54f, 220f, 16f), Left);
        DrawText(g, Cardinal(bearing), Rounded(34f, FontStyle.Bold), accent,
            new RectangleF(textX, y + 69f, 60f, 42f), Left);
        DrawText(g, bearing == null ? "--" : $"{Math.Round(bearing.Value).ToString("0", Es)}°",
            Mono(16f, FontStyle.Bold), White(0.55f),
            new RectangleF(textX + 68f, y + 82f, 90f, 22f), Left);
        DrawText(g, "DESTINO", Rounded(10f, FontStyle.Bold), White(0.35f),
            new RectangleF(textX, y + 113f, 220f, 16f), Left);
        DrawFitted(g, destination, "Bahnschrift", 20f, FontStyle.Bold, White(0.90f),
            new RectangleF(textX, y + 128f, width - 164f, 30f), Left, 12f);

        using (var divider = new Pen(Ink(15), 1f))
        {
            g.DrawLine(divider, x + 20f, y + 176f, x + width - 20f, y + 176f);
        }

        DrawText(g, "ORIGEN", Rounded(9f, FontStyle.Bold), White(0.30f),
            new RectangleF(x + 20f, y + 186f, 100f, 14f), Left);
        DrawFitted(g, origin, "Bahnschrift", 13f, FontStyle.Regular, White(0.55f),
            new RectangleF(x + 20f, y + 202f, 185f, 20f), Left, 7f);
        DrawText(g, "RUTA RESTANTE", Rounded(9f, FontStyle.Bold), White(0.30f),
            new RectangleF(x + width - 190f, y + 186f, 170f, 14f), Right);
        DrawText(g, RouteText(snap), Mono(13f, FontStyle.Bold), White(0.70f),
            new RectangleF(x + width - 190f, y + 202f, 170f, 20f), Right);

        DrawText(g, CoordsText(snap), Mono(10f, FontStyle.Bold), White(0.28f),
            new RectangleF(x + 20f, y + 242f, width - 40f, 18f), Left);
    }

    private void DrawSpeedLimitSign(Graphics g, double limitKmh, float right, float top, float size)
    {
        var rect = new RectangleF(right - size, top, size, size);

        using (var fill = new SolidBrush(Color.FromArgb(245, 255, 255, 255)))
        {
            g.FillEllipse(fill, rect);
        }

        using (var ring = new Pen(Red, Math.Max(3.2f, size * 0.07f)))
        {
            g.DrawEllipse(ring, rect);
        }

        var text = Math.Round(limitKmh).ToString("0", Es);
        DrawFitted(g, text, "Bahnschrift", size * 0.44f, FontStyle.Bold, Color.Black,
            new RectangleF(rect.X + 6f, rect.Y + 2f, rect.Width - 12f, rect.Height - 4f), Center, 12f);
    }

    private void DrawCompass(Graphics g, float x, float y, float size, double? bearing, Color accent)
    {
        var cx = x + size / 2f;
        var cy = y + size / 2f;
        var radius = size * 0.46f;

        using (var fill = new SolidBrush(Ink(6)))
        {
            g.FillEllipse(fill, cx - radius, cy - radius, radius * 2f, radius * 2f);
        }
        using (var ring = new Pen(Ink(26), 1f))
        {
            g.DrawEllipse(ring, cx - radius, cy - radius, radius * 2f, radius * 2f);
        }

        for (var i = 0; i < 36; i++)
        {
            var angle = i * 10.0;
            var major = i % 9 == 0;
            var outer = radius * 0.92f;
            var inner = radius * (major ? 0.78f : 0.84f);
            using var pen = new Pen(Ink(major ? 133 : 41), major ? 1.6f : 1f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };
            g.DrawLine(pen, CompassPoint(cx, cy, outer, angle), CompassPoint(cx, cy, inner, angle));
        }

        var labels = new[] { (0.0, "N"), (90.0, "E"), (180.0, "S"), (270.0, "O") };
        foreach (var (angle, label) in labels)
        {
            var p = CompassPoint(cx, cy, radius * 0.62f, angle);
            DrawText(g, label, Rounded(size * 0.12f, FontStyle.Bold), White(0.50f),
                new RectangleF(p.X - 14f, p.Y - 12f, 28f, 24f), Center);
        }

        var needleAngle = bearing ?? 0;
        var needleColor = bearing == null ? WithAlpha(accent, 0.30f) : accent;
        var tip = CompassPoint(cx, cy, radius * 0.50f, needleAngle);
        var left = CompassPoint(cx, cy, radius * 0.10f, needleAngle - 90);
        var right = CompassPoint(cx, cy, radius * 0.10f, needleAngle + 90);

        using (var needle = new SolidBrush(needleColor))
        {
            g.FillPolygon(needle, new[] { tip, left, right });
        }

        using (var black = new SolidBrush(Color.Black))
        {
            g.FillEllipse(black, cx - size * 0.035f, cy - size * 0.035f, size * 0.070f, size * 0.070f);
        }
        using (var hub = new Pen(WithAlpha(needleColor, 0.85f), 1.5f))
        {
            g.DrawEllipse(hub, cx - size * 0.035f, cy - size * 0.035f, size * 0.070f, size * 0.070f);
        }
    }

    private static string Cardinal(double? bearing)
    {
        if (bearing == null)
        {
            return "--";
        }

        var names = new[] { "N", "NE", "E", "SE", "S", "SO", "O", "NO" };
        return names[(int)((bearing.Value + 22.5) / 45.0) % names.Length];
    }

    private static string RouteText(Snap snap)
    {
        if (!snap.Active || !snap.OnJob || snap.RouteD is not { } distance || distance <= 0)
        {
            return "--";
        }

        var km = Math.Round(distance / 1000.0).ToString("N0", Es);
        var eta = FormatEta(snap.RouteT);
        return eta == null ? $"{km} km" : $"{km} km · {eta}";
    }

    private static string CoordsText(Snap snap)
    {
        if (!snap.Active || snap.PosX is not { } x || snap.PosZ is not { } z)
        {
            return "X --   Z --";
        }

        return $"X {x.ToString("N0", Es)}   Z {z.ToString("N0", Es)}";
    }

    // ---------------------------------------------------------------------
    // Painting helpers
    // ---------------------------------------------------------------------

    private Font Rounded(float size, FontStyle style = FontStyle.Regular)
    {
        return GetFont("Inter", size, style);
    }

    private Font Ui(float size, FontStyle style = FontStyle.Regular)
    {
        return GetFont("Inter", size, style);
    }

    private Font Mono(float size, FontStyle style = FontStyle.Regular)
    {
        return GetFont("Consolas", size, style);
    }

    private Font GetFont(string family, float size, FontStyle style)
    {
        var scaledSize = size * Theme.UiScale;
        var key = $"{family}|{scaledSize:0.0}|{(int)style}";
        if (!_fonts.TryGetValue(key, out var font))
        {
            try
            {
                font = new ATSTelemetry.SkiaCompat.SkiaFont(family, scaledSize, style, GraphicsUnit.Pixel);
            }
            catch
            {
                font = new Font("Segoe UI", scaledSize, style, GraphicsUnit.Pixel);
            }
            _fonts[key] = font;
        }
        return font;
    }

    private void DrawFitted(
        Graphics g,
        string text,
        string family,
        float size,
        FontStyle style,
        Color color,
        RectangleF rect,
        TextFormat format,
        float minimumSize)
    {
        var font = GetFont(family, size, style);
        while (size > minimumSize && g.MeasureString(text, font).Width > rect.Width + 1f)
        {
            size -= 0.5f;
            font = GetFont(family, size, style);
        }

        using var brush = new SolidBrush(color);
        g.DrawString(text, font, brush, rect, format);
    }

    private static void DrawText(Graphics g, string text, Font font, Color color, RectangleF rect, TextFormat format)
    {
        using var brush = new SolidBrush(color);
        g.DrawString(text, font, brush, rect, format);
    }

    private static float MeasureTracked(Graphics g, string text, Font font, float tracking)
    {
        var width = 0f;
        foreach (var character in text)
        {
            width += g.MeasureString(character.ToString(), font, PointF.Empty, Typographic).Width;
        }
        return width + tracking * Math.Max(0, text.Length - 1);
    }

    private static void DrawTracked(
        Graphics g,
        string text,
        Font font,
        Color color,
        float x,
        float centerY,
        float tracking,
        TextFormat alignment = TextFormat.Left)
    {
        var total = MeasureTracked(g, text, font, tracking);
        var startX = alignment switch
        {
            TextFormat.Center => x - total / 2f,
            TextFormat.Right => x - total,
            _ => x
        };

        var top = centerY - font.GetHeight(g) / 2f;
        using var brush = new SolidBrush(color);
        foreach (var character in text)
        {
            var token = character.ToString();
            g.DrawString(token, font, brush, startX, top, Typographic);
            startX += g.MeasureString(token, font, PointF.Empty, Typographic).Width + tracking;
        }
    }

    private static PointF PointAt(float cx, float cy, float radius, double degrees)
    {
        var radians = degrees * Math.PI / 180.0;
        return new PointF(
            cx + (float)Math.Cos(radians) * radius,
            cy + (float)Math.Sin(radians) * radius);
    }

    private static PointF CompassPoint(float cx, float cy, float radius, double degrees)
    {
        var radians = degrees * Math.PI / 180.0;
        return new PointF(
            cx + (float)Math.Sin(radians) * radius,
            cy - (float)Math.Cos(radians) * radius);
    }

    private static GraphicsPath RoundedPath(RectangleF rect, float radius)
    {
        var path = new GraphicsPath();
        var r = Math.Min(radius, Math.Min(rect.Width, rect.Height) / 2f);
        if (r <= 0.01f)
        {
            path.AddRectangle(rect);
            return path;
        }

        // Contorno continuo: arcos circulares unidos por los lados rectos.
        const int segments = 12;
        var points = new List<PointF>(segments * 4 + 8);
        AddCircularCorner(points, rect.Right - r, rect.Y + r, r, -90, 0, segments);
        AddCircularCorner(points, rect.Right - r, rect.Bottom - r, r, 0, 90, segments);
        AddCircularCorner(points, rect.X + r, rect.Bottom - r, r, 90, 180, segments);
        AddCircularCorner(points, rect.X + r, rect.Y + r, r, 180, 270, segments);

        path.MoveTo(points[0].X, points[0].Y);
        for (var i = 1; i < points.Count; i++)
        {
            path.LineTo(points[i].X, points[i].Y);
        }
        path.CloseFigure();
        return path;
    }

    private static void AddCircularCorner(
        List<PointF> points,
        float centerX,
        float centerY,
        float radius,
        double startDegrees,
        double endDegrees,
        int segments)
    {
        var start = startDegrees * Math.PI / 180.0;
        var end = endDegrees * Math.PI / 180.0;

        for (var i = 0; i <= segments; i++)
        {
            var angle = start + (end - start) * i / segments;
            points.Add(new PointF(
                centerX + radius * (float)Math.Cos(angle),
                centerY + radius * (float)Math.Sin(angle)));
        }
    }

    private static void DrawRounded(
        Graphics g,
        RectangleF rect,
        float radius,
        Color fill,
        Color stroke,
        float strokeWidth)
    {
        using var path = RoundedPath(rect, radius);
        using var brush = new SolidBrush(fill);
        g.FillPath(brush, path);

        if (stroke.A > 0 && strokeWidth > 0)
        {
            using var pen = new Pen(stroke, strokeWidth);
            g.DrawPath(pen, path);
        }
    }

    private static GraphicsPath SquirclePath(RectangleF rect, float radius)
    {
        var path = new GraphicsPath();
        var r = Math.Min(radius, Math.Min(rect.Width, rect.Height) / 2f);
        if (r <= 0.01f)
        {
            path.AddRectangle(rect);
            return path;
        }

        const int segments = 16;
        var points = new List<PointF>(segments * 4 + 8);
        AddSquircleCorner(points, rect.X + r, rect.Y + r, r, 180, 270, segments);
        points.Add(new PointF(rect.Right - r, rect.Y));
        AddSquircleCorner(points, rect.Right - r, rect.Y + r, r, 270, 360, segments);
        points.Add(new PointF(rect.Right, rect.Bottom - r));
        AddSquircleCorner(points, rect.Right - r, rect.Bottom - r, r, 0, 90, segments);
        points.Add(new PointF(rect.X + r, rect.Bottom));
        AddSquircleCorner(points, rect.X + r, rect.Bottom - r, r, 90, 180, segments);

        path.MoveTo(points[0].X, points[0].Y);
        for (var i = 1; i < points.Count; i++)
        {
            path.LineTo(points[i].X, points[i].Y);
        }
        path.CloseFigure();
        return path;
    }

    private static void AddSquircleCorner(
        List<PointF> points,
        float centerX,
        float centerY,
        float radius,
        double startDegrees,
        double endDegrees,
        int segments)
    {
        // Superelipse n=4: esquinas más suaves y continuas que un arco circular.
        const double exponent = 2.0 / 4.0;
        var start = startDegrees * Math.PI / 180.0;
        var end = endDegrees * Math.PI / 180.0;

        for (var i = 0; i <= segments; i++)
        {
            var angle = start + (end - start) * i / segments;
            var cos = Math.Cos(angle);
            var sin = Math.Sin(angle);
            var x = centerX + radius * Math.Sign(cos) * Math.Pow(Math.Abs(cos), exponent);
            var y = centerY + radius * Math.Sign(sin) * Math.Pow(Math.Abs(sin), exponent);
            points.Add(new PointF((float)x, (float)y));
        }
    }

    // ---------------------------------------------------------------------
    // Componentes reutilizables (Fase 5.2)
    // ---------------------------------------------------------------------
    private void DrawSectionHeader(
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

    private void DrawMetricRow(
        Graphics g, float x, float y, float width,
        string label, string value, Color color)
    {
        DrawText(g, label, Rounded(TypeScale.Caption, FontStyle.Regular),
            Theme.TextTertiary, new RectangleF(x, y, width * 0.55f, 18f), Left);
        DrawFitted(g, value, "Bahnschrift", TypeScale.Body, FontStyle.Bold, color,
            new RectangleF(x + width * 0.55f, y - 2f, width * 0.45f, 22f), Right, 11f);
    }

    private void DrawChip(
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

    private void DrawEmptyState(
        Graphics g, RectangleF area, string message, string? hint = null)
    {
        var blockHeight = hint != null ? 46f : 22f;
        var startY = area.Y + (area.Height - blockHeight) / 2f;

        DrawFitted(g, message, "Bahnschrift", TypeScale.Body, FontStyle.Bold,
            Theme.TextDisabled, new RectangleF(area.X, startY, area.Width, 22f), Center, 11f);

        if (hint != null)
        {
            DrawFitted(g, hint, "Segoe UI", TypeScale.Caption, FontStyle.Regular,
                Theme.TextDisabled,
                new RectangleF(area.X, startY + 24f, area.Width, 20f), Center, 10f);
        }
    }

    private void DrawOnboardingCard(Graphics g, float centerX, float top)
    {
        const float width = 520f;
        const float height = 200f;
        var x = centerX - width / 2f;
        var y = top;

        DrawGlassPanelCore(g, x, y, width, height, Theme.RadiusCard);

        DrawText(g, "SIN CONEXIÓN AL JUEGO", Rounded(18f, FontStyle.Bold), Theme.TextPrimary,
            new RectangleF(x, y + 18f, width, 26f), Center);
        DrawText(g, "Para ver telemetría en vivo:", Rounded(13f, FontStyle.Regular), Theme.TextSecondary,
            new RectangleF(x, y + 52f, width, 20f), Center);

        DrawText(g, "1. Instala el plugin de telemetría (install-plugin.bat)",
            Rounded(12f, FontStyle.Regular), Theme.TextSecondary,
            new RectangleF(x + 44f, y + 90f, width - 68f, 20f), Left);
        DrawText(g, "2. Abre ATS o ETS2",
            Rounded(12f, FontStyle.Regular), Theme.TextSecondary,
            new RectangleF(x + 44f, y + 116f, width - 68f, 20f), Left);
        DrawVectorIcon(g, "plug", new RectangleF(x + 20f, y + 88f, 16f, 16f), Cyan);
        DrawVectorIcon(g, "truck", new RectangleF(x + 20f, y + 114f, 16f, 16f), Cyan);
        DrawText(g, "La app detecta la conexión automáticamente.",
            Rounded(11f, FontStyle.Regular), Theme.TextTertiary,
            new RectangleF(x, y + 160f, width, 18f), Center);
    }

    /// <summary>
    /// Iconos vectoriales con grid de 24 px y trazo uniforme de 1,75 px. Se
    /// usan en estados vacíos y onboarding; los PNG siguen como fallback en
    /// los testigos del cluster mientras se migran.
    /// </summary>
    private static void DrawVectorIcon(
        Graphics g, string kind, RectangleF rect, Color color, float stroke = 1.75f)
    {
        var scale = Math.Min(rect.Width, rect.Height) / 24f;
        var ox = rect.X + (rect.Width - 24f * scale) / 2f;
        var oy = rect.Y + (rect.Height - 24f * scale) / 2f;

        PointF P(float x, float y) => new(ox + x * scale, oy + y * scale);

        using var pen = new Pen(color, stroke * scale)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };

        switch (kind)
        {
            case "plug":
                g.DrawRectangle(pen, ox + 7f * scale, oy + 5f * scale,
                    10f * scale, 9f * scale);
                g.DrawLine(pen, P(10f, 5f), P(10f, 1.5f));
                g.DrawLine(pen, P(14f, 5f), P(14f, 1.5f));
                g.DrawLine(pen, P(12f, 14f), P(12f, 19f));
                g.DrawLine(pen, P(9f, 19f), P(15f, 19f));
                break;

            case "truck":
                g.DrawRectangle(pen, ox + 2f * scale, oy + 7f * scale,
                    12f * scale, 8f * scale);
                g.DrawLine(pen, P(14f, 10f), P(19f, 10f));
                g.DrawLine(pen, P(19f, 10f), P(21f, 13f));
                g.DrawLine(pen, P(21f, 13f), P(21f, 15f));
                g.DrawLine(pen, P(14f, 15f), P(21f, 15f));
                g.DrawEllipse(pen, ox + 4f * scale, oy + 15f * scale,
                    4f * scale, 4f * scale);
                g.DrawEllipse(pen, ox + 16f * scale, oy + 15f * scale,
                    4f * scale, 4f * scale);
                break;

            case "alert":
            default:
                g.DrawLine(pen, P(12f, 3f), P(21f, 20f));
                g.DrawLine(pen, P(21f, 20f), P(3f, 20f));
                g.DrawLine(pen, P(3f, 20f), P(12f, 3f));
                g.DrawLine(pen, P(12f, 9f), P(12f, 14f));
                g.DrawLine(pen, P(12f, 17f), P(12.1f, 17f));
                break;
        }
    }

    private static void DrawSquircle(
        Graphics g,
        RectangleF rect,
        float radius,
        Color fill,
        Color stroke,
        float strokeWidth)
    {
        var fillRect = new RectangleF(rect.X - 1f, rect.Y - 1f, rect.Width + 2f, rect.Height + 2f);
        using var fillPath = SquirclePath(fillRect, radius + 1f);
        using var path = SquirclePath(rect, radius);
        using var brush = new SolidBrush(fill);
        g.FillPath(brush, fillPath);

        if (stroke.A > 0 && strokeWidth > 0)
        {
            using var pen = new Pen(stroke, strokeWidth);
            g.DrawPath(pen, path);
        }
    }

    private static void DrawPanelFrame(Graphics g, float x, float y, float width, float height, float radius)
    {
        // Marco limpio: línea blanca fina al 36% de opacidad (10% menos visible), sin relleno.
        using var path = SquirclePath(new RectangleF(x + 0.5f, y + 0.5f, width - 1f, height - 1f), radius);
        using var pen = new Pen(Ink(92), 1f);
        g.DrawPath(pen, path);
    }

    private static void DrawProgressBar(
        Graphics g,
        float x,
        float y,
        float width,
        float height,
        double fraction,
        Color color)
    {
        DrawRounded(g, new RectangleF(x, y, width, height), height / 2f,
            Ink(18), Color.Transparent, 0f);

        var fillWidth = (float)(width * Math.Clamp(fraction, 0, 1));
        if (fillWidth > 0.5f)
        {
            DrawRounded(g, new RectangleF(x, y, fillWidth, height), height / 2f,
                color, Color.Transparent, 0f);
        }
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        return Color.FromArgb(
            (int)Math.Clamp(alpha * 255f, 0f, 255f),
            color.R,
            color.G,
            color.B);
    }

    private static Color White(float alpha)
    {
        return Theme.Alpha(Theme.TextPrimary, alpha);
    }

    private static Color Ink(int alpha)
    {
        return Color.FromArgb(alpha, Theme.TextPrimary.R, Theme.TextPrimary.G, Theme.TextPrimary.B);
    }

    private static Color DamageColor(double? value)
    {
        if (value == null)
        {
            return Theme.TextDisabled;
        }
        if (value >= 50) return Red;
        if (value >= 20) return Orange;
        if (value >= 5) return Yellow;
        return Green;
    }

    private static string DamageLabel(double? value)
    {
        if (value == null) return "SIN DATOS";
        if (value >= 50) return "CRÍTICO";
        if (value >= 20) return "REVISAR";
        if (value >= 5) return "LEVE";
        return "OK";
    }

    private static ColorMatrix AlphaMatrix(float alpha)
    {
        return new ColorMatrix { Alpha = alpha };
    }

    private static ColorMatrix ColorTintMatrix(Color color, float alpha)
    {
        return new ColorMatrix { IsTint = true, Tint = color, Alpha = alpha };
    }

    private Image? Icon(string name)
    {
        if (_icons.TryGetValue(name, out var cached))
        {
            return cached;
        }

        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Assets", "Indicators", name + ".png");
            var image = File.Exists(path) ? Image.FromFile(path) : null;
            _icons[name] = image;
            return image;
        }
        catch
        {
            _icons[name] = null;
            return null;
        }
    }

    private Image? CompanyIcon(string name)
    {
        var key = "companyicon:" + name;
        if (_icons.TryGetValue(key, out var cached))
        {
            return cached;
        }

        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Assets", "Icons", name + ".png");
            var image = File.Exists(path) ? Image.FromFile(path) : null;
            _icons[key] = image;
            return image;
        }
        catch
        {
            _icons[key] = null;
            return null;
        }
    }

    private Image? BrandLogo(string name)
    {
        var key = "brand:" + name;
        if (_icons.TryGetValue(key, out var cached))
        {
            return cached;
        }

        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Assets", "BrandLogos", name + ".png");
            var image = File.Exists(path) ? Image.FromFile(path) : null;
            _icons[key] = image;
            return image;
        }
        catch
        {
            _icons[key] = null;
            return null;
        }
    }

    private Image? CompanyLogoImage(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var safe = new string(name.Where(c => char.IsLetterOrDigit(c) || c is '_' or '-').ToArray());
        if (safe.Length == 0)
        {
            return null;
        }

        var key = "company:" + safe;
        if (_icons.TryGetValue(key, out var cached))
        {
            return cached;
        }

        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Assets", "CompanyLogos", safe + ".png");
            var image = File.Exists(path) ? Image.FromFile(path) : null;
            _icons[key] = image;
            return image;
        }
        catch
        {
            _icons[key] = null;
            return null;
        }
    }

    private Image? TruckImage(Snap snap, out string key)
    {
        key = string.Empty;
        if (!snap.Active)
        {
            return null;
        }

        foreach (var candidate in TruckImageCandidates(snap))
        {
            var image = LoadTruckImage(candidate);
            if (image != null)
            {
                key = candidate;
                return image;
            }
        }

        return null;
    }

    private Image? LoadTruckImage(string name)
    {
        var key = "truck:" + name;
        if (_icons.TryGetValue(key, out var cached))
        {
            return cached;
        }

        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Assets", "TruckImages", name + ".jpg");
            var image = File.Exists(path) ? Image.FromFile(path) : null;
            _icons[key] = image;
            return image;
        }
        catch
        {
            _icons[key] = null;
            return null;
        }
    }

    private Image? CargoImage(Snap snap, out string key)
    {
        key = string.Empty;
        if (!snap.Active || !snap.CargoLoaded)
        {
            return null;
        }

        foreach (var candidate in CargoImageCandidates(snap))
        {
            var image = LoadCargoImage(candidate);
            if (image != null)
            {
                key = candidate;
                return image;
            }
        }

        // Respaldo genérico para cargas sin imagen específica (habitual en ETS2).
        var fallback = LoadCargoImage("fallback");
        if (fallback != null)
        {
            key = "fallback";
            return fallback;
        }

        return null;
    }

    private Image? LoadCargoImage(string name)
    {
        var key = "cargo:" + name;
        if (_icons.TryGetValue(key, out var cached))
        {
            return cached;
        }

        try
        {
            var dir = Path.Combine(AppContext.BaseDirectory, "Assets", "CargoImages");
            string? path = null;
            foreach (var ext in new[] { ".png", ".jpg", ".jpeg" })
            {
                var candidate = Path.Combine(dir, name + ext);
                if (File.Exists(candidate))
                {
                    path = candidate;
                    break;
                }
            }
            var image = path == null ? null : Image.FromFile(path);
            _icons[key] = image;
            return image;
        }
        catch
        {
            _icons[key] = null;
            return null;
        }
    }

    private static IEnumerable<string> CargoImageCandidates(Snap snap)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(snap.CargoId))
        {
            var id = snap.CargoId!.Trim().ToLowerInvariant()
                .Replace("cargo.", "").Replace(" ", "_").Replace("-", "_");
            if (seen.Add(id)) yield return id;
        }
        if (!string.IsNullOrWhiteSpace(snap.CargoName))
        {
            var name = snap.CargoName!.Trim().ToLowerInvariant()
                .Replace(" ", "_").Replace("-", "_");
            if (seen.Add(name)) yield return name;
        }
    }

    private static IEnumerable<string> TruckImageCandidates(Snap snap)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var id = NormalizeTruckKey(snap.TruckId);
        var brand = NormalizeTruckKey(snap.TruckBrand);
        var name = NormalizeTruckKey(snap.TruckName);

        if (id.Length > 0)
        {
            if (id.StartsWith("vehicle_", StringComparison.OrdinalIgnoreCase))
            {
                id = id["vehicle_".Length..];
            }

            if (seen.Add(id))
            {
                yield return id;
            }

            // Variantes habituales de mods: "t800_gtm", "w900_custom", etc.
            foreach (var suffix in new[] { "_gtm", "_mod", "_custom" })
            {
                if (id.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    var trimmed = id[..^suffix.Length];
                    if (seen.Add(trimmed))
                    {
                        yield return trimmed;
                    }
                }
            }
        }

        if (brand.Length > 0 && name.Length > 0)
        {
            var pair = name.StartsWith(brand + "_", StringComparison.OrdinalIgnoreCase)
                ? name
                : $"{brand}_{name}";
            if (seen.Add(pair))
            {
                yield return pair;
            }
        }

        if (name.Length > 0 && seen.Add(name))
        {
            yield return name;
        }
    }

    private static string NormalizeTruckKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new System.Text.StringBuilder(value.Length);
        var lastUnderscore = false;
        foreach (var raw in value.Trim().ToLowerInvariant())
        {
            var character = char.IsLetterOrDigit(raw) ? raw : '_';
            if (character == '_')
            {
                if (lastUnderscore || builder.Length == 0)
                {
                    continue;
                }
                lastUnderscore = true;
            }
            else
            {
                lastUnderscore = false;
            }
            builder.Append(character);
        }

        return builder.ToString().Trim('_');
    }

    private static void DrawBoxIcon(Graphics g, float x, float y, float size, Color color)
    {
        using var pen = new Pen(color, 1.8f);
        var rect = new RectangleF(x, y + size * 0.20f, size, size * 0.68f);
        g.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);
        g.DrawLine(pen, rect.X, rect.Y + rect.Height * 0.36f, rect.Right, rect.Y + rect.Height * 0.36f);
        g.DrawLine(pen, rect.X + rect.Width * 0.36f, rect.Y, rect.X + rect.Width * 0.36f, rect.Bottom);
    }

    private static void DrawLocationIcon(Graphics g, float x, float y, float size, Color color)
    {
        using var pen = new Pen(color, 1.8f);
        var centerX = x + size / 2f;
        var centerY = y + size * 0.38f;
        var radius = size * 0.30f;
        g.DrawEllipse(pen, centerX - radius, centerY - radius, radius * 2f, radius * 2f);
        g.DrawLine(pen, centerX, centerY + radius, centerX, y + size);
    }

    public void Dispose()
    {
        _backdropLayer?.Dispose();
        _backdropLayer = null;
        _panelLayer?.Dispose();
        _panelLayer = null;

        foreach (var image in _icons.Values)
        {
            image?.Dispose();
        }
        _icons.Clear();

        foreach (var font in _fonts.Values)
        {
            font.Dispose();
        }
        _fonts.Clear();
    }
}












