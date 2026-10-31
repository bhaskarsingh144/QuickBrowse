using System;
using System.Drawing;
using System.Windows.Forms;

internal static class ChromeLayoutTests
{
    private static int _passed;
    private static int _failed;

    private static int Main()
    {
        CellContentFitsAtScales();
        ChipsDoNotOverlap();
        RailAndPaneInsideWork();
        PaneLargerThanRailWithGap();
        CenterNearFraction();
        PaneClearsSiblingStrips();
        CtxMetricsScaleWithDpi();

        Console.WriteLine();
        Console.WriteLine("ChromeLayout tests: {0} passed, {1} failed", _passed, _failed);
        return _failed == 0 ? 0 : 1;
    }

    private static void CtxMetricsScaleWithDpi()
    {
        int baseBtn = ChromeLayout.CtxIconBtnPx(96f, 1f);
        int hiBtn = ChromeLayout.CtxIconBtnPx(192f, 1f);
        ExpectTrue(baseBtn == ChromeLayout.CtxIconBtnDip, "ctx btn @96 = dip");
        ExpectTrue(hiBtn > baseBtn, "ctx btn grows at 200% dpi");
        int baseGlyph = ChromeLayout.CtxGlyphPx(96f, 1f);
        int hiGlyph = ChromeLayout.CtxGlyphPx(144f, 1f);
        ExpectTrue(hiGlyph > baseGlyph, "ctx glyph grows at 150% dpi");
        ExpectTrue(ChromeLayout.CtxRowMinPx(96f, 1f) == ChromeLayout.CtxRowMinDip, "ctx row @96");
        ExpectTrue(ChromeLayout.CtxRowMinPx(144f, 1f) > ChromeLayout.CtxRowMinDip, "ctx row grows");
    }

    private static void CellContentFitsAtScales()
    {
        float[][] scales = new float[][]
        {
            new float[] { 96f, 1f, 1f },
            new float[] { 120f, 0.88f, 0.92f },
            new float[] { 144f, 0.78f, 0.90f },
            new float[] { 192f, 0.88f, 1f }
        };
        for (int i = 0; i < scales.Length; i++)
        {
            float dpi = scales[i][0];
            float fit = scales[i][1];
            float textFit = scales[i][2];
            using (Font font = MakeLabelFont(fit, textFit))
            {
                RailCellMetrics m = ChromeLayout.MeasureRailCell(dpi, fit, font);
                ExpectTrue(ChromeLayout.CellContentFits(m),
                    "content fits dpi=" + dpi + " fit=" + fit);
                ExpectTrue(m.InterChipGap >= 1, "inter-chip gap >= 1");
            }
        }
    }

    private static void ChipsDoNotOverlap()
    {
        float dpi = 96f;
        float fit = 1f;
        using (Font font = MakeLabelFont(fit, 1f))
        {
            RailCellMetrics m = ChromeLayout.MeasureRailCell(dpi, fit, font);
            int pad = DisplayScale.Px(ChromeLayout.RailPadDip, dpi, fit);
            int y0 = pad;
            int y1 = pad + m.RowHeight;
            Rectangle chip0 = ChipRect(0, y0, 96, m);
            Rectangle chip1 = ChipRect(0, y1, 96, m);
            ExpectTrue(chip0.Bottom <= chip1.Top,
                "chip0.Bottom (" + chip0.Bottom + ") <= chip1.Top (" + chip1.Top + ")");
        }
    }

    private static void RailAndPaneInsideWork()
    {
        Rectangle work = new Rectangle(0, 40, 1920, 1000);
        float dpi = 96f;
        float fit = 1f;
        using (Font font = MakeLabelFont(fit, 1f))
        {
            RailCellMetrics m = ChromeLayout.MeasureRailCell(dpi, fit, font);
            int railW = DisplayScale.Px(ChromeLayout.RailWidthDip, dpi, fit);
            int railH = ChromeLayout.RailHeightPx(m, 8, dpi, fit);
            Rectangle rail = ChromeLayout.RailHome(dpi, fit, railW, railH, work);
            Rectangle pane = ChromeLayout.PaneHome(dpi, fit, rail, work);
            ExpectTrue(work.Contains(rail) || RectMostlyInside(work, rail), "rail inside work");
            ExpectTrue(work.Contains(pane) || RectMostlyInside(work, pane), "pane inside work");
        }
    }

    private static void PaneLargerThanRailWithGap()
    {
        Rectangle work = new Rectangle(0, 0, 1920, 1080);
        float dpi = 96f;
        float fit = 1f;
        using (Font font = MakeLabelFont(fit, 1f))
        {
            RailCellMetrics m = ChromeLayout.MeasureRailCell(dpi, fit, font);
            int railW = DisplayScale.Px(ChromeLayout.RailWidthDip, dpi, fit);
            int railH = ChromeLayout.RailHeightPx(m, 8, dpi, fit);
            Rectangle rail = ChromeLayout.RailHome(dpi, fit, railW, railH, work);
            Rectangle pane = ChromeLayout.PaneHome(dpi, fit, rail, work);
            ExpectTrue(pane.Width > rail.Width, "pane wider than rail");
            ExpectTrue(pane.Height >= rail.Height, "pane taller or equal rail");
            int gap = pane.Left - rail.Right;
            int minGap = DisplayScale.Px(ChromeLayout.PaneGapDip, dpi, fit);
            ExpectTrue(gap >= minGap - 1, "gap >= PaneGapDip (" + gap + " vs " + minGap + ")");
        }
    }

    private static void CenterNearFraction()
    {
        Rectangle work = new Rectangle(0, 0, 1920, 1000);
        float dpi = 96f;
        float fit = 1f;
        using (Font font = MakeLabelFont(fit, 1f))
        {
            RailCellMetrics m = ChromeLayout.MeasureRailCell(dpi, fit, font);
            int railW = DisplayScale.Px(ChromeLayout.RailWidthDip, dpi, fit);
            int railH = ChromeLayout.RailHeightPx(m, 8, dpi, fit);
            Rectangle rail = ChromeLayout.RailHome(dpi, fit, railW, railH, work);
            int center = rail.Top + rail.Height / 2;
            int expect = work.Top + (int)(work.Height * ChromeLayout.RailCenterYFraction);
            int delta = Math.Abs(center - expect);
            ExpectTrue(delta <= rail.Height / 2 + 8, "rail center near 0.52 (delta=" + delta + ")");
        }
    }

    private static void PaneClearsSiblingStrips()
    {
        Rectangle work = new Rectangle(0, 0, 1920, 1080);
        float dpi = 96f;
        float fit = 1f;
        using (Font font = MakeLabelFont(fit, 1f))
        {
            RailCellMetrics m = ChromeLayout.MeasureRailCell(dpi, fit, font);
            int railW = DisplayScale.Px(ChromeLayout.RailWidthDip, dpi, fit);
            int railH = ChromeLayout.RailHeightPx(m, 10, dpi, fit);
            Rectangle rail = ChromeLayout.RailHome(dpi, fit, railW, railH, work);
            Rectangle pane = ChromeLayout.PaneHome(dpi, fit, rail, work);
            int topStrip = DisplayScale.Px(24, dpi, fit);
            int rightStrip = DisplayScale.Px(ChromeLayout.EdgeClearDip, dpi, fit);
            ExpectTrue(
                ChromeLayout.PaneClearsSiblingStrips(pane, work, topStrip, rightStrip),
                "pane clears top/right sibling strips");
            ExpectTrue(pane.Right <= work.Right - rightStrip + 1,
                "pane.Right clears EdgeClearDip");
        }
    }

    private static Rectangle ChipRect(int x, int y, int width, RailCellMetrics m)
    {
        Rectangle hit = new Rectangle(x, y, width, m.RowHeight);
        return Rectangle.Inflate(hit, -m.ChipInsetX, -m.ChipInsetY);
    }

    private static bool RectMostlyInside(Rectangle outer, Rectangle inner)
    {
        return inner.Left >= outer.Left - 2
            && inner.Top >= outer.Top - 2
            && inner.Right <= outer.Right + 2
            && inner.Bottom <= outer.Bottom + 2;
    }

    private static Font MakeLabelFont(float fit, float textFit)
    {
        float em = ChromeLayout.FontEm(fit, textFit);
        return new Font("Segoe UI", ChromeLayout.PointSize(ChromeLayout.RailLabelPt, em), FontStyle.Regular, GraphicsUnit.Point);
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
