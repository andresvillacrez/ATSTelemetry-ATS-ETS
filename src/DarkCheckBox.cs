using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ATSTelemetry;

/// <summary>
/// Checkbox dibujado por completo (sin pintado nativo) para que la marca y el
/// texto sean legibles en los temas oscuro, claro y alto contraste.
/// </summary>
internal sealed class DarkCheckBox : Control
{
    private bool _checked;

    public DarkCheckBox()
    {
        SetStyle(
            ControlStyles.UserPaint
            | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw
            | ControlStyles.Selectable
            | ControlStyles.SupportsTransparentBackColor,
            true);

        Height = 24;
        TabStop = true;
        Cursor = Cursors.Hand;
        BackColor = Color.Transparent;
        ForeColor = Theme.TextPrimary;
        AccessibleRole = AccessibleRole.CheckButton;
    }

    public event EventHandler? CheckedChanged;

    public bool Checked
    {
        get => _checked;
        set
        {
            if (_checked == value)
            {
                return;
            }

            _checked = value;
            Invalidate();
            CheckedChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        const float boxSize = 16f;
        var boxY = (Height - boxSize) / 2f;
        var box = new RectangleF(0f, boxY, boxSize, boxSize);

        using (var path = RoundedRect(box, 4f))
        using (var fill = new SolidBrush(Checked ? Theme.Accent : Theme.SurfaceRaised))
        using (var border = new Pen(Checked ? Theme.Accent : Theme.StrokeStrong, 1.2f))
        {
            g.FillPath(fill, path);
            g.DrawPath(border, path);
        }

        if (Checked)
        {
            using var check = new Pen(
                Theme.IsLight ? Color.White : Theme.BackgroundTop, 2f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };
            g.DrawLine(check, box.X + 4f, box.Y + 8f, box.X + 7f, box.Y + 11f);
            g.DrawLine(check, box.X + 7f, box.Y + 11f, box.X + 12f, box.Y + 5f);
        }

        var textRect = new RectangleF(boxSize + 10f, 0f, Width - boxSize - 10f, Height);
        TextRenderer.DrawText(
            g,
            Text,
            Font,
            Rectangle.Round(textRect),
            Enabled ? ForeColor : Theme.TextDisabled,
            TextFormatFlags.Left
            | TextFormatFlags.VerticalCenter
            | TextFormatFlags.NoPrefix
            | TextFormatFlags.EndEllipsis);

        if (Focused && ShowFocusCues)
        {
            ControlPaint.DrawFocusRectangle(
                g,
                Rectangle.Round(textRect),
                ForeColor,
                BackColor);
        }
    }

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        if (Enabled)
        {
            Focus();
            Checked = !Checked;
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (Enabled && e.KeyCode == Keys.Space)
        {
            Checked = !Checked;
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
    }

    protected override void OnEnter(EventArgs e)
    {
        base.OnEnter(e);
        Invalidate();
    }

    protected override void OnLeave(EventArgs e)
    {
        base.OnLeave(e);
        Invalidate();
    }

    private static GraphicsPath RoundedRect(RectangleF rect, float radius)
    {
        var path = new GraphicsPath();
        var diameter = radius * 2f;
        path.AddArc(rect.X, rect.Y, diameter, diameter, 180f, 90f);
        path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270f, 90f);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0f, 90f);
        path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90f, 90f);
        path.CloseFigure();
        return path;
    }
}