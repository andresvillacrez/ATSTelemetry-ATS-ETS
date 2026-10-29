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
