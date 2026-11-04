using System.Diagnostics;
using System.Drawing;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace ATSTelemetry.Setup;

internal sealed class SetupForm : Form
{
    private static readonly Color Background = Color.FromArgb(10, 12, 15);
    private static readonly Color Surface = Color.FromArgb(18, 22, 26);
    private static readonly Color SurfaceRaised = Color.FromArgb(26, 31, 36);
    private static readonly Color TextPrimary = Color.FromArgb(244, 247, 249);
    private static readonly Color TextSecondary = Color.FromArgb(168, 178, 186);
    private static readonly Color TextTertiary = Color.FromArgb(122, 133, 141);
    private static readonly Color Accent = Color.FromArgb(64, 209, 255);

    private readonly TextBox _path;
    private readonly InstallerCheckBox _desktopShortcut;
    private readonly InstallerCheckBox _startMenuShortcut;
    private readonly InstallerCheckBox _installPlugin;
    private readonly InstallerCheckBox _launchApp;
    private readonly Button _install;
    private readonly Button _cancel;
    private readonly ProgressBar _progress;
    private readonly Label _status;

    public SetupForm()
    {
        Text = "Instalador de ATS Telemetry";
        ClientSize = new Size(620, 520);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = Background;
        ForeColor = TextPrimary;
        Font = new Font("Segoe UI", 9.5f);
        AutoScaleMode = AutoScaleMode.Dpi;

        try
        {
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        }
        catch
        {
            // Sin icono si el ejecutable no lo expone.
        }

        var title = new Label
        {
            Text = "ATS Telemetry",
            Font = new Font("Segoe UI Semibold", 22f),
            ForeColor = TextPrimary,
            AutoSize = true,
            Location = new Point(28, 24)
        };
        Controls.Add(title);

        var subtitle = new Label
        {
            Text = "Instalador · telemetría para ATS y ETS2",
            Font = new Font("Segoe UI", 10f),
            ForeColor = TextSecondary,
            AutoSize = true,
            Location = new Point(30, 62)
        };
        Controls.Add(subtitle);

        var accent = new Panel
        {
            BackColor = Accent,
            Location = new Point(28, 92),
            Size = new Size(564, 2)
        };
        Controls.Add(accent);

        AddLabel("Carpeta de instalación", 28, 116);

        _path = new TextBox
        {
            Text = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs",
                "ATS Telemetry"),
            BackColor = SurfaceRaised,
            ForeColor = TextPrimary,
            BorderStyle = BorderStyle.FixedSingle,
            Location = new Point(28, 140),
            Size = new Size(450, 28)
        };
        Controls.Add(_path);

        var browse = new Button
        {
            Text = "Examinar…",
            FlatStyle = FlatStyle.Flat,
            BackColor = SurfaceRaised,
            ForeColor = TextPrimary,
            Location = new Point(486, 139),
            Size = new Size(106, 28)
        };
        browse.FlatAppearance.BorderColor = Color.FromArgb(54, 62, 70);
        browse.Click += (_, _) => BrowseFolder();
        Controls.Add(browse);

        var options = new Label
        {
            Text = "OPCIONES",
            Font = new Font("Segoe UI Semibold", 8.5f),
            ForeColor = TextTertiary,
            AutoSize = true,
            Location = new Point(28, 188)
        };
        Controls.Add(options);

        _desktopShortcut = AddCheck("Crear acceso directo en el escritorio", 28, 214, true);
        _startMenuShortcut = AddCheck("Crear acceso directo en el menú Inicio", 28, 244, true);
        _installPlugin = AddCheck("Instalar el plugin de telemetría en ATS/ETS2", 28, 274, true);
        _launchApp = AddCheck("Abrir ATS Telemetry al terminar", 28, 304, true);

        var note = new Label
        {
            Text = "Se copiarán la aplicación, sus recursos y scs-telemetry.dll.\n" +
                   "El plugin se instala en las carpetas de ATS/ETS2 detectadas por Steam.",
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = TextTertiary,
            AutoSize = false,
            Location = new Point(28, 340),
            Size = new Size(564, 40)
        };
        Controls.Add(note);

        _progress = new ProgressBar
        {
            Location = new Point(28, 398),
            Size = new Size(564, 14),
            Style = ProgressBarStyle.Continuous
        };
        Controls.Add(_progress);

        _status = new Label
        {
            Text = "Listo para instalar.",
            Font = new Font("Segoe UI", 9f),
            ForeColor = TextSecondary,
            AutoSize = false,
            Location = new Point(28, 420),
            Size = new Size(564, 22)
        };
        Controls.Add(_status);

        _install = new Button
        {
            Text = "Instalar",
            FlatStyle = FlatStyle.Flat,
            BackColor = SurfaceRaised,
            ForeColor = TextPrimary,
            Location = new Point(386, 462),
            Size = new Size(100, 34)
        };
        _install.FlatAppearance.BorderColor = Accent;
        _install.Click += async (_, _) => await InstallAsync();
        Controls.Add(_install);

        _cancel = new Button
        {
            Text = "Cancelar",
            FlatStyle = FlatStyle.Flat,
            BackColor = SurfaceRaised,
            ForeColor = TextSecondary,
            Location = new Point(492, 462),
            Size = new Size(100, 34),
            DialogResult = DialogResult.Cancel
        };
        _cancel.FlatAppearance.BorderColor = Color.FromArgb(54, 62, 70);
        Controls.Add(_cancel);

        AcceptButton = _install;
        CancelButton = _cancel;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ApplyDarkTitleBar(Handle);
    }

    private void BrowseFolder()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Selecciona la carpeta de instalación",
            UseDescriptionForTitle = true,
            SelectedPath = _path.Text
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _path.Text = Path.Combine(dialog.SelectedPath, "ATS Telemetry");
        }
    }

    private async Task InstallAsync()
    {
        var target = _path.Text.Trim();
        if (string.IsNullOrWhiteSpace(target))
        {
            ShowError("Indica una carpeta de instalación.");
            return;
        }

        try
        {
            target = Path.GetFullPath(target);
        }
        catch (Exception ex)
        {
            ShowError($"La carpeta no es válida: {ex.Message}");
            return;
        }

        if (Process.GetProcessesByName("ATSTelemetry").Length > 0)
        {
            ShowError("ATS Telemetry está abierto. Ciérralo antes de instalar o actualizar.");
            return;
        }

        SetBusy(true);
        _progress.Value = 0;

        try
        {
            Directory.CreateDirectory(target);

            var payload = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("payload.zip");
            if (payload == null)
            {
                throw new InvalidOperationException(
                    "El instalador no contiene el paquete de la aplicación.");
            }

            _status.Text = "Copiando archivos…";
            var progress = new Progress<int>(value => _progress.Value = Math.Clamp(value, 0, 100));
            await Task.Run(() => ExtractPayload(payload, target, progress));

            _progress.Value = 70;
            _status.Text = "Creando accesos directos…";
            var exePath = Path.Combine(target, "ATSTelemetry.exe");
            var iconPath = Path.Combine(target, "ATSTelemetry.ico");
            var uninstallCmd = Path.Combine(target, "uninstall.cmd");
            CreateShortcuts(target, exePath, iconPath, uninstallCmd);

            _progress.Value = 82;
            _status.Text = "Registrando la aplicación…";
            WriteUninstaller(target);
            RegisterUninstall(target, exePath, uninstallCmd);

            var pluginOk = false;
            if (_installPlugin.Checked)
            {
                _progress.Value = 90;
                _status.Text = "Instalando el plugin de telemetría…";
                pluginOk = await Task.Run(() => InstallPlugin(target));
            }

            _progress.Value = 100;
            _status.Text = "Instalación completada.";

            var summary = "ATS Telemetry se instaló correctamente.\n\n" +
                          $"Carpeta: {target}\n" +
                          "Accesos directos: escritorio y menú Inicio.";
            if (_installPlugin.Checked)
            {
                summary += pluginOk
                    ? "\nPlugin de telemetría: instalador ejecutado. " +
                      "Si Windows pidió permisos, acéptalos para completar la copia."
                    : "\nPlugin de telemetría: no se pudo ejecutar el instalador. " +
                      "Puedes instalarlo luego con ‘Instalar plugin en ATS y ETS2.bat’.";
            }

            MessageBox.Show(this, summary, "ATS Telemetry",
                MessageBoxButtons.OK, MessageBoxIcon.Information);

            if (_launchApp.Checked)
            {
                Process.Start(new ProcessStartInfo(exePath)
                {
                    UseShellExecute = true,
                    WorkingDirectory = target
                });
            }

            Close();
        }
        catch (Exception ex)
        {
            _progress.Value = 0;
            _status.Text = "La instalación no se completó.";
            ShowError($"No se pudo instalar ATS Telemetry:\n\n{ex.Message}");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private static void ExtractPayload(
        Stream payload,
        string target,
        IProgress<int> progress)
    {
        using var archive = new ZipArchive(payload, ZipArchiveMode.Read, leaveOpen: false);
        var entries = archive.Entries.Count;
        var root = Path.GetFullPath(target)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        for (var i = 0; i < archive.Entries.Count; i++)
        {
            var entry = archive.Entries[i];
            if (string.IsNullOrEmpty(entry.Name))
            {
                continue;
            }

            var destination = Path.GetFullPath(Path.Combine(target, entry.FullName));
            if (!destination.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "El paquete contiene una ruta no válida.");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            entry.ExtractToFile(destination, overwrite: true);
            progress.Report((int)((i + 1) * 68.0 / Math.Max(1, entries)));
        }
    }

    private void CreateShortcuts(
        string target, string exePath, string iconPath, string uninstallCmd)
    {
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        if (_desktopShortcut.Checked)
        {
            CreateShortcut(
                Path.Combine(desktop, "ATS Telemetry.lnk"),
                exePath, target, iconPath, "ATS Telemetry");
        }

        if (_startMenuShortcut.Checked)
        {
            var startMenu = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Programs),
                "ATS Telemetry");
            Directory.CreateDirectory(startMenu);

            CreateShortcut(
                Path.Combine(startMenu, "ATS Telemetry.lnk"),
                exePath, target, iconPath, "ATS Telemetry");

            CreateShortcut(
                Path.Combine(startMenu, "Desinstalar ATS Telemetry.lnk"),
                uninstallCmd, target, iconPath, "Desinstalar ATS Telemetry");
        }
    }

    private static void CreateShortcut(
        string shortcutPath,
        string targetPath,
        string workingDirectory,
        string iconPath,
        string description)
    {
        var shellType = Type.GetTypeFromProgID("WScript.Shell")
            ?? throw new InvalidOperationException("No se pudo acceder a WScript.Shell.");

        dynamic shell = Activator.CreateInstance(shellType)
            ?? throw new InvalidOperationException("No se pudo crear WScript.Shell.");
        dynamic shortcut = shell.CreateShortcut(shortcutPath);
        try
        {
            shortcut.TargetPath = targetPath;
            shortcut.WorkingDirectory = workingDirectory;
            shortcut.IconLocation = iconPath;
            shortcut.Description = description;
            shortcut.Save();
        }
        finally
        {
            Marshal.FinalReleaseComObject(shortcut);
            Marshal.FinalReleaseComObject(shell);
        }
    }

    private static void WriteUninstaller(string target)
    {
        var escapedTarget = target.Replace("'", "''");
        var script = $$"""
            $ErrorActionPreference = "SilentlyContinue"
            $target = '{{escapedTarget}}'
            $desktop = Join-Path ([Environment]::GetFolderPath('DesktopDirectory')) 'ATS Telemetry.lnk'
            $startMenu = Join-Path ([Environment]::GetFolderPath('Programs')) 'ATS Telemetry'
            Remove-Item -LiteralPath $desktop -Force
            Remove-Item -LiteralPath $startMenu -Recurse -Force
            Remove-Item -LiteralPath 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\ATSTelemetry' -Recurse -Force
            Start-Process -FilePath 'cmd.exe' -ArgumentList "/c timeout /t 2 >nul & rmdir /s /q `"$target`"" -WindowStyle Hidden
            """;

        File.WriteAllText(Path.Combine(target, "uninstall.ps1"), script);
        File.WriteAllText(
            Path.Combine(target, "uninstall.cmd"),
            "@echo off\r\npowershell -NoProfile -ExecutionPolicy Bypass -File \"%~dp0uninstall.ps1\"\r\n");
    }

    private static void RegisterUninstall(
        string target, string exePath, string uninstallCmd)
    {
        using var key = Registry.CurrentUser.CreateSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Uninstall\ATSTelemetry");
        if (key == null)
        {
            return;
        }

        var version = FileVersionInfo.GetVersionInfo(exePath).FileVersion ?? "1.0.0";
        key.SetValue("DisplayName", "ATS Telemetry");
        key.SetValue("DisplayVersion", version);
        key.SetValue("Publisher", "ATS Telemetry");
        key.SetValue("InstallLocation", target);
        key.SetValue("DisplayIcon", exePath);
        key.SetValue("UninstallString", $"\"{uninstallCmd}\"");
        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
    }

    private static bool InstallPlugin(string target)
    {
        var script = Path.Combine(target, "install-plugin.ps1");
        if (!File.Exists(script))
        {
            return false;
        }

        var start = new ProcessStartInfo("powershell.exe")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = target,
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{script}\""
        };

        using var process = Process.Start(start);
        if (process == null)
        {
            return false;
        }

        process.StandardOutput.ReadToEnd();
        process.StandardError.ReadToEnd();
        process.WaitForExit();
        return process.ExitCode == 0;
    }

    private void SetBusy(bool busy)
    {
        _path.Enabled = !busy;
        _desktopShortcut.Enabled = !busy;
        _startMenuShortcut.Enabled = !busy;
        _installPlugin.Enabled = !busy;
        _launchApp.Enabled = !busy;
        _install.Enabled = !busy;
        _cancel.Enabled = !busy;
        Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
    }

    private void ShowError(string message)
    {
        MessageBox.Show(this, message, "ATS Telemetry",
            MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private void AddLabel(string text, int x, int y)
    {
        var label = new Label
        {
            Text = text,
            ForeColor = TextSecondary,
            AutoSize = true,
            Location = new Point(x, y)
        };
        Controls.Add(label);
    }

    private InstallerCheckBox AddCheck(string text, int x, int y, bool isChecked)
    {
        var check = new InstallerCheckBox
        {
            Text = text,
            Checked = isChecked,
            Location = new Point(x, y),
            Size = new Size(564, 24)
        };
        Controls.Add(check);
        return check;
    }

    private static void ApplyDarkTitleBar(IntPtr handle)
    {
        const int attribute = 20;
        var value = 1;
        try
        {
            DwmSetWindowAttribute(handle, attribute, ref value, sizeof(int));
        }
        catch
        {
            // DWM no disponible.
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd, int attribute, ref int value, int size);
}