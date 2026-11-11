using System.Drawing;
using System.Windows.Forms;
using SkiaSharp;
using SkiaSharp.Views.Desktop;

namespace ATSTelemetry;

public sealed class DashboardControl : SKGLControl
{
    private readonly DashboardRenderer _renderer;

    public DashboardControl(TelemetryReader reader)
    {
        _renderer = new DashboardRenderer(reader);
        BackColor = Color.Black;
        Dock = DockStyle.Fill;
        VSync = true;
    }

    public string DisplayName
    {
        get => _renderer.DisplayName;
        set => _renderer.DisplayName = value;
    }

    public bool ShowDetails
    {
        get => _renderer.ShowDetails;
        set => _renderer.ShowDetails = value;
    }

    public bool ReduceMotion
    {
        get => _renderer.ReduceMotion;
        set => _renderer.ReduceMotion = value;
    }

    public LayoutMode LayoutMode
    {
        get => _renderer.LayoutMode;
        set => _renderer.LayoutMode = value;
    }

    public bool ShowDiagnostics
    {
        get => _renderer.ShowDiagnostics;
        set => _renderer.ShowDiagnostics = value;
    }

    public event EventHandler? SettingsRequested;
    public event EventHandler? CreditsRequested;

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        Cursor = _renderer.HitTest(e.X, e.Y, ClientSize.Width, ClientSize.Height)
            == DashboardRenderer.DashboardAction.None
            ? Cursors.Default
            : Cursors.Hand;
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        switch (_renderer.HitTest(e.X, e.Y, ClientSize.Width, ClientSize.Height))
        {
            case DashboardRenderer.DashboardAction.Settings:
                SettingsRequested?.Invoke(this, EventArgs.Empty);
                break;
            case DashboardRenderer.DashboardAction.Credits:
                CreditsRequested?.Invoke(this, EventArgs.Empty);
                break;
        }
    }

    public UnitSystem Units
    {
        get => _renderer.Units;
        set => _renderer.Units = value;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        VSync = true;
    }

    protected override void OnPaintSurface(SKPaintGLSurfaceEventArgs e)
    {
        _renderer.Render(e.Surface.Canvas, e.Info.Width, e.Info.Height);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _renderer.Dispose();
        }

        base.Dispose(disposing);
    }
}

