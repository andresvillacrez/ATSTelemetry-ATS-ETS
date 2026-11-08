using System.Drawing;
using System.Windows.Forms;

namespace ATSTelemetry;

/// <summary>
/// Diálogo de preferencias. Mantiene el mismo lenguaje visual que el panel:
/// superficies oscuras, un solo acento y controles nativos de Windows.
/// </summary>
internal sealed class SettingsForm : Form
{
    private readonly DarkSelect _layout;
    private readonly DarkSelect _theme;
    private readonly DarkSelect _uiScale;
    private readonly DarkSelect _units;
    private readonly DarkSelect _language;
    private readonly DarkCheckBox _details;
    private readonly DarkCheckBox _reduceMotion;
    private readonly DarkCheckBox _diagnostics;
    private readonly DarkCheckBox _alwaysOnTop;
    private readonly DarkCheckBox _overlay;
    private readonly TrackBar _opacity;
    private readonly Label _opacityValue;
    private readonly DarkCheckBox _clickThrough;
    private readonly Label _clickThroughHint;
    private readonly Snap? _snap;

    public SettingsForm(WindowPreferences preferences, Snap? snap = null)
    {
        _snap = snap;
        Text = "Ajustes";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(480, 752);
        BackColor = Theme.BackgroundTop;
        ForeColor = Theme.TextPrimary;
        Font = new Font("Segoe UI", 9.5f);
        AutoScaleMode = AutoScaleMode.Dpi;

        var title = new Label
        {
            Text = "Ajustes",
            Font = new Font("Segoe UI Semibold", 18f),
            ForeColor = Theme.TextPrimary,
            AutoSize = true,
            Location = new Point(24, 18)
        };
        Controls.Add(title);

        var subtitle = new Label
        {
            Text = "Presentación, ventana e integración con Windows.",
            Font = new Font("Segoe UI", 9f),
            ForeColor = Theme.TextTertiary,
            AutoSize = true,
            Location = new Point(26, 52)
        };
        Controls.Add(subtitle);

        var accent = new Panel
        {
            BackColor = Theme.Accent,
            Location = new Point(24, 80),
            Size = new Size(432, 2)
        };
        Controls.Add(accent);

        AddSection("PRESENTACIÓN", 98);

        AddLabel("Diseño", 24, 128);
        _layout = new DarkSelect
        {
            Location = new Point(128, 124),
            Size = new Size(328, 28)
        };
        _layout.AddItems(
            "Automático según la ventana",
            "Completo · 1920 × 1080",
            "Compacto · 1280 × 720",
            "Mínimo · 960 × 540",
            "Vertical · 1080 × 1920");
        _layout.SelectedIndex = (int)preferences.Layout;
        Controls.Add(_layout);

        _details = AddCheck("Mostrar datos secundarios en los paneles", 24, 168,
            preferences.ShowDetails);
        _reduceMotion = AddCheck("Reducir movimiento y animaciones", 24, 198,
            preferences.ReduceMotion);
        AddSection("VENTANA", 240);

        _alwaysOnTop = AddCheck("Mantener siempre visible", 24, 270,
            preferences.AlwaysOnTop);
        _overlay = AddCheck("Modo superposición sobre el juego", 24, 300,
            preferences.Overlay);

        AddLabel("Opacidad", 24, 338);
        _opacity = new TrackBar
        {
            Minimum = 20,
            Maximum = 100,
            TickFrequency = 10,
            SmallChange = 5,
            LargeChange = 10,
            Value = (int)Math.Round(Math.Clamp(preferences.OverlayOpacity, 0.20f, 1f) * 100f),
            Location = new Point(120, 330),
            Size = new Size(280, 45)
        };
        Controls.Add(_opacity);

        _opacityValue = new Label
        {
            Text = $"{_opacity.Value}%",
            ForeColor = Theme.TextSecondary,
            TextAlign = ContentAlignment.MiddleRight,
            AutoSize = false,
            Location = new Point(404, 340),
            Size = new Size(52, 22)
        };
        Controls.Add(_opacityValue);

        _clickThrough = AddCheck("Permitir clic a través", 24, 388,
            preferences.Overlay && preferences.OverlayClickThrough);
        _clickThrough.Enabled = preferences.Overlay;

        _clickThroughHint = new Label
        {
            Text = "Con clic a través activo, pulsa Ctrl+Shift+O para recuperar el control.",
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = Theme.TextTertiary,
            AutoSize = false,
            Location = new Point(44, 414),
            Size = new Size(412, 20)
        };
        Controls.Add(_clickThroughHint);

        var hint = new Label
        {
            Text = "El modo superposición ocupa la pantalla completa y se dibuja sobre el juego.",
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = Theme.TextTertiary,
            AutoSize = false,
            Location = new Point(24, 450),
            Size = new Size(432, 20)
        };
        Controls.Add(hint);

        AddSection("DIAGNÓSTICO", 482);
        AddInfoRow("Versión", AppInfo.Version, 510);
        AddInfoRow("Perfil", string.IsNullOrWhiteSpace(_snap?.CompanyName)
            ? "SIN PERFIL"
            : _snap!.CompanyName!, 532);
        AddInfoRow("Señal", _snap == null ? "SIN DATOS"
            : _snap.Demo ? "DEMO"
            : _snap.Active ? "EN VIVO"
            : "SIN SEÑAL", 554);
        AddInfoRow("Pantalla", string.IsNullOrWhiteSpace(preferences.ScreenDevice)
            ? "AUTOMÁTICA"
            : preferences.ScreenDevice!, 576);
        AddInfoRow("Preferencias", WindowPreferences.PreferencesPath, 598, small: true);

        AddLabel(Localization.L("THEME").ToUpperInvariant(), 24, 620);
        AddLabel(Localization.L("SCALE"), 136, 620);
        AddLabel(Localization.L("UNITS").ToUpperInvariant(), 248, 620);
        AddLabel(Localization.L("LANGUAGE").ToUpperInvariant(), 360, 620);

        _theme = new DarkSelect
        {
            Location = new Point(24, 638),
            Size = new Size(104, 28)
        };
        _theme.AddItems(Localization.L("THEME_DARK"), Localization.L("THEME_LIGHT"), Localization.L("THEME_CONTRAST"));
        _theme.SelectedIndex = Math.Clamp((int)preferences.Theme, 0, 2);
        Controls.Add(_theme);

        _uiScale = new DarkSelect
        {
            Location = new Point(136, 638),
            Size = new Size(104, 28)
        };
        _uiScale.AddItems("100 %", "125 %", "150 %");
        _uiScale.SelectedIndex = preferences.UiScale >= 1.45f ? 2 : preferences.UiScale >= 1.20f ? 1 : 0;
        Controls.Add(_uiScale);

        _units = new DarkSelect
        {
            Location = new Point(248, 638),
            Size = new Size(104, 28)
        };
        _units.AddItems(Localization.L("METRIC"), Localization.L("IMPERIAL"));
        _units.SelectedIndex = preferences.Units == UnitSystem.Imperial ? 1 : 0;
        Controls.Add(_units);

        _language = new DarkSelect
        {
            Location = new Point(360, 638),
            Size = new Size(96, 28)
        };
        _language.AddItems("Español", "English");
        _language.SelectedIndex = preferences.Language == AppLanguage.En ? 1 : 0;
        Controls.Add(_language);

        _diagnostics = AddCheck("Mostrar overlay de diagnóstico (FPS y frame)", 24, 676,
            preferences.ShowDiagnostics);

        var cancel = new Button
        {
            Text = "Cancelar",
            DialogResult = DialogResult.Cancel,
            FlatStyle = FlatStyle.Flat,
            BackColor = Theme.SurfaceRaised,
            ForeColor = Theme.TextSecondary,
            Location = new Point(252, 708),
            Size = new Size(100, 32)
        };
        cancel.FlatAppearance.BorderColor = Theme.StrokeStrong;
        Controls.Add(cancel);

        var accept = new Button
        {
            Text = "Guardar",
            DialogResult = DialogResult.OK,
            FlatStyle = FlatStyle.Flat,
            BackColor = Theme.SurfaceRaised,
            ForeColor = Theme.TextPrimary,
            Location = new Point(360, 708),
            Size = new Size(100, 32)
        };
        accept.FlatAppearance.BorderColor = Theme.Accent;
        Controls.Add(accept);

        AcceptButton = accept;
        CancelButton = cancel;

        _overlay.CheckedChanged += (_, _) =>
        {
            var enabled = _overlay.Checked;
            _opacity.Enabled = enabled;
            _opacityValue.Enabled = enabled;
            _clickThrough.Enabled = enabled;
            if (!enabled)
            {
                _clickThrough.Checked = false;
            }
        };

        _opacity.ValueChanged += (_, _) =>
        {
            _opacityValue.Text = $"{_opacity.Value}%";
        };
    }

    public void ApplyTo(WindowPreferences preferences)
    {
        preferences.Theme = (ThemeMode)Math.Clamp(_theme.SelectedIndex, 0, 2);
        preferences.UiScale = _uiScale.SelectedIndex switch
        {
            2 => 1.50f,
            1 => 1.25f,
            _ => 1.00f
        };
        preferences.Units = _units.SelectedIndex == 1 ? UnitSystem.Imperial : UnitSystem.Metric;
        preferences.Language = _language.SelectedIndex == 1 ? AppLanguage.En : AppLanguage.Es;
        preferences.Layout = (LayoutMode)Math.Clamp(_layout.SelectedIndex, 0, 4);
        preferences.ShowDetails = _details.Checked;
        preferences.ReduceMotion = _reduceMotion.Checked;
        preferences.ShowDiagnostics = _diagnostics.Checked;
        preferences.AlwaysOnTop = _alwaysOnTop.Checked;
        preferences.Overlay = _overlay.Checked;
        preferences.OverlayOpacity = _opacity.Value / 100f;
        preferences.OverlayClickThrough = _overlay.Checked && _clickThrough.Checked;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        WindowChrome.ApplyTitleBar(Handle, !Theme.IsLight);
    }

    private void AddSection(string text, int y)
    {
        var label = new Label
        {
            Text = text,
            Font = new Font("Segoe UI Semibold", 8.5f),
            ForeColor = Theme.TextTertiary,
            AutoSize = true,
            Location = new Point(24, y)
        };
        Controls.Add(label);
    }

    private void AddLabel(string text, int x, int y)
    {
        var label = new Label
        {
            Text = text,
            ForeColor = Theme.TextSecondary,
            AutoSize = true,
            Location = new Point(x, y)
        };
        Controls.Add(label);
    }

    private void AddInfoRow(string label, string value, int y, bool small = false)
    {
        AddLabel(label, 24, y);

        var valueLabel = new Label
        {
            Text = value,
            Font = new Font("Segoe UI", small ? 8f : 9f),
            ForeColor = small ? Theme.TextTertiary : Theme.TextPrimary,
            AutoSize = false,
            AutoEllipsis = true,
            Location = new Point(120, y),
            Size = new Size(336, 18)
        };
        Controls.Add(valueLabel);
    }

    private DarkCheckBox AddCheck(string text, int x, int y, bool isChecked)
    {
        var check = new DarkCheckBox
        {
            Text = text,
            Checked = isChecked,
            Location = new Point(x, y),
            Size = new Size(432, 24)
        };
        Controls.Add(check);
        return check;
    }
}
