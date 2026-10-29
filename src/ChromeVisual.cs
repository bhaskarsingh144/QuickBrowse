using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

/// <summary>
/// Win11 / QuickView-style rounded chrome helpers.
/// </summary>
internal static class ChromeVisual
{
    // Must stay 1.0 — layered Opacity below 1 blurs ClearType (classic WinForms issue).
    // QuickView dims only during brightness preview; resting chrome stays opaque for sharp text.
    public const double PanelOpacity = 1.0;
    public const int CornerDip = 16;
    public const int TileCornerDip = 10;
    public const int ChipCornerDip = 8;

    public static GraphicsPath Round(Rectangle r, int radius)
    {
        GraphicsPath path = new GraphicsPath();
        int d = Math.Max(2, radius * 2);
        if (d > r.Width) d = Math.Max(1, r.Width);
        if (d > r.Height) d = Math.Max(1, r.Height);
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    public static void ApplyRoundedRegion(Form form, int radiusPx)
    {
        if (form == null || form.Width < 4 || form.Height < 4)
            return;
        using (GraphicsPath path = Round(new Rectangle(0, 0, form.Width, form.Height), radiusPx))
        {
            Region old = form.Region;
            form.Region = new Region(path);
            if (old != null)
                old.Dispose();
        }
    }

    public static void FillRound(Graphics g, Rectangle rect, int radius, Color color)
    {
        using (GraphicsPath path = Round(rect, radius))
        using (SolidBrush brush = new SolidBrush(color))
            g.FillPath(brush, path);
    }

    public static void DrawRoundBorder(Graphics g, Rectangle rect, int radius, Color color)
    {
        Rectangle r = rect;
        if (r.Width < 2 || r.Height < 2)
            return;
        r.Width -= 1;
        r.Height -= 1;
        using (GraphicsPath path = Round(r, radius))
        using (Pen pen = new Pen(color))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.DrawPath(pen, path);
        }
    }

    public static void PreferDarkMode(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
            return;
        try
        {
            int useDark = 1;
            // DWMWA_USE_IMMERSIVE_DARK_MODE = 20 (Win10 1903+ / Win11)
            DwmSetWindowAttribute(hwnd, 20, ref useDark, 4);
            // DWMWA_WINDOW_CORNER_PREFERENCE = 33, DWMWCP_ROUND = 2
            int corner = 2;
            DwmSetWindowAttribute(hwnd, 33, ref corner, 4);
        }
        catch
        {
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
}
