using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using SkiaSharp;

namespace ATSTelemetry.SkiaCompat;

public enum TextFormat
{
    Center,
    Left,
    Right,
    LeftTop,
    RightTop,
    Typographic
}

public static class SkiaFontResolver
{
    public static Func<string, string>? FamilyResolver { get; set; }
}

public sealed class SkiaFont : IDisposable
{
    public SkiaFont(string family, float size, FontStyle style, GraphicsUnit unit = GraphicsUnit.Pixel)
    {
        Family = family;
        Size = size;
        Bold = (style & FontStyle.Bold) != 0;
    }

    public string Family { get; }
    public float Size { get; }
    public bool Bold { get; }

    public string Key => $"{Family}|{Size:0.###}|{Bold}";

    public float GetHeight(SkiaGraphics graphics) => graphics.GetLineHeight(this);

    public void Dispose()
    {
    }
}

public abstract class SkiaBrush : IDisposable
{
    public abstract Color Color { get; }
    internal abstract SKPaint CreatePaint(SKRect? bounds);

    public void Dispose()
    {
    }
}

public sealed class SkiaSolidBrush : SkiaBrush
{
    private readonly Color _color;

    public SkiaSolidBrush(Color color)
    {
        _color = color;
    }

    public override Color Color => _color;

    internal override SKPaint CreatePaint(SKRect? bounds)
    {
        return new SKPaint
        {
            Color = SkiaGraphics.ToSK(_color),
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };
    }
}

public sealed class SkiaLinearGradientBrush : SkiaBrush
{
    private readonly RectangleF _bounds;
    private readonly Color _start;
    private readonly Color _end;
    private readonly LinearGradientMode _mode;
    private ColorBlend? _blend;

    public SkiaLinearGradientBrush(RectangleF bounds, Color start, Color end, LinearGradientMode mode)
    {
        _bounds = bounds;
        _start = start;
        _end = end;
        _mode = mode;
    }

    public ColorBlend InterpolationColors
    {
        set => _blend = value;
    }

    public override Color Color => _start;

    internal override SKPaint CreatePaint(SKRect? bounds)
    {
        var colors = _blend?.Colors ?? new[] { _start, _end };
        var positions = _blend?.Positions ?? new[] { 0f, 1f };

        var (from, to) = _mode switch
        {
            LinearGradientMode.Horizontal => (
                new SKPoint(_bounds.Left, _bounds.Top),
                new SKPoint(_bounds.Right, _bounds.Top)),
            LinearGradientMode.BackwardDiagonal => (
                new SKPoint(_bounds.Right, _bounds.Bottom),
                new SKPoint(_bounds.Left, _bounds.Top)),
            LinearGradientMode.ForwardDiagonal => (
                new SKPoint(_bounds.Left, _bounds.Top),
                new SKPoint(_bounds.Right, _bounds.Bottom)),
            _ => (
                new SKPoint(_bounds.Left, _bounds.Top),
                new SKPoint(_bounds.Left, _bounds.Bottom))
        };

        var shader = SKShader.CreateLinearGradient(
            from,
            to,
            Array.ConvertAll(colors, SkiaGraphics.ToSK),
            positions,
            SKShaderTileMode.Clamp);

        return new SKPaint
        {
            Shader = shader,
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };
    }
}

public sealed class SkiaPathGradientBrush : SkiaBrush
{
    private readonly SkiaGraphicsPath _path;

    public SkiaPathGradientBrush(SkiaGraphicsPath path)
    {
        _path = path;
    }

    public Color CenterColor { get; set; } = Color.White;
    public Color[] SurroundColors { get; set; } = new[] { Color.Black };

    public override Color Color => CenterColor;

    internal override SKPaint CreatePaint(SKRect? bounds)
    {
        var rect = _path.Bounds;
        var center = new SKPoint(rect.Left + rect.Width / 2f, rect.Top + rect.Height / 2f);
        var radius = Math.Max(rect.Width, rect.Height) / 2f;
        if (radius <= 0.01f)
        {
            radius = 1f;
        }

        var colors = new[] { SkiaGraphics.ToSK(CenterColor), SkiaGraphics.ToSK(SurroundColors[0]) };
        var shader = SKShader.CreateRadialGradient(
            center,
            radius,
            colors,
            new[] { 0f, 1f },
            SKShaderTileMode.Clamp);

        return new SKPaint
        {
            Shader = shader,
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };
    }
}

public sealed class SkiaPen : IDisposable
{
    public SkiaPen(Color color, float width)
    {
        Color = color;
        Width = width;
    }

    public Color Color { get; set; }
    public float Width { get; set; }
    public LineCap StartCap { get; set; } = LineCap.Flat;
    public LineCap EndCap { get; set; } = LineCap.Flat;

    internal SKPaint CreatePaint()
    {
        return new SKPaint
        {
            Color = SkiaGraphics.ToSK(Color),
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = Width,
            StrokeCap = ToSK(StartCap),
            StrokeJoin = SKStrokeJoin.Round
        };
    }

    internal static SKStrokeCap ToSK(LineCap cap)
    {
        return cap switch
        {
            LineCap.Round => SKStrokeCap.Round,
            LineCap.Square => SKStrokeCap.Square,
            _ => SKStrokeCap.Butt
        };
    }

    public void Dispose()
    {
    }
}

public sealed class SkiaGraphicsPath : IDisposable
{
    internal SKPath Path { get; } = new();

    public RectangleF Bounds
    {
        get
        {
            var bounds = Path.Bounds;
            return new RectangleF(bounds.Left, bounds.Top, bounds.Width, bounds.Height);
        }
    }

    public void AddRectangle(RectangleF rect)
    {
        Path.AddRect(SkiaGraphics.ToSK(rect));
    }

    public void MoveTo(float x, float y)
    {
        Path.MoveTo(x, y);
    }

    public void LineTo(float x, float y)
    {
        Path.LineTo(x, y);
    }

    public void AddArc(float x, float y, float width, float height, float startAngle, float sweepAngle)
    {
        Path.AddArc(new SKRect(x, y, x + width, y + height), startAngle, sweepAngle);
    }

    public void AddEllipse(float x, float y, float width, float height)
    {
        Path.AddOval(new SKRect(x, y, x + width, y + height));
    }

    public void CloseFigure()
    {
        Path.Close();
    }

    public void Dispose()
    {
        Path.Dispose();
    }
}

public sealed class SkiaColorMatrix
{
    public bool IsTint { get; init; }
    public Color Tint { get; init; }
    public float Alpha { get; init; } = 1f;
}

public sealed class SkiaImageAttributes : IDisposable
{
    internal SkiaColorMatrix? Matrix { get; private set; }

    public void SetColorMatrix(SkiaColorMatrix matrix)
    {
        Matrix = matrix;
    }

    public void Dispose()
    {
    }
}

public sealed class SkiaImage : IDisposable
{
    internal SkiaImage(SKBitmap bitmap)
    {
        Bitmap = bitmap;
    }

    internal SKBitmap Bitmap { get; }
    public int Width => Bitmap.Width;
    public int Height => Bitmap.Height;

    public static SkiaImage? FromFile(string path)
    {
        var bitmap = SKBitmap.Decode(path);
        return bitmap == null ? null : new SkiaImage(bitmap);
    }

    public void Dispose()
    {
        Bitmap.Dispose();
    }
}

public sealed class SkiaGraphics
{
    private static readonly Dictionary<string, SKTypeface> Typefaces = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, SKPaint> TextPaints = new(StringComparer.Ordinal);
    private static readonly object Sync = new();

    private readonly SKCanvas _canvas;

    public SkiaGraphics(SKCanvas canvas)
    {
        _canvas = canvas;
    }

    public SmoothingMode SmoothingMode { get; set; }
    public PixelOffsetMode PixelOffsetMode { get; set; }
    public CompositingQuality CompositingQuality { get; set; }
    public TextRenderingHint TextRenderingHint { get; set; }

    public int Save() => _canvas.Save();

    public void Restore(int state) => _canvas.RestoreToCount(state);

    public void TranslateTransform(float dx, float dy) => _canvas.Translate(dx, dy);

    public void ScaleTransform(float sx, float sy) => _canvas.Scale(sx, sy);

    public void Clear(Color color) => _canvas.Clear(ToSK(color));

    public void SetClip(SkiaGraphicsPath path)
    {
        _canvas.ClipPath(path.Path, SKClipOperation.Intersect, true);
    }

    public void DrawLine(SkiaPen pen, float x1, float y1, float x2, float y2)
    {
        using var paint = pen.CreatePaint();
        _canvas.DrawLine(x1, y1, x2, y2, paint);
    }

    public void DrawLine(SkiaPen pen, PointF p1, PointF p2)
    {
        DrawLine(pen, p1.X, p1.Y, p2.X, p2.Y);
    }

    public void DrawArc(SkiaPen pen, float x, float y, float width, float height, float startAngle, float sweepAngle)
    {
        using var paint = pen.CreatePaint();
        _canvas.DrawArc(new SKRect(x, y, x + width, y + height), startAngle, sweepAngle, false, paint);
    }

    public void DrawEllipse(SkiaPen pen, float x, float y, float width, float height)
    {
        using var paint = pen.CreatePaint();
        _canvas.DrawOval(new SKRect(x, y, x + width, y + height), paint);
    }

    public void DrawEllipse(SkiaPen pen, RectangleF rect)
    {
        DrawEllipse(pen, rect.X, rect.Y, rect.Width, rect.Height);
    }

    public void FillEllipse(SkiaBrush brush, float x, float y, float width, float height)
    {
        using var paint = brush.CreatePaint(new SKRect(x, y, x + width, y + height));
        using var shader = paint.Shader;
        _canvas.DrawOval(new SKRect(x, y, x + width, y + height), paint);
    }

    public void FillEllipse(SkiaBrush brush, RectangleF rect)
    {
        FillEllipse(brush, rect.X, rect.Y, rect.Width, rect.Height);
    }

    public void DrawRectangle(SkiaPen pen, float x, float y, float width, float height)
    {
        using var paint = pen.CreatePaint();
        _canvas.DrawRect(new SKRect(x, y, x + width, y + height), paint);
    }

    public void FillRectangle(SkiaBrush brush, float x, float y, float width, float height)
    {
        var rect = new SKRect(x, y, x + width, y + height);
        using var paint = brush.CreatePaint(rect);
        using var shader = paint.Shader;
        _canvas.DrawRect(rect, paint);
    }

    public void FillRectangle(SkiaBrush brush, RectangleF rect)
    {
        FillRectangle(brush, rect.X, rect.Y, rect.Width, rect.Height);
    }

    public void DrawPath(SkiaPen pen, SkiaGraphicsPath path)
    {
        using var paint = pen.CreatePaint();
        _canvas.DrawPath(path.Path, paint);
    }

    public void FillPath(SkiaBrush brush, SkiaGraphicsPath path)
    {
        using var paint = brush.CreatePaint(path.Path.Bounds);
        using var shader = paint.Shader;
        _canvas.DrawPath(path.Path, paint);
    }

    public void FillPolygon(SkiaBrush brush, PointF[] points)
    {
        if (points.Length < 3)
        {
            return;
        }

        using var path = new SKPath();
        path.MoveTo(points[0].X, points[0].Y);
        for (var i = 1; i < points.Length; i++)
        {
            path.LineTo(points[i].X, points[i].Y);
        }
        path.Close();

        using var paint = brush.CreatePaint(path.Bounds);
        using var shader = paint.Shader;
        _canvas.DrawPath(path, paint);
    }

    public void DrawImage(SkiaImage image, RectangleF rect)
    {
        using var paint = new SKPaint
        {
            IsAntialias = true,
            FilterQuality = SKFilterQuality.High
        };
        _canvas.DrawBitmap(image.Bitmap, ToSK(rect), paint);
    }

    public void DrawImage(
        SkiaImage image,
        Rectangle destination,
        int sourceX,
        int sourceY,
        int sourceWidth,
        int sourceHeight,
        GraphicsUnit unit,
        SkiaImageAttributes attributes)
    {
        using var paint = new SKPaint
        {
            IsAntialias = true,
            FilterQuality = SKFilterQuality.High
        };

        SKColorFilter? filter = null;
        try
        {
            if (attributes.Matrix is { } matrix)
            {
                var alpha = (byte)Math.Clamp((int)Math.Round(matrix.Alpha * 255f), 0, 255);
                paint.Color = new SKColor(255, 255, 255, alpha);
                if (matrix.IsTint)
                {
                    filter = SKColorFilter.CreateBlendMode(ToSK(matrix.Tint), SKBlendMode.SrcIn);
                    paint.ColorFilter = filter;
                }
            }

            _canvas.DrawBitmap(
                image.Bitmap,
                new SKRect(destination.Left, destination.Top, destination.Right, destination.Bottom),
                paint);
        }
        finally
        {
            filter?.Dispose();
        }
    }

    public void DrawString(string text, SkiaFont font, SkiaBrush brush, RectangleF rect, TextFormat format)
    {
        var paint = GetTextPaint(font);
        paint.Color = ToSK(brush.Color);

        var metrics = paint.FontMetrics;
        var width = paint.MeasureText(text);
        var lineHeight = metrics.Descent - metrics.Ascent;

        float x;
        float baseline;

        switch (format)
        {
            case TextFormat.Center:
                x = rect.Left + (rect.Width - width) / 2f;
                baseline = rect.Top + (rect.Height - lineHeight) / 2f - metrics.Ascent;
                break;
            case TextFormat.Right:
                x = rect.Right - width;
                baseline = rect.Top + (rect.Height - lineHeight) / 2f - metrics.Ascent;
                break;
            case TextFormat.RightTop:
                x = rect.Right - width;
                baseline = rect.Top - metrics.Ascent;
                break;
            case TextFormat.Left:
                x = rect.Left;
                baseline = rect.Top + (rect.Height - lineHeight) / 2f - metrics.Ascent;
                break;
            case TextFormat.LeftTop:
            case TextFormat.Typographic:
            default:
                x = rect.Left;
                baseline = rect.Top - metrics.Ascent;
                break;
        }

        _canvas.DrawText(text, x, baseline, paint);
    }

    public void DrawString(string text, SkiaFont font, SkiaBrush brush, float x, float y, TextFormat format)
    {
        var paint = GetTextPaint(font);
        paint.Color = ToSK(brush.Color);
        var metrics = paint.FontMetrics;
        var baseline = format == TextFormat.Typographic ? y - metrics.Ascent : y;
        _canvas.DrawText(text, x, baseline, paint);
    }

    public SizeF MeasureString(string text, SkiaFont font)
    {
        var paint = GetTextPaint(font);
        var metrics = paint.FontMetrics;
        return new SizeF(paint.MeasureText(text), metrics.Descent - metrics.Ascent);
    }

    public SizeF MeasureString(string text, SkiaFont font, PointF origin, TextFormat format)
    {
        return MeasureString(text, font);
    }

    public float GetLineHeight(SkiaFont font)
    {
        var paint = GetTextPaint(font);
        var metrics = paint.FontMetrics;
        return metrics.Descent - metrics.Ascent;
    }

    internal static SKColor ToSK(Color color) => new(color.R, color.G, color.B, color.A);

    internal static SKRect ToSK(RectangleF rect) => new(rect.Left, rect.Top, rect.Right, rect.Bottom);

    private static SKPaint GetTextPaint(SkiaFont font)
    {
        lock (Sync)
        {
            if (TextPaints.TryGetValue(font.Key, out var cached))
            {
                return cached;
            }

            var family = SkiaFontResolver.FamilyResolver?.Invoke(font.Family) ?? font.Family;
            var typefaceKey = $"{family}|{font.Bold}";
            if (!Typefaces.TryGetValue(typefaceKey, out var typeface))
            {
                typeface = SKTypeface.FromFamilyName(
                    family,
                    font.Bold ? SKFontStyle.Bold : SKFontStyle.Normal) ?? SKTypeface.Default;
                Typefaces[typefaceKey] = typeface;
            }

            var paint = new SKPaint
            {
                Typeface = typeface,
                TextSize = font.Size,
                IsAntialias = true,
                SubpixelText = true,
                LcdRenderText = false,
                Style = SKPaintStyle.Fill
            };

            TextPaints[font.Key] = paint;
            return paint;
        }
    }
}
