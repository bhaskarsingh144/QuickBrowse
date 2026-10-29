using System;
using System.Drawing;

/// <summary>
/// Typography helpers inspired by QuickView’s sharp metric/network text:
/// prefer Semibold faces, keep point sizes on a half-point grid, never below a readable floor.
/// </summary>
internal static class ChromeFonts
{
    public static float SnapPt(float pt)
    {
        if (pt < ChromeLayout.MetricMinPt)
            pt = ChromeLayout.MetricMinPt;
        // Half-point grid avoids blurry fractional ClearType sizes on high-DPI panels.
        return (float)(Math.Round(pt * 2.0) / 2.0);
    }

    public static Font CreateUi(float basePt, float fontEm)
    {
        return CreateFace(SnapPt(ChromeLayout.PointSize(basePt, fontEm)), preferSemibold: true);
    }

    public static Font CreateLabel(float basePt, float fontEm)
    {
        return CreateFace(SnapPt(ChromeLayout.PointSize(basePt, fontEm)), preferSemibold: true);
    }

    /// <summary>QuickView-style metric face for any owner-drawn “hero” caption.</summary>
    public static Font CreateMetric(float basePt, float fontEm)
    {
        float em = SnapPt(ChromeLayout.PointSize(basePt, fontEm));
        string[] names = new string[] { "Bahnschrift SemiBold", "Bahnschrift", "Segoe UI Semibold", "Segoe UI" };
        for (int i = 0; i < names.Length; i++)
        {
            string name = names[i];
            FontStyle style = (name == "Bahnschrift" || name == "Segoe UI") ? FontStyle.Bold : FontStyle.Regular;
            try
            {
                Font font = new Font(name, em, style, GraphicsUnit.Point);
                string family = font.FontFamily.Name;
                if (family.IndexOf("Bahnschrift", StringComparison.OrdinalIgnoreCase) >= 0
                    || family.IndexOf("Segoe", StringComparison.OrdinalIgnoreCase) >= 0)
                    return font;
                font.Dispose();
            }
            catch
            {
            }
        }
        return new Font("Segoe UI", em, FontStyle.Bold, GraphicsUnit.Point);
    }

    private static Font CreateFace(float emSize, bool preferSemibold)
    {
        if (preferSemibold)
        {
            try
            {
                return new Font("Segoe UI Semibold", emSize, FontStyle.Regular, GraphicsUnit.Point);
            }
            catch
            {
            }
            try
            {
                return new Font("Segoe UI", emSize, FontStyle.Bold, GraphicsUnit.Point);
            }
            catch
            {
            }
        }
        return new Font("Segoe UI", emSize, FontStyle.Regular, GraphicsUnit.Point);
    }
}
