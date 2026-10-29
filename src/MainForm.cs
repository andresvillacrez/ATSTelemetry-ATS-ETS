using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

namespace ATSTelemetry;

public sealed class MainForm : Form
{
    private const int WmFrame = 0x8000 + 1;

    private readonly TelemetryReader _reader;
    private DashboardControl _dashboard;
    private FrameClock _frameClock;
    private readonly NotifyIcon _tray;
    private readonly WindowPreferences _preferences;
    private readonly string? _screenTarget;
    private readonly bool _startWindowed;
    private ToolStripMenuItem? _screenMenu;
    private ToolStripMenuItem? _windowedItem;
    private ToolStripMenuItem? _weatherAutoItem;
    private readonly List<ToolStripMenuItem> _weatherItems = new();
    private bool _windowed;
    private bool _switchingMode;
    private string _displayName = string.Empty;
    private Icon? _appIcon;
    private int _frameQueued;

    public MainForm(string[] args)
    {
        var demo = args.Any(arg => arg.Equals("--demo", StringComparison.OrdinalIgnoreCase));
        _screenTarget = args.FirstOrDefault(arg => arg.StartsWith("--screen=", StringComparison.OrdinalIgnoreCase))
            ?.Substring("--screen=".Length);

        _preferences = WindowPreferences.Load();

        var hasExplicitScreen = !string.IsNullOrWhiteSpace(_screenTarget);
        var forcedWindowed = args.Any(arg =>
            arg.Equals("--windowed", StringComparison.OrdinalIgnoreCase)
            || arg.Equals("--window", StringComparison.OrdinalIgnoreCase));
        _startWindowed = forcedWindowed || (!hasExplicitScreen && _preferences.Windowed);

        _reader = new TelemetryReader(demo);
        _reader.Weather = _preferences.Weather;
        _reader.WeatherAuto = _preferences.WeatherAuto;
        _dashboard = new DashboardControl(_reader) { Dock = DockStyle.Fill };

        Text = "ATS Telemetry";
        _appIcon = LoadAppIcon();
        Icon = _appIcon;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.Black;
        ShowInTaskbar = false;
        TopMost = true;
        KeyPreview = true;
        AutoScaleMode = AutoScaleMode.None;
        DoubleBuffered = true;

        Controls.Add(_dashboard);

        ApplyStartupLayout();

        _frameClock = new FrameClock(60, RequestFrame);
        _frameClock.Start();

        _tray = BuildTrayIcon();

        KeyDown += (_, e) =>
        {
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
            if (!_windowed || WindowState != FormWindowState.Normal)
            {
                return;
            }

            var screen = CurrentScreen();
            _preferences.ScreenDevice = screen.DeviceName;
            UpdateScreenLabel(screen);
        };
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        SavePreferences();
        _frameClock.Dispose();
        _tray.Visible = false;
        _tray.Dispose();
        _reader.Dispose();
        base.OnFormClosing(e);
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

        _windowed = windowed;
        WindowState = FormWindowState.Normal;
        FormBorderStyle = targetBorder;
        ShowInTaskbar = windowed;
        MaximizeBox = windowed;
        MinimizeBox = windowed;
        TopMost = true;

        if (windowed)
        {
            var area = screen.WorkingArea;
            var width = Math.Min(1280, Math.Max(480, (int)(area.Width * 0.9)));
            var height = Math.Min(720, Math.Max(320, (int)(area.Height * 0.9)));

            MinimumSize = new Size(Math.Min(640, width), Math.Min(360, height));
            Bounds = new Rectangle(
                area.Left + (area.Width - width) / 2,
                area.Top + (area.Height - height) / 2,
                width,
                height);
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
            DisplayName = _displayName
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
        _preferences.Windowed = _windowed;
        _preferences.Save();
    }

    private void OnDisplayChanged()
    {
        if (IsDisposed || !IsHandleCreated)
        {
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

        menu.Items.Add(new ToolStripSeparator());

        var weatherMenu = new ToolStripMenuItem("Clima");

        _weatherAutoItem = new ToolStripMenuItem("Automático (juego)")
        {
            Checked = _preferences.WeatherAuto,
            CheckOnClick = false
        };
        _weatherAutoItem.Click += (_, _) => UseAutomaticWeather();
        weatherMenu.DropDownItems.Add(_weatherAutoItem);
        weatherMenu.DropDownItems.Add(new ToolStripSeparator());

        AddWeatherItem(weatherMenu, "Despejado", WeatherCondition.Clear);
        AddWeatherItem(weatherMenu, "Nublado", WeatherCondition.Cloudy);
        AddWeatherItem(weatherMenu, "Lluvia", WeatherCondition.Rain);
        AddWeatherItem(weatherMenu, "Tormenta", WeatherCondition.Storm);

        menu.Items.Add(weatherMenu);
        menu.Items.Add(new ToolStripSeparator());

        var exit = new ToolStripMenuItem("Salir");
        exit.Click += (_, _) => Close();
        menu.Items.Add(exit);

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

    private void AddWeatherItem(ToolStripMenuItem parent, string label, WeatherCondition condition)
    {
        var item = new ToolStripMenuItem(label)
        {
            Checked = !_preferences.WeatherAuto && condition == _preferences.Weather,
            CheckOnClick = false,
            Tag = condition
        };

        item.Click += (_, _) =>
        {
            _reader.WeatherAuto = false;
            _reader.Weather = condition;
            _preferences.WeatherAuto = false;
            _preferences.Weather = condition;
            SavePreferences();
            UpdateWeatherChecks();
        };

        _weatherItems.Add(item);
        parent.DropDownItems.Add(item);
    }

    private void UseAutomaticWeather()
    {
        _reader.WeatherAuto = true;
        _preferences.WeatherAuto = true;
        SavePreferences();
        UpdateWeatherChecks();
    }

    private void UpdateWeatherChecks()
    {
        if (_weatherAutoItem != null)
        {
            _weatherAutoItem.Checked = _preferences.WeatherAuto;
        }

        foreach (var item in _weatherItems)
        {
            item.Checked = !_preferences.WeatherAuto
                && item.Tag is WeatherCondition condition
                && condition == _preferences.Weather;
        }
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
