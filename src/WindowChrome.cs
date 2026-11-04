using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ATSTelemetry;

/// <summary>
/// Integración mínima con DWM y con el dibujo de menús para que la barra de
/// título, las esquinas y la bandeja sigan el lenguaje visual oscuro.
/// </summary>
internal static class WindowChrome
{
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaUseImmersiveDarkModeBefore20H1 = 19;
    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwcpRound = 2;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd,
        int attribute,
        ref int value,
        int size);

    public static void ApplyDarkTitleBar(IntPtr handle)
    {
        ApplyTitleBar(handle, dark: true);
    }

    public static void ApplyTitleBar(IntPtr handle, bool dark)
    {
        if (handle == IntPtr.Zero)
        {
            return;
        }

        try
        {
            var value = dark ? 1 : 0;
            if (DwmSetWindowAttribute(handle, DwmwaUseImmersiveDarkMode, ref value, sizeof(int)) != 0)
            {
                DwmSetWindowAttribute(handle, DwmwaUseImmersiveDarkModeBefore20H1, ref value, sizeof(int));
            }

            var rounded = DwmwcpRound;
            DwmSetWindowAttribute(handle, DwmwaWindowCornerPreference, ref rounded, sizeof(int));
        }
        catch
        {
            // La aplicación debe funcionar aunque DWM no esté disponible.
        }
    }

    public static void ApplyDarkMenu(ContextMenuStrip menu)
    {
        ApplyDarkMenu((ToolStripDropDown)menu);
    }

    public static void ApplyDarkMenu(ToolStripDropDown menu)
    {
        menu.BackColor = Theme.SurfaceRaised;
        menu.ForeColor = Theme.TextPrimary;
        menu.Renderer = new ToolStripProfessionalRenderer(new DarkMenuColorTable())
        {
            RoundedEdges = false
        };
        ApplyDarkMenu(menu.Items);
    }

    /// <summary>
    /// Los submenús no heredan ForeColor del menú padre; se aplica a cada item
    /// para que "Diseño" y "Pantalla" no muestren texto negro.
    /// </summary>
    public static void ApplyDarkMenu(ToolStripItemCollection items)
    {
        foreach (ToolStripItem item in items)
        {
            item.ForeColor = Theme.TextPrimary;
            item.BackColor = Theme.SurfaceRaised;

            if (item is ToolStripMenuItem menuItem)
            {
                ApplyDarkMenu(menuItem.DropDown);
            }
        }
    }

    private sealed class DarkMenuColorTable : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => Theme.SurfaceRaised;
        public override Color MenuBorder => Theme.StrokeStrong;
        public override Color MenuItemBorder => Theme.StrokeStrong;
        public override Color MenuItemSelected => Theme.Surface;
        public override Color MenuItemSelectedGradientBegin => Theme.Surface;
        public override Color MenuItemSelectedGradientEnd => Theme.Surface;
        public override Color MenuItemPressedGradientBegin => Theme.SurfaceRaised;
        public override Color MenuItemPressedGradientEnd => Theme.SurfaceRaised;
        public override Color ImageMarginGradientBegin => Theme.SurfaceRaised;
        public override Color ImageMarginGradientMiddle => Theme.SurfaceRaised;
        public override Color ImageMarginGradientEnd => Theme.SurfaceRaised;
        public override Color SeparatorDark => Theme.StrokeStrong;
        public override Color SeparatorLight => Theme.Stroke;
        public override Color CheckBackground => Theme.Surface;
        public override Color CheckSelectedBackground => Theme.Surface;
        public override Color CheckPressedBackground => Theme.Surface;
    }
}
