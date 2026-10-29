using System;
using System.Drawing;
using System.Windows.Forms;

/// <summary>
/// Label band must be tall enough for "Downloads" at common Fit×TextFit pairs.
/// </summary>
internal static class RailMetricsTests
{
    private static int _passed;
    private static int _failed;

    private static int Main()
    {
        float[][] pairs = new float[][]
        {
            new float[] { 1.00f, 1.00f },
            new float[] { 0.88f, 0.92f },
            new float[] { 0.78f, 0.90f },
            new float[] { 0.94f, 1.00f }
        };
        for (int i = 0; i < pairs.Length; i++)
        {
            float fit = pairs[i][0];
            float textFit = pairs[i][1];
            float dpi = 96f;
            using (Font font = new Font("Segoe UI",
                ChromeLayout.PointSize(ChromeLayout.RailLabelPt, ChromeLayout.FontEm(fit, textFit)),
                FontStyle.Regular, GraphicsUnit.Point))
            {
                RailCellMetrics m = ChromeLayout.MeasureRailCell(dpi, fit, font);
                Size need = TextRenderer.MeasureText(
                    "Downloads",
                    font,
                    new Size(int.MaxValue, int.MaxValue),
                    TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
                ExpectTrue(m.LabelBand >= need.Height,
                    "labelBand (" + m.LabelBand + ") >= MeasureText (" + need.Height + ") fit=" + fit);
                ExpectTrue(ChromeLayout.CellContentFits(m), "CellContentFits fit=" + fit);
            }
        }

        Console.WriteLine();
        Console.WriteLine("RailMetrics tests: {0} passed, {1} failed", _passed, _failed);
        return _failed == 0 ? 0 : 1;
    }

    private static void ExpectTrue(bool ok, string name)
    {
        if (ok)
        {
            _passed++;
            Console.WriteLine("  PASS  {0}", name);
        }
        else
        {
            _failed++;
            Console.WriteLine("  FAIL  {0}", name);
        }
    }
}
