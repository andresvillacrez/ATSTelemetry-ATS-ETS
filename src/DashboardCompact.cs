using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using Font = ATSTelemetry.SkiaCompat.SkiaFont;
using Graphics = ATSTelemetry.SkiaCompat.SkiaGraphics;
using Pen = ATSTelemetry.SkiaCompat.SkiaPen;
using SolidBrush = ATSTelemetry.SkiaCompat.SkiaSolidBrush;
using TextFormat = ATSTelemetry.SkiaCompat.TextFormat;

namespace ATSTelemetry;

/// <summary>
/// Composiciones para ventanas pequeñas. Compact (1280 × 720) y Minimal
/// (960 × 540) tienen canvas propio para conservar la tipografía legible en
/// lugar de encoger el canvas de 1920 × 1080.
/// </summary>
public sealed partial class DashboardRenderer
{
    private void DrawCompactDashboard(
        Graphics g,
        Snap snap,
        DateTime now,
        bool ignitionOn,
        bool bulbCheck,
        double ignitionElapsed,
        DashboardLayout layout)
    {
        DrawCompactHeader(g, snap, now, layout);
        DrawCompactIndicatorRow(g, snap, ignitionOn, bulbCheck, layout.Indicators);
        DrawCompactCluster(g, snap, ignitionOn, bulbCheck, ignitionElapsed,
            layout.ClusterCenter, layout.ClusterRadius);

        DrawCompactFuelCard(g, snap, ignitionElapsed, layout.Fuel);
        DrawCompactWeatherCard(g, snap, layout.Weather);
        DrawCompactFinancePanel(g, snap, layout.Finance);
        DrawCompactDamagePanel(g, snap, layout.Damage);
        DrawCompactCargoPanel(g, snap, layout.Cargo);
        DrawCompactGpsPanel(g, snap, layout.Gps);
        DrawCompactNotices(g, snap, layout.Notices);

        DrawCompactFooter(g, snap, now, layout);
    }

    private void DrawMinimalDashboard(
        Graphics g,
        Snap snap,
        DateTime now,
        bool ignitionOn,
        bool bulbCheck,
        double ignitionElapsed,
        DashboardLayout layout)
    {
        DrawMinimalHeader(g, snap, now, layout);
        DrawCompactIndicatorRow(g, snap, ignitionOn, bulbCheck, layout.Indicators);
        DrawCompactCluster(g, snap, ignitionOn, bulbCheck, ignitionElapsed,
            layout.ClusterCenter, layout.ClusterRadius);

        DrawMinimalFuelCard(g, snap, ignitionElapsed, layout.Fuel);
        DrawMinimalTripCard(g, snap, layout.Trip);
        DrawMinimalNotices(g, snap, layout.Notices);

        DrawCompactFooter(g, snap, now, layout);
    }

    // ---------------------------------------------------------------------
    // Cabecera y pie
    // ---------------------------------------------------------------------

    private void DrawCompactHeader(Graphics g, Snap snap, DateTime now, DashboardLayout layout)
    {
        var header = layout.Header;
        var (statusText, statusColor) = Status(snap);
        var statusFont = Rounded(TypeScale.Caption, FontStyle.Bold);
        var statusWidth = g.MeasureString(statusText, statusFont).Width;

        using (var dot = new SolidBrush(statusColor))
        {
            g.FillEllipse(dot, header.X + 24f, header.Y + 26f, 7f, 7f);
        }

        DrawText(g, statusText, statusFont, WithAlpha(statusColor, 0.95f),
            new RectangleF(header.X + 38f, header.Y + 18f, statusWidth + 6f, 20f), Left);

        var truck = TruckReference(snap);
        var brandText = (snap.TruckBrand ?? string.Empty).Trim().ToUpperInvariant();
        var logo = BrandLogoAsset(snap) is { } logoAsset ? BrandLogo(logoAsset) : null;

        if (logo != null && brandText.Length > 0
            && truck.StartsWith(brandText + " ", StringComparison.OrdinalIgnoreCase))
        {
            var model = truck.Substring(brandText.Length).TrimStart();
            if (model.Length > 0)
            {
                truck = model;
            }
        }

        const float logoHeight = 26f;
        const float logoGap = 12f;
        var logoWidth = logo == null ? 0f : logoHeight * logo.Width / logo.Height;
        var nameFont = Rounded(22f, FontStyle.Bold);
        var nameWidth = g.MeasureString(truck, nameFont).Width;
        var groupWidth = logoWidth + (logo == null ? 0f : logoGap) + nameWidth;
        var groupLeft = layout.CanvasWidth / 2f - groupWidth / 2f;

        if (logo != null)
        {
            DrawBrandLogo(g, logo, new RectangleF(groupLeft, 17f, logoWidth, logoHeight));
        }

        DrawText(g, truck, nameFont, Theme.TextPrimary,
            new RectangleF(groupLeft + logoWidth + (logo == null ? 0f : logoGap),
                11f, nameWidth + 2f, 34f), Left);

        var context = $"{GameLabel(snap)}  ·  {ProfileLabel(snap)}";
        DrawText(g, context, Ui(TypeScale.Caption), Theme.TextSecondary,
            new RectangleF(header.Right - 424f, header.Y + 10f, 400f, 18f), Right);
        DrawText(g, SyncLabel(now), Mono(TypeScale.Caption, FontStyle.Bold), Theme.TextTertiary,
            new RectangleF(header.Right - 424f, header.Y + 28f, 400f, 18f), Right);

        DrawHairline(g, header.X + 24f, header.Bottom - 1f, header.Right - 24f, 18);
    }

    private void DrawMinimalHeader(Graphics g, Snap snap, DateTime now, DashboardLayout layout)
    {
        var header = layout.Header;
        var (statusText, statusColor) = Status(snap);
        var statusFont = Rounded(TypeScale.Caption, FontStyle.Bold);

        using (var dot = new SolidBrush(statusColor))
        {
            g.FillEllipse(dot, header.X + 16f, header.Y + 22f, 7f, 7f);
        }

        DrawText(g, statusText, statusFont, WithAlpha(statusColor, 0.95f),
            new RectangleF(header.X + 30f, header.Y + 14f, 140f, 20f), Left);

        DrawText(g, TruckReference(snap), Rounded(18f, FontStyle.Bold), Theme.TextPrimary,
            new RectangleF(header.X + 180f, header.Y + 10f, header.Width - 360f, 30f), Center);

        var context = $"{GameLabel(snap)}  ·  {ProfileLabel(snap)}";
        DrawText(g, context, Ui(TypeScale.Caption), Theme.TextSecondary,
            new RectangleF(header.Right - 296f, header.Y + 8f, 280f, 18f), Right);
        DrawText(g, SyncLabel(now), Mono(TypeScale.Caption, FontStyle.Bold), Theme.TextTertiary,
            new RectangleF(header.Right - 296f, header.Y + 26f, 280f, 18f), Right);

        DrawHairline(g, header.X + 16f, header.Bottom - 1f, header.Right - 16f, 18);
    }

    private void DrawCompactFooter(Graphics g, Snap snap, DateTime now, DashboardLayout layout)
    {
        DrawHairline(g, layout.Footer.X + 24f, layout.Footer.Y,
            layout.Footer.Right - 24f, 12);

        var left = $"{AppInfo.Name} {AppInfo.Version}  ·  {ProfileLabel(snap)}";
        DrawText(g, left, Ui(TypeScale.Caption), Theme.TextTertiary,
            new RectangleF(layout.Footer.X + 24f, layout.Footer.Y + 4f, 700f, 18f), Left);

        var (statusText, statusColor) = Status(snap);
        var right = $"{statusText}  ·  {SyncLabel(now)}";
        DrawText(g, right, Mono(TypeScale.Caption, FontStyle.Bold), WithAlpha(statusColor, 0.80f),
            new RectangleF(layout.Footer.Right - 424f, layout.Footer.Y + 4f, 400f, 18f), Right);
    }

    // ---------------------------------------------------------------------
    // Testigos
    // ---------------------------------------------------------------------

    private void DrawCompactIndicatorRow(
        Graphics g,
        Snap snap,
        bool ignitionOn,
        bool bulbCheck,
        RectangleF area)
    {
        var checkEngine = ignitionOn && snap.Engine
                          && snap.EngineDmg is { } engineDamage && engineDamage >= 5;
        var indicators = new (string Asset, bool Active, Color Color, bool Pulses, bool Framed, float Width)[]
        {
            ("beacon-c", ignitionOn && snap.Beacon, Yellow, true, false, 24f),
            ("side-markers-c", ignitionOn && snap.Parking, Green, false, false, 24f),
            ("low-beam-c", ignitionOn && snap.LowB, Green, false, false, 24f),
            ("high-beam-c", ignitionOn && snap.HighB, Blue, false, false, 24f),
            ("brake-c1", ignitionOn && snap.Brake, Red, false, false, 24f),
            ("turn-lights-left-c", ignitionOn && snap.BlinkL, Green, false, false, 24f),
            ("check-engine-line", checkEngine, Yellow, false, true, 64f),
            ("turn-lights-right-c", ignitionOn && snap.BlinkR, Green, false, false, 24f),
            ("parking-brake-2-c", ignitionOn && snap.Park, Red, true, false, 24f),
            ("retarder-c", ignitionOn && snap.Retarder, Green, false, false, 24f),
            ("engine-brake-round", ignitionOn && snap.Motor, Green, false, false, 24f),
            ("wiper-c", ignitionOn && snap.Wipers > 0, Cyan, true, false, 24f),
            ("diff-lock-c", ignitionOn && snap.DiffLock, Orange, true, false, 24f)
        };

        const float iconSize = 24f;
        const float spacing = 10f;
        var total = -spacing;
        foreach (var indicator in indicators)
        {
            total += indicator.Width + spacing;
        }

        var startX = area.X + (area.Width - total) / 2f;
        var iconY = area.Y + (area.Height - iconSize) / 2f;

        DrawSquircle(g,
            new RectangleF(startX - 12f, area.Y, total + 24f, area.Height),
            14f,
            Ink(4),
            Ink(12),
            1f);

        var cursor = startX;
        foreach (var item in indicators)
        {
            var lit = item.Active || bulbCheck;

            if (ignitionOn && item.Framed)
            {
                DrawSquircle(g, new RectangleF(cursor, iconY, item.Width, iconSize), 7f,
                    lit ? WithAlpha(item.Color, 0.07f) : Color.Transparent,
                    lit ? WithAlpha(item.Color, 0.58f) : Ink(22),
                    1f);
                DrawTintedIcon(g, item.Asset,
                    new RectangleF(cursor + 5f, iconY + 3f, 18f, 18f),
                    lit ? item.Color : White(0.40f));
                DrawText(g, "CHECK", Rounded(TypeScale.Caption, FontStyle.Bold),
                    lit ? WithAlpha(item.Color, 0.82f) : Theme.TextTertiary,
                    new RectangleF(cursor + 27f, iconY + 2f, item.Width - 30f, 20f), Left);
            }
            else if (ignitionOn)
            {
                DrawIcon(g, item.Asset, new RectangleF(cursor, iconY, iconSize, iconSize),
                    lit, item.Color, item.Pulses && !bulbCheck);
            }

            cursor += item.Width + spacing;
        }
    }

    // ---------------------------------------------------------------------
    // Cluster central
    // ---------------------------------------------------------------------

    private void DrawCompactCluster(
        Graphics g,
        Snap snap,
        bool ignitionOn,
        bool bulbCheck,
        double ignitionElapsed,
        PointF center,
        float radius)
    {
        const double startAngle = 158.0;
        const double sweep = 224.0;
        var maxSpeed = SpeedMax;

        var cx = center.X;
        var cy = center.Y;
        var active = snap.Active;
        var speedKmh = active ? ToDisplaySpeed(Math.Abs(snap.Speed ?? 0f) * 3.6) : 0;
        var limitKmh = active && snap.Limit is { } limit && limit > 0.5f
            ? ToDisplaySpeed(limit * 3.6)
            : (double?)null;
        var cruiseSetKmh = ignitionOn && snap.CruiseOn && snap.Cruise is { } cruise && cruise > 0.5f
            ? ToDisplaySpeed(cruise * 3.6)
            : (double?)null;
        var overLimit = limitKmh is { } limitValue && speedKmh > limitValue + 1.0;
        var accent = !active ? Theme.TextDisabled : overLimit ? Orange : Cyan;
        var targetFraction = Math.Clamp(speedKmh / maxSpeed, 0, 1);
        var fraction = AnimatedFraction(SmoothTarget("compact-speed", targetFraction), ignitionElapsed);
        var displayedSpeedKmh = fraction * maxSpeed;

        using (var halo = new SolidBrush(Theme.Alpha(Theme.Accent, 0.03f)))
        {
            g.FillEllipse(halo, cx - radius - 40f, cy - radius - 40f,
                (radius + 40f) * 2f, (radius + 40f) * 2f);
        }

        using (var track = new Pen(Ink(16), 7f)
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
            using (var glow = new Pen(WithAlpha(accent, 0.12f), 18f)
                   {
                       StartCap = LineCap.Round,
                       EndCap = LineCap.Round
                   })
            {
                g.DrawArc(glow, cx - radius, cy - radius, radius * 2f, radius * 2f,
                    (float)startAngle, (float)(sweep * fraction));
            }

            DrawFh6SpeedArc(g, cx, cy, radius, startAngle, sweep, fraction, accent, Mint, 7f);
        }

        var tickCount = (int)Math.Round(maxSpeed / 20.0);
        for (var i = 0; i <= tickCount; i++)
        {
            var value = i * 20.0;
            var angle = startAngle + sweep * (value / maxSpeed);
            var major = i % 2 == 0;
            var outer = radius + (major ? 15f : 9f);
            var inner = radius + 4f;
            using var tick = new Pen(
                Ink(major ? 150 : 50),
                major ? 1.6f : 1f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };
            g.DrawLine(tick, PointAt(cx, cy, inner, angle), PointAt(cx, cy, outer, angle));

            if (major)
            {
                var p = PointAt(cx, cy, radius + 31f, angle);
                DrawText(g, Math.Round(value).ToString("0", Es),
                    Rounded(TypeScale.Caption, FontStyle.Bold), Theme.TextTertiary,
                    new RectangleF(p.X - 22f, p.Y - 10f, 44f, 20f), Center);
            }
        }

        if (fraction > 0.002)
        {
            var angle = startAngle + sweep * fraction;
            using var marker = new Pen(accent, 3.5f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };
            g.DrawLine(marker,
                PointAt(cx, cy, radius - 12f, angle),
                PointAt(cx, cy, radius + 12f, angle));
        }

        var speedFontSize = radius * 0.64f;
        var valueText = active && snap.Speed != null
            ? Math.Round(displayedSpeedKmh).ToString("0", Es)
            : "--";
        var speedColor = !active ? Theme.TextDisabled : overLimit ? Orange : Theme.TextPrimary;
        DrawFitted(g, valueText, "Bahnschrift Light", speedFontSize, FontStyle.Regular, speedColor,
            new RectangleF(cx - radius * 0.82f, cy - radius * 0.58f,
                radius * 1.64f, radius * 0.72f), Center, speedFontSize * 0.55f);

        DrawText(g, SpeedUnit, Rounded(TypeScale.Caption, FontStyle.Bold), Theme.TextTertiary,
            new RectangleF(cx - 60f, cy + radius * 0.16f, 120f, 20f), Center);

        DrawFh6CruiseChip(g, cx, cy - radius * 0.95f, cruiseSetKmh);

        var gearText = !active || snap.Gear is not { } gear
            ? "--"
            : gear > 0 ? gear.ToString(Es) : gear == 0 ? "N" : "R";
        var gearColor = !active || snap.Gear is not { } gearValue
            ? Theme.TextDisabled
            : gearValue < 0 ? Red : gearValue == 0 ? Cyan : Theme.TextPrimary;
        var gearWidth = radius * 0.34f;
        var gearHeight = radius * 0.28f;
        var gearTop = cy + radius * 0.26f;

        DrawSquircle(g,
            new RectangleF(cx - gearWidth / 2f, gearTop, gearWidth, gearHeight),
            12f,
            Ink(5),
            Ink(18),
            1f);
        DrawTracked(g, "MARCHA", Rounded(TypeScale.Caption, FontStyle.Bold), Theme.TextTertiary,
            cx, gearTop + gearHeight * 0.22f, 1f, Center);
        DrawText(g, gearText, Rounded(gearHeight * 0.62f, FontStyle.Bold), gearColor,
            new RectangleF(cx - gearWidth / 2f, gearTop + gearHeight * 0.36f,
                gearWidth, gearHeight * 0.60f), Center);

        if (ignitionOn)
        {
            var gaugeMax = snap.RpmMax is { } rpmMax && rpmMax >= 800 ? (double)rpmMax : 2500.0;
            var hasRpm = snap.Rpm != null;
            var targetRpm = Math.Clamp(snap.Rpm ?? 0, 0, (float)gaugeMax);
            var targetRpmFraction = gaugeMax > 0 ? targetRpm / gaugeMax : 0;
            var rpmFraction = hasRpm
                ? AnimatedFraction(SmoothTarget("compact-rpm", targetRpmFraction), ignitionElapsed)
                : 0;
            var rpm = rpmFraction * gaugeMax;
            var rpmColor = rpmFraction >= 0.92 ? Red
                : rpmFraction >= 0.84 ? Orange
                : rpmFraction >= 0.76 ? Yellow
                : Green;

            var barWidth = radius * 1.45f;
            var barX = cx - barWidth / 2f;
            var barY = cy + radius * 0.70f;

            DrawTracked(g, "RPM", Rounded(TypeScale.Caption, FontStyle.Bold), Theme.TextTertiary,
                barX, barY - 10f, 1.2f);
            DrawText(g, hasRpm ? Math.Round(rpm).ToString("N0", Es) : "--",
                Rounded(13f, FontStyle.Bold), rpmColor,
                new RectangleF(barX + 34f, barY - 20f, 120f, 20f), Left);
            DrawFh6RpmBar(g, barX, barY, barWidth, 8f, rpmFraction, rpmColor);
        }
    }

    // ---------------------------------------------------------------------
    // Tarjetas laterales
    // ---------------------------------------------------------------------

    private void DrawCompactFuelCard(
        Graphics g,
        Snap snap,
        double ignitionElapsed,
        RectangleF area)
    {
        DrawGlassPanel(g, area.X, area.Y, area.Width, area.Height, Theme.RadiusCard);

        var fuel = snap.Active && snap.Fuel is { } fuelValue ? (double?)fuelValue : null;
        var capacity = snap.Active && snap.FuelCap is { } cap && cap > 0 ? (double?)cap : null;
        var targetFraction = fuel.HasValue && capacity.HasValue && capacity > 0
            ? Math.Clamp(fuel.Value / capacity.Value, 0, 1)
            : 0;
        var fraction = AnimatedFraction(SmoothTarget("compact-fuel", targetFraction), ignitionElapsed);
        var lowFuel = snap.FuelWarn || (snap.Active && targetFraction < 0.12);
        var color = !snap.Active || fuel == null || capacity == null
            ? Theme.TextDisabled
            : lowFuel ? Red
            : targetFraction < 0.30 ? Orange
            : Green;

        DrawFuelIcon(g, area.X + 16f, area.Y + 14f, 20f, color);
        DrawTracked(g, "COMBUSTIBLE", Rounded(TypeScale.Caption, FontStyle.Bold), Theme.TextSecondary,
            area.X + 44f, area.Y + 24f, 1.2f);
        DrawText(g, !snap.Active ? "SIN DATOS" : lowFuel ? "RESERVA" : "NIVEL",
            Mono(TypeScale.Caption, FontStyle.Bold),
            !snap.Active ? Theme.TextDisabled : lowFuel ? Red : Theme.TextTertiary,
            new RectangleF(area.Right - 112f, area.Y + 16f, 96f, 18f), Right);

        DrawFitted(g, fuel == null || capacity == null
                ? "--"
                : $"{Math.Round(fraction * 100).ToString("0", Es)}%",
            "Bahnschrift", 32f, FontStyle.Bold, color,
            new RectangleF(area.X + 16f, area.Y + 34f, 104f, 40f), Left, 20f);

        var gallons = capacity is { } animatedCapacity && fuel != null
            ? Math.Round(fraction * animatedCapacity * 0.2641720524).ToString("N0", Es)
            : "--";
        var capacityGallons = capacity is { } capacityValue
            ? Math.Round(capacityValue * 0.2641720524).ToString("N0", Es)
            : "--";
        DrawFitted(g, $"{gallons}/{capacityGallons} GAL", "Consolas",
            TypeScale.Caption, FontStyle.Bold, Theme.TextSecondary,
            new RectangleF(area.X + 126f, area.Y + 40f, area.Width - 142f, 18f), Left, 10f);
        DrawFitted(g, snap.Active && snap.Range is { } range
                ? $"AUT. {Math.Round(range).ToString("N0", Es)} KM"
                : "AUT. -- KM",
            "Consolas", TypeScale.Caption, FontStyle.Bold, Theme.TextTertiary,
            new RectangleF(area.X + 126f, area.Y + 58f, area.Width - 142f, 18f), Left, 10f);

        DrawProgressBar(g, area.X + 16f, area.Bottom - 20f, area.Width - 32f, 6f, fraction, color);
    }

    private void DrawCompactWeatherCard(Graphics g, Snap snap, RectangleF area)
    {
        DrawGlassPanel(g, area.X, area.Y, area.Width, area.Height, Theme.RadiusCard);

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

        DrawCompactWeatherIcon(g, condition, isDay, area.X + 16f, area.Y + 22f, 44f, accent, snap.Active);

        DrawText(g, !snap.Active ? "SIN DATOS" : isDay ? "DÍA" : "NOCHE",
            Mono(TypeScale.Caption, FontStyle.Bold),
            !snap.Active ? Theme.TextDisabled : Theme.TextTertiary,
            new RectangleF(area.Right - 96f, area.Y + 14f, 80f, 18f), Right);

        DrawFitted(g, conditionText, "Bahnschrift", 17f, FontStyle.Bold,
            snap.Active ? accent : Theme.TextDisabled,
            new RectangleF(area.X + 72f, area.Y + 16f, area.Width - 184f, 24f), Left, TypeScale.Caption);

        var timeText = minutes == null
            ? "--:--"
            : $"{((minutes.Value / 60) % 24):00}:{minutes.Value % 60:00}";
        DrawText(g, timeText, Mono(24f, FontStyle.Bold),
            snap.Active ? Theme.TextPrimary : Theme.TextDisabled,
            new RectangleF(area.X + 72f, area.Y + 42f, area.Width - 88f, 32f), Left);
    }

    private void DrawCompactFinancePanel(Graphics g, Snap snap, RectangleF area)
    {
        DrawGlassPanel(g, area.X, area.Y, area.Width, area.Height, Theme.RadiusCard);

        const float inset = 18f;
        var contentX = area.X + inset;
        var contentW = area.Width - inset * 2f;

        const float logoSize = 32f;
        var companyLogo = CompanyLogoImage(snap.CompanyLogo);
        var companyTextW = companyLogo == null ? contentW : contentW - logoSize - 10f;

        DrawTracked(g, "EMPRESA", Rounded(TypeScale.Caption, FontStyle.Bold), Theme.TextTertiary,
            contentX, area.Y + 18f, 1.2f);

        var company = string.IsNullOrWhiteSpace(snap.CompanyName) ? "SIN EMPRESA" : snap.CompanyName;
        DrawFitted(g, company, "Bahnschrift", 19f, FontStyle.Bold,
            string.IsNullOrWhiteSpace(snap.CompanyName) ? Theme.TextDisabled : Theme.TextPrimary,
            new RectangleF(contentX, area.Y + 28f, companyTextW, 28f), Left, TypeScale.Caption);

        if (companyLogo != null)
        {
            DrawFh6CompanyLogo(g, companyLogo,
                new RectangleF(area.Right - inset - logoSize, area.Y + 12f, logoSize, logoSize));
        }

        DrawHairline(g, contentX, area.Y + 58f, contentX + contentW, 16);

        var income = Income(snap);
        var net = Net(snap);
        var margin = income is { } incomeValue && incomeValue > 0 && net is { } netValue
            ? Math.Clamp((double)netValue / incomeValue, 0, 1)
            : (double?)null;

        DrawFh6LedgerRow(g, contentX, area.Y + 76f, contentW,
            "TRABAJO ACTUAL", Money(income), Green, 20f);
        DrawFh6LedgerRow(g, contentX, area.Y + 114f, contentW,
            "GANANCIA NETA", Money(net), Cyan, 20f);
        DrawFh6LedgerRow(g, contentX, area.Y + 152f, contentW,
            "SALDO DEL BANCO", Money(snap.BankBalance), Cyan, 21f);

        DrawMetricRow(g, contentX, area.Bottom - 22f, contentW, "MARGEN",
            margin == null ? "--" : $"{Math.Round(margin.Value * 100).ToString("0", Es)}%",
            margin == null ? Theme.TextDisabled : Theme.TextSecondary);
        DrawProgressBar(g, contentX, area.Bottom - 8f, contentW, 4f, margin ?? 0, Cyan);
    }

    private void DrawCompactCargoPanel(Graphics g, Snap snap, RectangleF area)
    {
        DrawGlassPanel(g, area.X, area.Y, area.Width, area.Height, Theme.RadiusCard);

        const float inset = 18f;
        var contentX = area.X + inset;
        var contentW = area.Width - inset * 2f;
        var loaded = snap.Active && snap.CargoLoaded;
        var cargoName = loaded && !string.IsNullOrWhiteSpace(snap.CargoName) ? snap.CargoName : null;
        var mass = loaded && snap.CargoMass is { } cargoMass && cargoMass > 0 ? (double?)cargoMass : null;
        var damage = loaded && snap.CargoDmg is { } cargoDamage ? (double?)cargoDamage : null;
        var accent = !snap.Active || !loaded ? Theme.TextDisabled : DamageColor(damage ?? 0);
        var damageColor = DamageColor(damage);

        DrawBoxIcon(g, contentX, area.Y + 12f, 20f, accent);
        DrawTracked(g, "CARGA", Rounded(TypeScale.Caption, FontStyle.Bold), Theme.TextSecondary,
            contentX + 28f, area.Y + 22f, 1.2f);
        DrawText(g, !snap.Active ? "SIN DATOS" : loaded ? "CARGADA" : "VACÍA",
            Mono(TypeScale.Caption, FontStyle.Bold), WithAlpha(accent, 0.86f),
            new RectangleF(area.Right - 118f, area.Y + 14f, 100f, 18f), Right);

        DrawFitted(g, cargoName ?? "SIN CARGA", "Bahnschrift", 21f, FontStyle.Bold,
            Theme.TextPrimary,
            new RectangleF(contentX, area.Y + 36f, contentW, 30f), LeftTop, 13f);

        DrawTracked(g, "PESO", Rounded(TypeScale.Caption, FontStyle.Bold), Theme.TextTertiary,
            contentX, area.Y + 80f, 1.2f);
        var massText = mass == null ? "-- kg" : $"{Math.Round(mass.Value).ToString("N0", Es)} kg";
        DrawFitted(g, massText, "Bahnschrift", 23f, FontStyle.Bold, Theme.TextPrimary,
            new RectangleF(contentX, area.Y + 88f, contentW - 64f, 30f), Left, 14f);
        if (mass != null)
        {
            DrawText(g, $"{(mass.Value / 1000).ToString("N1", Es)} t",
                Mono(TypeScale.Caption, FontStyle.Bold), Theme.TextTertiary,
                new RectangleF(area.Right - inset - 60f, area.Y + 96f, 60f, 18f), Right);
        }

        DrawTracked(g, "INTEGRIDAD", Rounded(TypeScale.Caption, FontStyle.Bold), Theme.TextTertiary,
            contentX, area.Y + 128f, 1.2f);
        DrawText(g, damage == null ? "--" : $"{Math.Round(100 - damage.Value).ToString("0", Es)}%",
            Mono(14f, FontStyle.Bold), damageColor,
            new RectangleF(area.Right - inset - 80f, area.Y + 122f, 80f, 20f), Right);
        DrawProgressBar(g, contentX, area.Y + 140f, contentW, 6f,
            damage == null ? 0 : Math.Clamp(1 - damage.Value / 100, 0, 1), damageColor);

        DrawHairline(g, contentX, area.Y + 160f, contentX + contentW, 16);

        var route = snap.Active && snap.OnJob && !string.IsNullOrWhiteSpace(snap.CitySrc)
                    && !string.IsNullOrWhiteSpace(snap.CityDst)
            ? $"{snap.CitySrc}  →  {snap.CityDst}"
            : "--";
        DrawTracked(g, "RUTA", Rounded(TypeScale.Caption, FontStyle.Bold), Theme.TextTertiary,
            contentX, area.Y + 176f, 1.2f);
        DrawFitted(g, route, "Segoe UI", TypeScale.Label, FontStyle.Regular, Theme.TextSecondary,
            new RectangleF(contentX, area.Y + 186f, contentW, 20f), Left, TypeScale.Caption);

        var eta = snap.Active && snap.OnJob ? FormatEta(snap.RouteT) : null;
        DrawTracked(g, "ENTREGA", Rounded(TypeScale.Caption, FontStyle.Bold), Theme.TextTertiary,
            contentX, area.Y + 220f, 1.2f);
        DrawText(g, eta ?? "--", Rounded(TypeScale.Body, FontStyle.Bold), Theme.TextSecondary,
            new RectangleF(contentX, area.Y + 230f, contentW, 22f), Left);
    }

    private void DrawCompactDamagePanel(Graphics g, Snap snap, RectangleF area)
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

        DrawGlassPanel(g, area.X, area.Y, area.Width, area.Height, Theme.RadiusCard);

        const float inset = 18f;
        DrawTintedIcon(g, "advisor-diagnostics",
            new RectangleF(area.X + inset, area.Y + 12f, 20f, 20f), headerColor, 1f);
        DrawTracked(g, "DAÑOS", Rounded(TypeScale.Caption, FontStyle.Bold), Theme.TextSecondary,
            area.X + inset + 28f, area.Y + 22f, 1.2f);
        DrawText(g, status, Mono(TypeScale.Caption, FontStyle.Bold), WithAlpha(headerColor, 0.86f),
            new RectangleF(area.Right - 118f, area.Y + 14f, 100f, 18f), Right);

        DrawDamageRow(g, area.X + inset, area.Y + 58f, area.Width - inset * 2f, "CAMIÓN", truck);
        DrawDamageRow(g, area.X + inset, area.Y + 104f, area.Width - inset * 2f, "TRÁILER", trailer);
        DrawDamageRow(g, area.X + inset, area.Y + 150f, area.Width - inset * 2f, "CARGA", cargo);
    }

    private void DrawCompactGpsPanel(Graphics g, Snap snap, RectangleF area)
    {
        var active = snap.Active;
        var heading = active && snap.Heading is { } headingValue
            ? (double?)((headingValue % 1.0 + 1.0) % 1.0)
            : null;
        var bearing = heading == null
            ? (double?)null
            : ((1.0 - heading.Value) * 360.0 % 360.0 + 360.0) % 360.0;
        var accent = active ? Cyan : Theme.TextDisabled;
        var destination = active && snap.OnJob && !string.IsNullOrWhiteSpace(snap.CityDst)
            ? snap.CityDst!
            : "SIN DESTINO";
        var trip = BuildTrip(snap);

        DrawGlassPanel(g, area.X, area.Y, area.Width, area.Height, Theme.RadiusCard);

        const float inset = 18f;
        DrawLocationIcon(g, area.X + inset, area.Y + 12f, 20f, accent);
        DrawTracked(g, "GPS", Rounded(TypeScale.Caption, FontStyle.Bold), Theme.TextSecondary,
            area.X + inset + 28f, area.Y + 22f, 1.2f);
        DrawText(g, !active ? "SIN DATOS" : snap.OnJob ? "EN RUTA" : "SIN RUTA",
            Mono(TypeScale.Caption, FontStyle.Bold),
            !active ? Theme.TextDisabled : snap.OnJob ? WithAlpha(Green, 0.82f) : Theme.TextDisabled,
            new RectangleF(area.Right - 118f, area.Y + 14f, 100f, 18f), Right);

        DrawCompass(g, area.X + 14f, area.Y + 40f, 92f, bearing, accent);

        var columnX = area.X + 116f;
        var columnW = area.Width - 134f;

        DrawTracked(g, "RUMBO", Rounded(TypeScale.Caption, FontStyle.Bold), Theme.TextTertiary,
            columnX + columnW / 2f, area.Y + 50f, 1.2f, Center);
        DrawText(g, Cardinal(bearing), Rounded(26f, FontStyle.Bold), accent,
            new RectangleF(columnX, area.Y + 54f, columnW, 30f), Center);
        DrawText(g, bearing == null ? "--" : $"{Math.Round(bearing.Value).ToString("0", Es)}°",
            Mono(TypeScale.Caption, FontStyle.Bold), Theme.TextSecondary,
            new RectangleF(columnX, area.Y + 84f, columnW, 16f), Center);

        DrawTracked(g, "DESTINO", Rounded(TypeScale.Caption, FontStyle.Bold), Theme.TextTertiary,
            columnX + columnW / 2f, area.Y + 108f, 1.2f, Center);
        DrawFitted(g, destination, "Bahnschrift", 16f, FontStyle.Bold, Theme.TextPrimary,
            new RectangleF(columnX, area.Y + 114f, columnW, 22f), Center, TypeScale.Caption);

        DrawHairline(g, area.X + inset, area.Y + 142f, area.Right - inset, 14);

        DrawTracked(g, "FALTAN", Rounded(TypeScale.Caption, FontStyle.Bold), Theme.TextTertiary,
            area.X + inset, area.Y + 156f, 1.2f);
        var remaining = trip?.Remaining ?? "--";
        var remainingFont = Rounded(32f, FontStyle.Bold);
        DrawText(g, remaining, remainingFont, trip == null ? Theme.TextDisabled : Green,
            new RectangleF(area.X + inset, area.Y + 162f, 130f, 40f), Left);
        DrawText(g, "km", Rounded(TypeScale.Caption, FontStyle.Bold), Theme.TextTertiary,
            new RectangleF(area.X + inset + 4f + g.MeasureString(remaining, remainingFont).Width,
                area.Y + 174f, 40f, 18f), Left);

        DrawTracked(g, "LLEGADA", Rounded(TypeScale.Caption, FontStyle.Bold), Theme.TextTertiary,
            area.Right - inset, area.Y + 158f, 1.2f, Right);
        DrawText(g, trip?.Eta ?? "--", Rounded(16f, FontStyle.Bold), Theme.TextSecondary,
            new RectangleF(area.Right - inset - 150f, area.Y + 168f, 150f, 22f), Right);

        var progress = RouteProgress(snap);
        DrawTracked(g, "PROGRESO", Rounded(TypeScale.Caption, FontStyle.Bold), Theme.TextTertiary,
            area.X + inset, area.Y + 210f, 1.2f);
        DrawText(g, progress > 0
                ? $"{Math.Round(progress * 100).ToString("0", Es)}%"
                : "--",
            Mono(TypeScale.Caption, FontStyle.Bold), Theme.TextSecondary,
            new RectangleF(area.Right - inset - 90f, area.Y + 201f, 90f, 18f), Right);
        DrawProgressBar(g, area.X + inset, area.Y + 224f, area.Width - inset * 2f, 6f, progress, accent);

        var route = active && snap.OnJob && !string.IsNullOrWhiteSpace(snap.CitySrc)
                    && !string.IsNullOrWhiteSpace(snap.CityDst)
            ? $"{snap.CitySrc}  →  {snap.CityDst}"
            : "--";
        DrawFitted(g, route, "Segoe UI", TypeScale.Caption, FontStyle.Regular, Theme.TextTertiary,
            new RectangleF(area.X + inset, area.Y + 244f, area.Width - inset * 2f, 18f),
            Left, TypeScale.Caption);
    }

    private void DrawCompactNotices(Graphics g, Snap snap, RectangleF area)
    {
        DrawGlassPanel(g, area.X, area.Y, area.Width, area.Height, Theme.RadiusCard);

        DrawTintedIcon(g, "advisor-diagnostics",
            new RectangleF(area.X + 16f, area.Y + 12f, 18f, 18f), Orange, 0.92f);
        DrawTracked(g, "AVISOS", Rounded(TypeScale.Caption, FontStyle.Bold), Theme.TextSecondary,
            area.X + 42f, area.Y + 22f, 1.2f);

        var status = snap.Demo ? "DEMO" : snap.Active ? "EN VIVO" : "SIN DATOS";
        DrawText(g, status, Mono(TypeScale.Caption, FontStyle.Bold),
            snap.Active ? Theme.TextTertiary : Theme.TextDisabled,
            new RectangleF(area.Right - 112f, area.Y + 14f, 96f, 18f), Right);

        if (_currentMessage is { } message && _messageOpacity > 0.01f)
        {
            var color = message.Kind switch
            {
                DashboardRenderer.NoticeKind.Income => Green,
                DashboardRenderer.NoticeKind.Expense => Red,
                DashboardRenderer.NoticeKind.Route => Cyan,
                _ => Orange
            };
            DrawFitted(g, message.Text, "Bahnschrift", 19f, FontStyle.Bold,
                WithAlpha(color, 0.96f * _messageOpacity),
                new RectangleF(area.X + 16f, area.Y + 38f, area.Width - 32f, 34f), Center, 12f);
        }
        else
        {
            DrawFitted(g, "SIN AVISOS", "Bahnschrift", 16f, FontStyle.Bold, Theme.TextDisabled,
                new RectangleF(area.X + 16f, area.Y + 38f, area.Width - 32f, 34f), Center, 12f);
        }
    }

    // ---------------------------------------------------------------------
    // Minimal
    // ---------------------------------------------------------------------

    private void DrawMinimalFuelCard(
        Graphics g,
        Snap snap,
        double ignitionElapsed,
        RectangleF area)
    {
        DrawGlassPanel(g, area.X, area.Y, area.Width, area.Height, Theme.RadiusCard);

        var fuel = snap.Active && snap.Fuel is { } fuelValue ? (double?)fuelValue : null;
        var capacity = snap.Active && snap.FuelCap is { } cap && cap > 0 ? (double?)cap : null;
        var targetFraction = fuel.HasValue && capacity.HasValue && capacity > 0
            ? Math.Clamp(fuel.Value / capacity.Value, 0, 1)
            : 0;
        var fraction = AnimatedFraction(SmoothTarget("minimal-fuel", targetFraction), ignitionElapsed);
        var lowFuel = snap.FuelWarn || (snap.Active && targetFraction < 0.12);
        var color = !snap.Active || fuel == null || capacity == null
            ? Theme.TextDisabled
            : lowFuel ? Red
            : targetFraction < 0.30 ? Orange
            : Green;

        DrawFuelIcon(g, area.X + 14f, area.Y + 12f, 18f, color);
        DrawTracked(g, "COMBUSTIBLE", Rounded(TypeScale.Caption, FontStyle.Bold), Theme.TextSecondary,
            area.X + 38f, area.Y + 21f, 1.1f);

        DrawFitted(g, fuel == null || capacity == null
                ? "--"
                : $"{Math.Round(fraction * 100).ToString("0", Es)}%",
            "Bahnschrift", 28f, FontStyle.Bold, color,
            new RectangleF(area.X + 14f, area.Y + 30f, 96f, 34f), Left, 18f);

        DrawText(g, snap.Active && snap.Range is { } range
                ? $"AUT. {Math.Round(range).ToString("N0", Es)} KM"
                : "AUT. -- KM",
            Mono(TypeScale.Caption, FontStyle.Bold), Theme.TextTertiary,
            new RectangleF(area.X + 14f, area.Y + 64f, area.Width - 28f, 18f), Left);

        DrawProgressBar(g, area.X + 14f, area.Bottom - 14f, area.Width - 28f, 5f, fraction, color);
    }

    private void DrawMinimalTripCard(Graphics g, Snap snap, RectangleF area)
    {
        DrawGlassPanel(g, area.X, area.Y, area.Width, area.Height, Theme.RadiusCard);

        var trip = BuildTrip(snap);
        var accent = snap.Active && snap.OnJob ? Green : Theme.TextDisabled;

        DrawRouteIcon(g, area.X + 14f, area.Y + 12f, 18f, accent);
        DrawTracked(g, "RUTA", Rounded(TypeScale.Caption, FontStyle.Bold), Theme.TextSecondary,
            area.X + 38f, area.Y + 21f, 1.1f);

        DrawTracked(g, "FALTAN", Rounded(TypeScale.Caption, FontStyle.Bold), Theme.TextTertiary,
            area.X + 14f, area.Y + 44f, 1.1f);
        DrawFitted(g, trip?.Remaining ?? "--", "Bahnschrift", 26f, FontStyle.Bold,
            trip == null ? Theme.TextDisabled : Green,
            new RectangleF(area.X + 14f, area.Y + 50f, 96f, 32f), Left, 16f);
        DrawText(g, "km", Rounded(TypeScale.Caption, FontStyle.Bold), Theme.TextTertiary,
            new RectangleF(area.X + 92f, area.Y + 60f, 30f, 18f), Left);

        DrawTracked(g, "LLEGADA", Rounded(TypeScale.Caption, FontStyle.Bold), Theme.TextTertiary,
            area.X + 14f, area.Y + 86f, 1.1f);
        DrawText(g, trip?.Eta ?? "--", Rounded(TypeScale.Body, FontStyle.Bold), Theme.TextSecondary,
            new RectangleF(area.X + 80f, area.Y + 78f, area.Width - 94f, 20f), Right);

        var progress = RouteProgress(snap);
        DrawProgressBar(g, area.X + 14f, area.Bottom - 14f, area.Width - 28f, 5f, progress, accent);
    }

    private void DrawMinimalNotices(Graphics g, Snap snap, RectangleF area)
    {
        if (_currentMessage is { } message && _messageOpacity > 0.01f)
        {
            var color = message.Kind switch
            {
                DashboardRenderer.NoticeKind.Income => Green,
                DashboardRenderer.NoticeKind.Expense => Red,
                DashboardRenderer.NoticeKind.Route => Cyan,
                _ => Orange
            };
            DrawText(g, message.Text, Rounded(TypeScale.Caption, FontStyle.Bold),
                WithAlpha(color, 0.92f * _messageOpacity),
                new RectangleF(area.X, area.Y, area.Width, area.Height), Center);
            return;
        }

        DrawText(g, "SIN AVISOS", Rounded(TypeScale.Caption, FontStyle.Bold), Theme.TextDisabled,
            new RectangleF(area.X, area.Y, area.Width, area.Height), Center);
    }
}
