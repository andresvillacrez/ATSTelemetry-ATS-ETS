using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ATSTelemetry;

/// <summary>
/// Selector oscuro/claro dibujado a mano. Sustituye a ComboBox para evitar
/// los bordes blancos del tema nativo y mantener el mismo lenguaje visual.
/// </summary>
internal sealed class DarkSelect : Control
{
    private readonly List<string> _items = new();
    private int _selectedIndex = -1;

    public DarkSelect()
    {
        SetStyle(
            ControlStyles.UserPaint
            | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw
            | ControlStyles.Selectable
            | ControlStyles.SupportsTransparentBackColor,
            true);

        Height = 28;
        TabStop = true;
        Cursor = Cursors.Hand;
        Font = new Font("Segoe UI", 9.5f);
        ForeColor = Theme.TextPrimary;
        BackColor = Color.Transparent;
    }

    public event EventHandler? SelectedIndexChanged;

    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            var index = _items.Count == 0 ? -1 : Math.Clamp(value, -1, _items.Count - 1);
            if (index == _selectedIndex)
            {
                return;
            }

            _selectedIndex = index;
            Invalidate();
            SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public string? SelectedItem =>
        _selectedIndex >= 0 && _selectedIndex < _items.Count ? _items[_selectedIndex] : null;

    public void AddItems(params string[] items)
    {
        _items.AddRange(items);
        if (_selectedIndex < 0 && _items.Count > 0)
        {
            _selectedIndex = 0;
        }
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var rect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
        using (var path = RoundedRect(rect, 8f))
        using (var fill = new SolidBrush(Theme.SurfaceRaised))
        using (var border = new Pen(Focused ? Theme.Accent : Theme.StrokeStrong, 1f))
        {
            g.FillPath(fill, path);
            g.DrawPath(border, path);
        }

        var text = SelectedItem ?? string.Empty;
        var textRect = new Rectangle(12, 0, Width - 44, Height);
        TextRenderer.DrawText(
            g,
            text,
            Font,
            textRect,
            Enabled ? Theme.TextPrimary : Theme.TextDisabled,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

        // Chevron
        var cx = Width - 20f;
        var cy = Height / 2f;
        using var chevron = new Pen(Theme.TextTertiary, 1.6f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        g.DrawLine(chevron, cx - 4f, cy - 2f, cx, cy + 2f);
        g.DrawLine(chevron, cx, cy + 2f, cx + 4f, cy - 2f);

        if (Focused && ShowFocusCues)
        {
            ControlPaint.DrawFocusRectangle(
                g,
                new Rectangle(4, 4, Width - 8, Height - 8),
                Theme.TextPrimary,
                Theme.SurfaceRaised);
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left)
        {
            Focus();
            ShowMenu();
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode is Keys.Enter or Keys.Space or Keys.Down)
        {
            ShowMenu();
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

    private void ShowMenu()
    {
        if (_items.Count == 0)
        {
            return;
        }

        var menu = new ContextMenuStrip
        {
            ShowImageMargin = false,
            ShowCheckMargin = true
        };
        WindowChrome.ApplyDarkMenu(menu);

        for (var i = 0; i < _items.Count; i++)
        {
            var index = i;
            var item = new ToolStripMenuItem(_items[i])
            {
                Checked = index == _selectedIndex,
                CheckOnClick = false
            };
            item.Click += (_, _) => SelectedIndex = index;
            menu.Items.Add(item);
        }

        WindowChrome.ApplyDarkMenu(menu.Items);
        menu.Show(this, new Point(0, Height));
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