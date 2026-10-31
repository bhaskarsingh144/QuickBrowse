using System;
using System.Drawing;
using System.Windows.Forms;

/// <summary>
/// Pure geometry + font math. Rail cells are measure-driven so labels never clip.
/// </summary>
internal struct RailCellMetrics
{
    public int RowHeight;
    public int IconSize;
    public int LabelBand;
    public int ChipInsetX;
    public int ChipInsetY;
    public int ContentTopPad;
    public int IconLabelGap;
    public int ContentBottomPad;
    public int InterChipGap;
}

internal static class ChromeLayout
{
    public const int RailWidthDip = 104;
    public const int RailPadDip = 10;
    public const int RailLeftInsetDip = 10;
    public const int RailIconDip = 28;
    public const int RailMinRowDip = 52;

    // ~30% wider than the prior 840 DIP pane so path + nav chrome fit comfortably.
    public const int PaneWidthDip = 1092;
    public const int PaneHeightDip = 860;
    public const int PaneGapDip = 16;

    public const int TopClearDip = 56;
    public const int BottomClearDip = 24;
    // Keep pane/flyout out of QuickView's ~24px right strip (was only 12px).
    public const int EdgeClearDip = 32;

    public const double RailCenterYFraction = 0.52;

    // Rail place labels — Semibold + a bit larger so icon strip reads at a glance.
    public const float RailLabelPt = 11.5f;
    public const float UiPt = 11.5f;
    public const float TitlePt = 12f;
    public const float MetricMinPt = 9f;

    // Two-row pane chrome: nav/path on top, organize + view below.
    public const int PaneChromeHeightDip = 116;
    public const int PaneChromeBtnDip = 40;
    public const int PaneChromeBtnWidthDip = 52;
    public const int PaneChromeIconDip = 26;
    /// <summary>Path field — slightly shorter than chips so it reads as a field, not a button.</summary>
    public const int PanePathHeightDip = 34;
    public const int PaneViewBtnWidthDip = 40;
    /// <summary>Single view-mode dropdown trigger (glyph + chevron).</summary>
    public const int PaneViewDropWidthDip = 56;
    public const int ViewMenuWidthDip = 200;
    /// <summary>Ideal width for the in-folder filter field on row 1.</summary>
    public const int PaneFilterSlotDip = 200;

    // Larger icon cells with room for 2-line labels.
    public const int TileIconDip = 68;
    public const int TileWidthDip = 148;
    public const int TileHeightDip = 140;

    // Win11 Content-style rows (icon left, name + meta right).
    public const int ContentRowWidthDip = 420;
    public const int ContentRowHeightDip = 72;
    public const int ContentIconDip = 48;

    // Pins flyout beside the rail — denser than rail chips so many pins fit.
    public const int PinsFlyoutWidthDip = 188;
    public const int PinsFlyoutGapDip = 10;
    public const int PinsFlyoutMaxVisible = 16;
    public const int PinsFlyoutRowDip = 36;
    public const int PinsFlyoutIconDip = 20;
    public const int PinsFlyoutPadDip = 8;

    // Recent panel occupies the browse-pane slot (same home as PaneHome).
    public const int RecentHeaderDip = 52;
    public const int RecentPadDip = 16;
    public const int RecentSectionGapDip = 18;
    public const int RecentSectionHeaderDip = 28;
    public const int RecentTileWidthDip = 120;
    public const int RecentTileHeightDip = 112;
    public const int RecentTileGapDip = 12;
    public const int RecentTileIconDip = 52;

    public static float FontEm(float fit, float textFit)
    {
        float t = fit * textFit;
        if (t < 0.55f)
            return 0.55f;
        return t;
    }

    public static float PointSize(float basePt, float fontEm)
    {
        float pt = basePt * fontEm;
        if (pt < MetricMinPt)
            pt = MetricMinPt;
        return (float)(Math.Round(pt * 2.0) / 2.0);
    }

    /// <summary>
    /// Build cell metrics from DPI + font. Label band is measured ("Downloads" sample) + slack.
    /// </summary>
    public static RailCellMetrics MeasureRailCell(float dpi, float fit, Font labelFont)
    {
        RailCellMetrics m = new RailCellMetrics();
        m.IconSize = DisplayScale.Px(RailIconDip, dpi, fit);
        m.ContentTopPad = DisplayScale.Px(6, dpi, fit);
        m.IconLabelGap = DisplayScale.Px(4, dpi, fit);
        m.ContentBottomPad = DisplayScale.Px(6, dpi, fit);
        m.InterChipGap = DisplayScale.Px(2, dpi, fit);
        m.ChipInsetX = DisplayScale.Px(6, dpi, fit);
        // Keep chip vertical inset small; InterChipGap provides separation so next chip
        // never paints over glyph descenders.
        m.ChipInsetY = Math.Max(1, m.InterChipGap / 2);

        int labelBand = DisplayScale.Px(14, dpi, fit);
        if (labelFont != null)
        {
            Size sz = TextRenderer.MeasureText(
                "Downloads",
                labelFont,
                new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
            labelBand = sz.Height + 2;
        }
        m.LabelBand = labelBand;

        int row = m.ContentTopPad + m.IconSize + m.IconLabelGap + m.LabelBand + m.ContentBottomPad;
        int minRow = DisplayScale.Px(RailMinRowDip, dpi, fit);
        if (row < minRow)
            row = minRow;
        m.RowHeight = row;
        return m;
    }

    /// <summary>Content stack must fit inside the row (unit-test contract).</summary>
    public static bool CellContentFits(RailCellMetrics m)
    {
        int need = m.ContentTopPad + m.IconSize + m.IconLabelGap + m.LabelBand + m.ContentBottomPad;
        return need <= m.RowHeight && m.LabelBand > 0 && m.IconSize > 0;
    }

    public static int RailHeightPx(RailCellMetrics cell, int placeCount, float dpi, float fit)
    {
        int pad = DisplayScale.Px(RailPadDip, dpi, fit);
        int h = pad * 2 + Math.Max(1, placeCount) * cell.RowHeight;
        int min = DisplayScale.Px(220, dpi, fit);
        return h < min ? min : h;
    }

    public static Rectangle RailHome(float dpi, float fit, int railW, int railH)
    {
        return RailHome(dpi, fit, railW, railH, Screen.PrimaryScreen.WorkingArea);
    }

    public static Rectangle RailHome(float dpi, float fit, int railW, int railH, Rectangle work)
    {
        int topClear = DisplayScale.Px(TopClearDip, dpi, fit);
        int bottomClear = DisplayScale.Px(BottomClearDip, dpi, fit);
        int maxH = work.Height - topClear - bottomClear;
        if (maxH < 1)
            maxH = work.Height;
        if (railH > maxH)
            railH = maxH;

        int centerY = work.Top + (int)(work.Height * RailCenterYFraction);
        int top = centerY - railH / 2;
        int minTop = work.Top + topClear;
        int maxTop = work.Bottom - bottomClear - railH;
        if (top < minTop) top = minTop;
        if (top > maxTop) top = Math.Max(minTop, maxTop);

        int left = work.Left + DisplayScale.Px(RailLeftInsetDip, dpi, fit);
        return new Rectangle(left, top, railW, railH);
    }

    public static Rectangle PaneHome(float dpi, float fit, Rectangle rail)
    {
        return PaneHome(dpi, fit, rail, Screen.PrimaryScreen.WorkingArea);
    }

    public static Rectangle PaneHome(float dpi, float fit, Rectangle rail, Rectangle work)
    {
        int topClear = DisplayScale.Px(TopClearDip, dpi, fit);
        int bottomClear = DisplayScale.Px(BottomClearDip, dpi, fit);
        int gap = DisplayScale.Px(PaneGapDip, dpi, fit);
        int wantW = DisplayScale.Px(PaneWidthDip, dpi, fit);
        int wantH = DisplayScale.Px(PaneHeightDip, dpi, fit);

        int maxH = work.Height - topClear - bottomClear;
        if (maxH < 1)
            maxH = work.Height;
        if (wantH > maxH)
            wantH = maxH;
        int minH = DisplayScale.Px(520, dpi, fit);
        if (wantH < minH)
            wantH = Math.Min(maxH, minH);

        int edgeClear = DisplayScale.Px(EdgeClearDip, dpi, fit);
        int maxW = Math.Max(320, work.Width - (rail.Right + gap - work.Left) - edgeClear);
        if (wantW > maxW)
            wantW = maxW;

        int left = rail.Right + gap;
        int top = rail.Top + rail.Height / 2 - wantH / 2;
        int minTop = work.Top + topClear;
        int maxTop = work.Bottom - bottomClear - wantH;
        if (top < minTop) top = minTop;
        if (top > maxTop) top = Math.Max(minTop, maxTop);

        if (left + wantW > work.Right - edgeClear)
            left = Math.Max(work.Left + rail.Width + gap, work.Right - wantW - edgeClear);

        return new Rectangle(left, top, wantW, wantH);
    }

    /// <summary>True when pane does not intersect synthetic top/right sibling hover strips.</summary>
    public static bool PaneClearsSiblingStrips(Rectangle pane, Rectangle work, int topStripPx, int rightStripPx)
    {
        if (topStripPx < 1) topStripPx = 1;
        if (rightStripPx < 1) rightStripPx = 1;
        Rectangle topStrip = new Rectangle(work.Left, work.Top, work.Width, topStripPx);
        Rectangle rightStrip = new Rectangle(work.Right - rightStripPx, work.Top, rightStripPx, work.Height);
        return !pane.IntersectsWith(topStrip) && !pane.IntersectsWith(rightStrip);
    }
}
