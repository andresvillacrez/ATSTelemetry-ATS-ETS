using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

namespace ATSTelemetry;

public sealed class MainForm : Form
{
    private const int WmFrame = 0x8000 + 1;
    private const int WmHotKey = 0x0312;
    private const int HotKeyToggleClickThrough = 1;
    private const int HotKeyToggleOverlay = 2;
    private const int HotKeyOpenSettings = 3;
    private const int HotKeyCycleLayout = 4;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const int GwlExStyle = -20;
    private const int WsExTransparent = 0x20;
    private const int WsExLayered = 0x80000;

    private readonly TelemetryReader _reader;
    private DashboardControl _dashboard;
    private FrameClock _frameClock;
    private readonly NotifyIcon _tray;
    private readonly WindowPreferences _preferences;
    private readonly string? _screenTarget;
    private readonly bool _startWindowed;
    private ToolStripMenuItem? _screenMenu;
    private ToolStripMenuItem? _windowedItem;
    private ToolStripMenuItem? _overlayItem;
    private ToolStripMenuItem? _clickThroughItem;
    private ToolStripMenuItem? _alwaysOnTopItem;
    private ToolStripMenuItem? _layoutMenu;
    private bool _windowed;
    private bool _switchingMode;
    private bool _overlay;
    private bool _clickThrough;
    private bool _settingsOpen;
    private readonly System.Windows.Forms.Timer _clickThroughGuard;
    private string _displayName = string.Empty;
    private Icon? _appIcon;
    private IntPtr _hotKeyHandle;
    private int _frameQueued;

    public MainForm(string[] args)
    {
        var demo = args.Any(arg => arg.Equals("--demo", StringComparison.OrdinalIgnoreCase));
        _screenTarget = args.FirstOrDefault(arg => arg.StartsWith("--screen=", StringComparison.OrdinalIgnoreCase))
            ?.Substring("--screen=".Length);

        _preferences = WindowPreferences.Load();
        Theme.Apply(_preferences.Theme, _preferences.UiScale);

        var hasExplicitScreen = !string.IsNullOrWhiteSpace(_screenTarget);
        var forcedWindowed = args.Any(arg =>
            arg.Equals("--windowed", StringComparison.OrdinalIgnoreCase)
            || arg.Equals("--window", StringComparison.OrdinalIgnoreCase));
        _startWindowed = forcedWindowed || (!hasExplicitScreen && _preferences.Windowed);

        _reader = new TelemetryReader(demo);
        _reader.WeatherAuto = true;
        _dashboard = new DashboardControl(_reader) { Dock = DockStyle.Fill };
        _dashboard.SettingsRequested += (_, _) => ShowSettings();
        _dashboard.CreditsRequested += (_, _) => ShowCredits();
        ApplyRendererPreferences();

        Text = "ATS Telemetry";
        _appIcon = LoadAppIcon();
        Icon = _appIcon;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        BackColor = Theme.BackgroundTop;
        ShowInTaskbar = false;
        TopMost = true;
        KeyPreview = true;
        AutoScaleMode = AutoScaleMode.None;
        DoubleBuffered = true;

        // Red de seguridad: si el clic a través queda activo por olvido, se
        // desactiva solo a los 30 minutos y avisa en la bandeja.
        _clickThroughGuard = new System.Windows.Forms.Timer { Interval = 30 * 60 * 1000 };
        _clickThroughGuard.Tick += (_, _) =>
        {
            _clickThroughGuard.Stop();
            ApplyClickThrough(false);
            _tray?.ShowBalloonTip(
                5000,
                "Clic a través desactivado",
                "Se restauró el control de la ventana por seguridad.",
                ToolTipIcon.Info);
        };

        Controls.Add(_dashboard);

        ApplyStartupLayout();

        if (_preferences.Overlay)
        {
            // El clic a través nunca se restaura al arrancar.
            EnableOverlay(restoreClickThrough: false);
        }

        _frameClock = new FrameClock(60, RequestFrame);
        _frameClock.Start();

        _tray = BuildTrayIcon();

        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape && _overlay)
            {
                if (_clickThrough)
                {
                    ApplyClickThrough(false);
                }
                else
                {
                    DisableOverlay();
                }

                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }

            if (e.KeyCode == Keys.F11 && !e.Control && !e.Alt && !e.Shift)
            {
                ToggleWindowed();
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }

            if (e.Control && e.Shift && e.KeyCode == Keys.Q)
            {
                Close();
            }
        };

        Move += (_, _) =>
        {
            if (!_windowed || _overlay || WindowState != FormWindowState.Normal)
            {
                return;
            }

            UpdateSavedBounds();
            var screen = CurrentScreen();
            _preferences.ScreenDevice = screen.DeviceName;
            UpdateScreenLabel(screen);
        };

        Resize += (_, _) => UpdateSavedBounds();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        SavePreferences();
        UnregisterGlobalHotKeys();
        _clickThroughGuard.Stop();
        _clickThroughGuard.Dispose();
        _frameClock.Dispose();
        _tray.Visible = false;
        _tray.Dispose();
        _reader.Dispose();
        base.OnFormClosing(e);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        WindowChrome.ApplyTitleBar(Handle, !Theme.IsLight);
        _hotKeyHandle = Handle;
        RegisterGlobalHotKeys();
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        UnregisterGlobalHotKeys();
        _hotKeyHandle = IntPtr.Zero;
        base.OnHandleDestroyed(e);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _appIcon?.Dispose();
            _appIcon = null;
        }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmHotKey)
        {
            if (_settingsOpen)
            {
                return;
            }

            switch (m.WParam.ToInt32())
            {
                case HotKeyToggleClickThrough:
                    if (_overlay)
                    {
                        ApplyClickThrough(!_clickThrough);
                    }
                    return;
                case HotKeyToggleOverlay:
                    ToggleOverlay();
                    return;
                case HotKeyOpenSettings:
                    // Abrir el diálogo fuera del WndProc evita bloquear el bucle
                    // de mensajes mientras el atajo llega desde el juego.
                    BeginInvoke(ShowSettings);
                    return;
                case HotKeyCycleLayout:
                    CycleLayout();
                    return;
            }
        }

        if (m.Msg == WmFrame)
        {
            Interlocked.Exchange(ref _frameQueued, 0);

            if (!_switchingMode && !IsDisposed && !_dashboard.IsDisposed && _dashboard.IsHandleCreated)
            {
                _dashboard.Invalidate();
            }

            return;
        }

        base.WndProc(ref m);

        const int WmDisplayChange = 0x007E;
        if (m.Msg == WmDisplayChange)
        {
            BeginInvoke(OnDisplayChanged);
        }
    }

    private void RequestFrame()
    {
        if (_switchingMode || IsDisposed || !IsHandleCreated)
        {
            return;
        }

        if (Interlocked.Exchange(ref _frameQueued, 1) == 1)
        {
            return;
        }

        try
        {
            if (!PostMessage(Handle, WmFrame, IntPtr.Zero, IntPtr.Zero))
            {
                Interlocked.Exchange(ref _frameQueued, 0);
            }
        }
        catch (ObjectDisposedException)
        {
            Interlocked.Exchange(ref _frameQueued, 0);
        }
    }

    private void ApplyStartupLayout()
    {
        var screen = FindTargetScreen(_screenTarget)
                     ?? FindTargetScreen(_preferences.ScreenDevice)
                     ?? Screen.FromPoint(Cursor.Position);

        if (_startWindowed)
        {
            ApplyWindowed(screen);
        }
        else
        {
            ApplyFullscreen(screen);
        }
    }

    private void ApplyFullscreen(Screen screen, bool save = true)
    {
        ApplyLayout(screen, windowed: false, save);
    }

    private void ApplyWindowed(Screen screen, bool save = true)
    {
        ApplyLayout(screen, windowed: true, save);
    }

    private void ApplyLayout(Screen screen, bool windowed, bool save)
    {
        var targetBorder = windowed ? FormBorderStyle.Sizable : FormBorderStyle.None;
        var recreateDashboard = IsHandleCreated && FormBorderStyle != targetBorder;

        if (recreateDashboard)
        {
            BeginLayoutChange();
        }

        if (!windowed && _windowed)
        {
            UpdateSavedBounds();
        }

        _windowed = windowed;
        WindowState = FormWindowState.Normal;
        FormBorderStyle = targetBorder;
        ShowInTaskbar = windowed;
        MaximizeBox = windowed;
        MinimizeBox = windowed;
        TopMost = _preferences.AlwaysOnTop || _overlay;

        if (windowed)
        {
            var bounds = RestoreWindowBounds(screen) ?? DefaultWindowBounds(screen);
            MinimumSize = new Size(Math.Min(640, bounds.Width), Math.Min(360, bounds.Height));
            Bounds = bounds;
        }
        else
        {
            MinimumSize = Size.Empty;
            Bounds = screen.Bounds;
        }

        UpdateScreenLabel(screen);
        _preferences.ScreenDevice = screen.DeviceName;
        _preferences.Windowed = windowed;
        UpdateWindowMenu();

        if (recreateDashboard)
        {
            CompleteLayoutChange();
        }

        if (save)
        {
            _preferences.Save();
        }
    }

    private static Rectangle DefaultWindowBounds(Screen screen)
    {
        var area = screen.WorkingArea;
        var width = Math.Min(1280, Math.Max(480, (int)(area.Width * 0.9)));
        var height = Math.Min(720, Math.Max(320, (int)(area.Height * 0.9)));

        return new Rectangle(
            area.Left + (area.Width - width) / 2,
            area.Top + (area.Height - height) / 2,
            width,
            height);
    }

    private Rectangle? RestoreWindowBounds(Screen screen)
    {
        if (_preferences.WindowX is not { } x
            || _preferences.WindowY is not { } y
            || _preferences.WindowWidth is not { } width
            || _preferences.WindowHeight is not { } height)
        {
            return null;
        }

        if (width < 480 || height < 320)
        {
            return null;
        }

        var area = screen.WorkingArea;
        var intersection = Rectangle.Intersect(new Rectangle(x, y, width, height), area);
        if (intersection.Width < 160 || intersection.Height < 120)
        {
            return null;
        }

        width = Math.Min(width, area.Width);
        height = Math.Min(height, area.Height);

        var minX = area.Left - width + 160;
        var maxX = area.Right - 160;
        x = maxX >= minX ? Math.Clamp(x, minX, maxX) : area.Left;

        var minY = area.Top;
        var maxY = area.Bottom - 120;
        y = maxY >= minY ? Math.Clamp(y, minY, maxY) : area.Top;

        return new Rectangle(x, y, width, height);
    }

    private void UpdateSavedBounds()
    {
        if (!_windowed || _overlay || WindowState != FormWindowState.Normal)
        {
            return;
        }

        var bounds = Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        _preferences.WindowX = bounds.X;
        _preferences.WindowY = bounds.Y;
        _preferences.WindowWidth = bounds.Width;
        _preferences.WindowHeight = bounds.Height;
    }

    private void BeginLayoutChange()
    {
        _switchingMode = true;
        _frameClock.Dispose();
        Controls.Remove(_dashboard);
        _dashboard.Dispose();
    }

    private void CompleteLayoutChange()
    {
        _dashboard = new DashboardControl(_reader)
        {
            Dock = DockStyle.Fill,
            DisplayName = _displayName,
            ShowDetails = _preferences.ShowDetails,
            ReduceMotion = _preferences.ReduceMotion,
            LayoutMode = _preferences.Layout
        };
        Controls.Add(_dashboard);
        _dashboard.BringToFront();

        Interlocked.Exchange(ref _frameQueued, 0);
        _frameClock = new FrameClock(60, RequestFrame);
        _switchingMode = false;
        _frameClock.Start();
    }

    private void ToggleWindowed()
    {
        if (_overlay)
        {
            DisableOverlay();
            return;
        }

        var screen = CurrentScreen();

        if (_windowed)
        {
            ApplyFullscreen(screen);
        }
        else
        {
            ApplyWindowed(screen);
        }

        Activate();
    }

    private void MoveToScreen(Screen screen)
    {
        if (_overlay)
        {
            Bounds = screen.Bounds;
            return;
        }

        if (_windowed)
        {
            ApplyWindowed(screen);
        }
        else
        {
            ApplyFullscreen(screen);
        }

        Activate();
    }

    private Screen CurrentScreen()
    {
        if (Bounds.Width > 0 && Bounds.Height > 0)
        {
            return Screen.FromRectangle(Bounds);
        }

        return Screen.PrimaryScreen ?? Screen.AllScreens[0];
    }

    private static string ScreenLabel(Screen screen)
    {
        var friendly = GetMonitorFriendlyName(screen.DeviceName);
        if (!string.IsNullOrWhiteSpace(friendly)
            && !friendly.Contains("Generic PnP", StringComparison.OrdinalIgnoreCase))
        {
            return friendly.Trim();
        }

        return screen.DeviceName.Replace(@"\\.\", string.Empty);
    }

    private void UpdateScreenLabel(Screen screen)
    {
        _displayName = ScreenLabel(screen);

        if (!_dashboard.IsDisposed)
        {
            _dashboard.DisplayName = _displayName;
        }
    }

    private void SavePreferences()
    {
        var screen = CurrentScreen();
        _preferences.ScreenDevice = screen.DeviceName;
        if (!_overlay)
        {
            _preferences.Windowed = _windowed;
            UpdateSavedBounds();
        }
        _preferences.Save();
    }

    private void ApplyRendererPreferences()
    {
        if (_dashboard.IsDisposed)
        {
            return;
        }

        _dashboard.ShowDetails = _preferences.ShowDetails;
        _dashboard.ReduceMotion = _preferences.ReduceMotion;
        _dashboard.ShowDiagnostics = _preferences.ShowDiagnostics;
        _dashboard.Units = _preferences.Units;
        _dashboard.LayoutMode = _preferences.Layout;
    }

    private void RegisterGlobalHotKeys()
    {
        if (_hotKeyHandle == IntPtr.Zero)
        {
            return;
        }

        var clickThrough = RegisterHotKey(
            _hotKeyHandle, HotKeyToggleClickThrough, ModControl | ModShift, (uint)Keys.O);
        var overlay = RegisterHotKey(
            _hotKeyHandle, HotKeyToggleOverlay, ModControl | ModShift, (uint)Keys.F);
        var settings = RegisterHotKey(
            _hotKeyHandle, HotKeyOpenSettings, ModControl | ModShift, (uint)Keys.A);
        var layout = RegisterHotKey(
            _hotKeyHandle, HotKeyCycleLayout, ModControl | ModShift, (uint)Keys.L);

        if (!(clickThrough && overlay && settings && layout) && _tray is not null)
        {
            _tray.ShowBalloonTip(
                4000,
                "Atajos globales",
                "Algunos atajos no se pudieron registrar. El menú de la bandeja sigue disponible.",
                ToolTipIcon.Warning);
        }
    }

    private void UnregisterGlobalHotKeys()
    {
        if (_hotKeyHandle == IntPtr.Zero)
        {
            return;
        }

        UnregisterHotKey(_hotKeyHandle, HotKeyToggleClickThrough);
        UnregisterHotKey(_hotKeyHandle, HotKeyToggleOverlay);
        UnregisterHotKey(_hotKeyHandle, HotKeyOpenSettings);
        UnregisterHotKey(_hotKeyHandle, HotKeyCycleLayout);
    }

    private void CycleLayout()
    {
        var next = _preferences.Layout switch
        {
            LayoutMode.Auto => LayoutMode.Full,
            LayoutMode.Full => LayoutMode.Compact,
            LayoutMode.Compact => LayoutMode.Minimal,
            _ => LayoutMode.Auto
        };

        SetLayout(next);

        if (_tray is not null)
        {
            _tray.ShowBalloonTip(2500, "Diseño", LayoutLabel(next), ToolTipIcon.Info);
        }
    }

    private void SetLayout(LayoutMode mode)
    {
        _preferences.Layout = mode;
        ApplyRendererPreferences();
        SavePreferences();
        UpdateLayoutMenu();
    }

    private void UpdateLayoutMenu()
    {
        if (_layoutMenu == null)
        {
            return;
        }

        foreach (ToolStripItem item in _layoutMenu.DropDownItems)
        {
            if (item is ToolStripMenuItem menuItem && menuItem.Tag is LayoutMode mode)
            {
                menuItem.Checked = mode == _preferences.Layout;
            }
        }
    }

    private static string LayoutLabel(LayoutMode mode) => mode switch
    {
        LayoutMode.Full => "Completo · 1920 × 1080",
        LayoutMode.Compact => "Compacto · 1280 × 720",
        LayoutMode.Minimal => "Mínimo · 960 × 540",
        _ => "Automático según la ventana"
    };

    private void ShowSettings()
    {
        if (_settingsOpen)
        {
            return;
        }

        _settingsOpen = true;

        try
        {
            using var form = new SettingsForm(_preferences, _reader.Snapshot());
            // El panel principal es TopMost; se baja temporalmente para que el
            // diálogo pueda quedar delante y recibir foco.
            var restoreTopMost = TopMost;
            TopMost = false;
            form.TopMost = true;
            form.Shown += (_, _) =>
            {
                form.Activate();
                form.BringToFront();
            };

            var result = form.ShowDialog(this);
            TopMost = restoreTopMost;

            if (result != DialogResult.OK)
            {
                return;
            }

            form.ApplyTo(_preferences);
            Theme.Apply(_preferences.Theme, _preferences.UiScale);
            WindowChrome.ApplyTitleBar(Handle, !Theme.IsLight);
            BackColor = Theme.BackgroundTop;
            if (_tray.ContextMenuStrip is { } trayMenu)
            {
                WindowChrome.ApplyDarkMenu(trayMenu);
            }
            ApplyRendererPreferences();
            _dashboard.Invalidate();

            if (_preferences.Overlay != _overlay)
            {
                if (_preferences.Overlay)
                {
                    EnableOverlay(restoreClickThrough: true);
                }
                else
                {
                    DisableOverlay();
                }
            }
            else if (_overlay)
            {
                Opacity = _preferences.OverlayOpacity;
                ApplyClickThrough(_preferences.OverlayClickThrough);
            }

            TopMost = _preferences.AlwaysOnTop || _overlay;
            SavePreferences();
            UpdateOverlayMenu();
            UpdateLayoutMenu();
        }
        finally
        {
            _settingsOpen = false;
        }
    }

    private void ShowCredits()
    {
        if (_settingsOpen)
        {
            return;
        }

        _settingsOpen = true;

        try
        {
            using var form = new CreditsForm();
            var restoreTopMost = TopMost;
            TopMost = false;
            form.TopMost = true;
            form.Shown += (_, _) =>
            {
                form.Activate();
                form.BringToFront();
            };

            form.ShowDialog(this);
            TopMost = restoreTopMost;
        }
        finally
        {
            _settingsOpen = false;
        }
    }

    private void ToggleOverlay()
    {
        if (_overlay)
        {
            DisableOverlay();
        }
        else
        {
            EnableOverlay(restoreClickThrough: true);
        }
    }

    private void EnableOverlay(bool restoreClickThrough)
    {
        if (_overlay)
        {
            return;
        }

        if (_windowed)
        {
            UpdateSavedBounds();
        }

        var restoreWindowed = _preferences.Windowed;
        _overlay = true;
        _preferences.Overlay = true;

        ApplyLayout(CurrentScreen(), windowed: false, save: false);
        _preferences.Windowed = restoreWindowed;

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        Opacity = _preferences.OverlayOpacity;
        TopMost = true;

        if (restoreClickThrough)
        {
            ApplyClickThrough(_preferences.OverlayClickThrough);
        }
        else
        {
            // Nunca se restaura al arrancar: solo se activa de forma explícita.
            _clickThrough = false;
            _preferences.OverlayClickThrough = false;
        }

        _preferences.Save();
        UpdateOverlayMenu();
    }

    private void DisableOverlay()
    {
        if (!_overlay)
        {
            return;
        }

        _overlay = false;
        _preferences.Overlay = false;
        ApplyClickThrough(false);
        Opacity = 1.0;

        ApplyLayout(CurrentScreen(), windowed: _preferences.Windowed, save: false);
        _preferences.Save();
        UpdateOverlayMenu();
    }

    private void ApplyClickThrough(bool enabled)
    {
        if (enabled && !_overlay)
        {
            return;
        }

        if (IsHandleCreated)
        {
            var style = GetWindowLong(Handle, GwlExStyle);
            style = enabled
                ? style | WsExTransparent | WsExLayered
                : style & ~WsExTransparent;
            SetWindowLong(Handle, GwlExStyle, style);
        }

        _clickThrough = enabled;
        _preferences.OverlayClickThrough = enabled;

        if (enabled)
        {
            _clickThroughGuard.Start();
        }
        else
        {
            _clickThroughGuard.Stop();
        }

        UpdateOverlayMenu();
    }

    private void UpdateOverlayMenu()
    {
        if (_overlayItem != null)
        {
            _overlayItem.Checked = _overlay;
        }

        if (_clickThroughItem != null)
        {
            _clickThroughItem.Checked = _clickThrough;
            _clickThroughItem.Enabled = _overlay;
        }

        if (_alwaysOnTopItem != null)
        {
            _alwaysOnTopItem.Checked = _preferences.AlwaysOnTop;
        }

        if (_windowedItem != null)
        {
            _windowedItem.Checked = _windowed && !_overlay;
        }
    }

    private void OnDisplayChanged()
    {
        if (IsDisposed || !IsHandleCreated)
        {
            return;
        }

        if (_overlay)
        {
            Bounds = CurrentScreen().Bounds;
            return;
        }

        var screen = CurrentScreen();

        if (_windowed)
        {
            ApplyWindowed(screen);
        }
        else
        {
            ApplyFullscreen(screen);
        }
    }

    private NotifyIcon BuildTrayIcon()
    {
        var menu = new ContextMenuStrip();
        WindowChrome.ApplyDarkMenu(menu);

        _screenMenu = new ToolStripMenuItem("Pantalla");
        _screenMenu.DropDownOpening += (_, _) => RebuildScreenMenu();
        menu.Items.Add(_screenMenu);

        _windowedItem = new ToolStripMenuItem("Modo ventana")
        {
            ShortcutKeyDisplayString = "F11",
            CheckOnClick = false,
            Checked = _windowed
        };
        _windowedItem.Click += (_, _) => ToggleWindowed();
        menu.Items.Add(_windowedItem);

        _layoutMenu = new ToolStripMenuItem("Diseño")
        {
            ShortcutKeyDisplayString = "Ctrl+Shift+L"
        };
        AddLayoutItem(_layoutMenu, "Automático según la ventana", LayoutMode.Auto);
        AddLayoutItem(_layoutMenu, "Completo · 1920 × 1080", LayoutMode.Full);
        AddLayoutItem(_layoutMenu, "Compacto · 1280 × 720", LayoutMode.Compact);
        AddLayoutItem(_layoutMenu, "Mínimo · 960 × 540", LayoutMode.Minimal);
        menu.Items.Add(_layoutMenu);

        menu.Items.Add(new ToolStripSeparator());

        _overlayItem = new ToolStripMenuItem("Superposición")
        {
            ShortcutKeyDisplayString = "Ctrl+Shift+F",
            CheckOnClick = false,
            Checked = _overlay
        };
        _overlayItem.Click += (_, _) => ToggleOverlay();
        menu.Items.Add(_overlayItem);

        _clickThroughItem = new ToolStripMenuItem("Clic a través")
        {
            ShortcutKeyDisplayString = "Ctrl+Shift+O",
            CheckOnClick = false,
            Checked = _clickThrough,
            Enabled = _overlay
        };
        _clickThroughItem.Click += (_, _) => ApplyClickThrough(!_clickThrough);
        menu.Items.Add(_clickThroughItem);

        _alwaysOnTopItem = new ToolStripMenuItem("Siempre visible")
        {
            CheckOnClick = false,
            Checked = _preferences.AlwaysOnTop
        };
        _alwaysOnTopItem.Click += (_, _) =>
        {
            _preferences.AlwaysOnTop = !_preferences.AlwaysOnTop;
            TopMost = _preferences.AlwaysOnTop || _overlay;
            UpdateOverlayMenu();
            SavePreferences();
        };
        menu.Items.Add(_alwaysOnTopItem);

        menu.Items.Add(new ToolStripSeparator());

        var settings = new ToolStripMenuItem("Ajustes…")
        {
            ShortcutKeyDisplayString = "Ctrl+Shift+A"
        };
        settings.Click += (_, _) => ShowSettings();
        menu.Items.Add(settings);

        var credits = new ToolStripMenuItem("Créditos");
        credits.Click += (_, _) => ShowCredits();
        menu.Items.Add(credits);

        menu.Items.Add(new ToolStripSeparator());

        var exit = new ToolStripMenuItem("Salir");
        exit.Click += (_, _) => Close();
        menu.Items.Add(exit);

        WindowChrome.ApplyDarkMenu(menu.Items);

        var tray = new NotifyIcon
        {
            Icon = _appIcon ?? SystemIcons.Application,
            Text = "ATS Telemetry",
            ContextMenuStrip = menu,
            Visible = true
        };

        return tray;
    }

    private void RebuildScreenMenu()
    {
        if (_screenMenu == null)
        {
            return;
        }

        _screenMenu.DropDownItems.Clear();
        var current = CurrentScreen();

        foreach (var screen in Screen.AllScreens)
        {
            var friendly = GetMonitorFriendlyName(screen.DeviceName);
            if (string.IsNullOrWhiteSpace(friendly)
                || friendly.Contains("Generic PnP", StringComparison.OrdinalIgnoreCase))
            {
                friendly = null;
            }

            var device = screen.DeviceName.Replace(@"\\.\", string.Empty);
            var label = friendly == null
                ? $"{device}  ·  {screen.Bounds.Width}×{screen.Bounds.Height}"
                : $"{friendly}  ·  {device}  ·  {screen.Bounds.Width}×{screen.Bounds.Height}";

            if (screen.Primary)
            {
                label += "  ·  Principal";
            }

            var target = screen;
            var item = new ToolStripMenuItem(label)
            {
                Checked = screen.DeviceName.Equals(current.DeviceName, StringComparison.OrdinalIgnoreCase),
                CheckOnClick = false
            };
            item.Click += (_, _) => MoveToScreen(target);
            _screenMenu.DropDownItems.Add(item);
        }

        if (_screenMenu.DropDownItems.Count == 0)
        {
            _screenMenu.DropDownItems.Add(new ToolStripMenuItem("No se detectaron pantallas")
            {
                Enabled = false
            });
        }

        WindowChrome.ApplyDarkMenu(_screenMenu.DropDownItems);
    }

    private void UpdateWindowMenu()
    {
        if (_windowedItem != null)
        {
            _windowedItem.Checked = _windowed;
        }
    }

    private static Icon? LoadAppIcon()
    {
        try
        {
            var path = Application.ExecutablePath;
            return string.IsNullOrWhiteSpace(path) ? null : Icon.ExtractAssociatedIcon(path);
        }
        catch
        {
            return null;
        }
    }

    private void AddLayoutItem(ToolStripMenuItem parent, string label, LayoutMode mode)
    {
        var item = new ToolStripMenuItem(label)
        {
            Checked = _preferences.Layout == mode,
            CheckOnClick = false,
            Tag = mode
        };

        item.Click += (_, _) => SetLayout(mode);
        parent.DropDownItems.Add(item);
    }

    private static Screen? FindTargetScreen(string? target)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            return null;
        }

        foreach (var screen in Screen.AllScreens)
        {
            var friendly = GetMonitorFriendlyName(screen.DeviceName);
            if (screen.DeviceName.Contains(target, StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrWhiteSpace(friendly)
                    && friendly.Contains(target, StringComparison.OrdinalIgnoreCase)))
            {
                return screen;
            }
        }

        return null;
    }

    private static string? GetMonitorFriendlyName(string deviceName)
    {
        try
        {
            var device = new DisplayDevice { cb = Marshal.SizeOf<DisplayDevice>() };
            if (!EnumDisplayDevices(deviceName, 0, ref device, 0))
            {
                return null;
            }

            // EnumDisplayDevices returns "Generic PnP Monitor" for most panels.
            // The real model name (for example "PM1562") is the EDID descriptor.
            return ReadEdidName(device.DeviceID) ?? device.DeviceString?.Trim();
        }
        catch
        {
            return null;
        }
    }

    private static string? ReadEdidName(string? deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return null;
        }

        // DeviceID: MONITOR\XYM2561\{class-guid}\0004
        var parts = deviceId.Split('\\');
        if (parts.Length < 4)
        {
            return null;
        }

        var hardwareId = parts[1];
        var instanceSuffix = parts[3];
        var basePath = $@"SYSTEM\CurrentControlSet\Enum\DISPLAY\{hardwareId}";

        try
        {
            using var baseKey = Registry.LocalMachine.OpenSubKey(basePath);
            if (baseKey == null)
            {
                return null;
            }

            foreach (var instanceName in baseKey.GetSubKeyNames())
            {
                using var instanceKey = baseKey.OpenSubKey(instanceName);
                var driver = instanceKey?.GetValue("Driver") as string;

                if (driver == null
                    || !driver.EndsWith("\\" + instanceSuffix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                using var parameters = instanceKey!.OpenSubKey("Device Parameters");
                if (parameters?.GetValue("EDID") is not byte[] edid || edid.Length < 128)
                {
                    return null;
                }

                for (var offset = 54; offset + 18 <= 126; offset += 18)
                {
                    if (edid[offset] != 0 || edid[offset + 1] != 0
                        || edid[offset + 2] != 0 || edid[offset + 3] != 0xFC)
                    {
                        continue;
                    }

                    var name = Encoding.ASCII
                        .GetString(edid, offset + 5, 13)
                        .Trim('\0', ' ', '\r', '\n');

                    if (name.Length > 0)
                    {
                        return name;
                    }
                }

                return null;
            }
        }
        catch
        {
            // Registry entries can be restricted on some systems.
        }

        return null;
    }

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool EnumDisplayDevices(
        string? lpDevice,
        uint iDevNum,
        ref DisplayDevice lpDisplayDevice,
        uint dwFlags);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DisplayDevice
    {
        public int cb;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceString;

        public int StateFlags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceID;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceKey;
    }
}
