using System.Drawing;
using System.Windows.Forms;

namespace ATSTelemetry;

/// <summary>
/// Diálogo de créditos y derechos. Mantiene el mismo lenguaje visual oscuro
/// que Ajustes y se muestra por delante del panel principal.
/// </summary>
internal sealed class CreditsForm : Form
{
    public CreditsForm()
    {
        Text = "Créditos";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(480, 380);
        BackColor = Theme.BackgroundTop;
        ForeColor = Theme.TextPrimary;
        Font = new Font("Segoe UI", 9.5f);
        AutoScaleMode = AutoScaleMode.Dpi;

        var title = new Label
        {
            Text = "Créditos",
            Font = new Font("Segoe UI Semibold", 18f),
            ForeColor = Theme.TextPrimary,
            AutoSize = true,
            Location = new Point(24, 18)
        };
        Controls.Add(title);

        var accent = new Panel
        {
            BackColor = Theme.Accent,
            Location = new Point(24, 62),
            Size = new Size(432, 2)
        };
        Controls.Add(accent);

        AddLine("ATS Telemetry", 82, 15f, FontStyle.Bold, Theme.TextPrimary);
        AddLine(AppInfo.Version, 110, 10f, FontStyle.Regular, Theme.TextTertiary);
        AddLine("Panel de telemetría para American Truck Simulator y Euro Truck Simulator 2.",
            142, 10f, FontStyle.Regular, Theme.TextSecondary);
        AddLine("Creador y mantenimiento: ATS Telemetry.",
            176, 10f, FontStyle.Regular, Theme.TextSecondary);
        AddLine("© 2026 ATS Telemetry. Todos los derechos reservados.",
            200, 10f, FontStyle.Regular, Theme.TextSecondary);

        var separator = new Panel
        {
            BackColor = Theme.StrokeStrong,
            Location = new Point(24, 236),
            Size = new Size(432, 1)
        };
        Controls.Add(separator);

        AddLine("Tecnologías: .NET 8 · WinForms · SkiaSharp · SCS Telemetry.",
            254, 9f, FontStyle.Regular, Theme.TextTertiary);
        AddLine("Agradecimientos: SCS Software y la comunidad de ATS / ETS2.",
            278, 9f, FontStyle.Regular, Theme.TextTertiary);
        AddLine("Los nombres de marcas y juegos pertenecen a sus respectivos propietarios.",
            302, 8.5f, FontStyle.Regular, Theme.TextTertiary);

        var close = new Button
        {
            Text = "Cerrar",
            DialogResult = DialogResult.OK,
            FlatStyle = FlatStyle.Flat,
            BackColor = Theme.SurfaceRaised,
            ForeColor = Theme.TextPrimary,
            Location = new Point(360, 330),
            Size = new Size(100, 32)
        };
        close.FlatAppearance.BorderColor = Theme.Accent;
        Controls.Add(close);

        AcceptButton = close;
        CancelButton = close;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        WindowChrome.ApplyTitleBar(Handle, !Theme.IsLight);
    }

    private void AddLine(string text, int y, float size, FontStyle style, Color color)
    {
        var label = new Label
        {
            Text = text,
            Font = new Font("Segoe UI", size, style),
            ForeColor = color,
            AutoSize = false,
            Location = new Point(24, y),
            Size = new Size(432, 22)
        };
        Controls.Add(label);
    }
}