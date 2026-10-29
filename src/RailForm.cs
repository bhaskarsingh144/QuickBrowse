using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

/// <summary>
/// Places rail — measure-driven cells so labels never clip under the next chip.
/// Hits 0..UtilityCount-1 are utility chips (Pins, Recent); then system places.
/// </summary>
internal sealed class RailForm : Form
{
    public const int UtilityCount = 2;
    public const int PinsHit = 0;
    public const int RecentHit = 1;

    private readonly ShellHost _session;
    private readonly List<PlaceEntry> _places;
    private readonly List<Rectangle> _hits = new List<Rectangle>();
    private int _hot = -1;
    private int _active = -1;
    private bool _pinsSelected;
    private bool _recentSelected;
    private float _dpi = 96f;
    private float _fit = 1f;
    private float _textFit = 1f;
    private Font _labelFont;
    private RailCellMetrics _cell;
    private ToolTip _tip;
    private Image _pinsGlyph;
    private Image _recentGlyph;

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);

    public RailForm(ShellHost session)
    {
        _session = session;
        _places = new List<PlaceEntry>(PlacesCatalog.DefaultPlaces());
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = false;
        BackColor = UiTheme.Bg;
        Opacity = ChromeVisual.PanelOpacity;
        DoubleBuffered = true;
        Text = "QuickBrowse";
        AutoScaleMode = AutoScaleMode.None;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        _tip = new ToolTip();
        _cell = ChromeLayout.MeasureRailCell(96f, 1f, null);
    }

    public IList<PlaceEntry> Places { get { return _places; } }
    public int ActiveIndex { get { return _active; } }
    public bool PinsSelected { get { return _pinsSelected; } }
    public bool RecentSelected { get { return _recentSelected; } }
    public bool MoreSelected { get { return _pinsSelected; } }
    public float Dpi { get { return _dpi; } }
    public float Fit { get { return _fit; } }
    public float TextFit { get { return _textFit; } }
    public RailCellMetrics CellMetrics { get { return _cell; } }

    /// <summary>Utility chips + place rows.</summary>
    public int RowCount { get { return _places.Count + UtilityCount; } }

    protected override bool ShowWithoutActivation { get { return true; } }

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            ShellChrome.Prepare(cp);
            return cp;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ChromeVisual.PreferDarkMode(Handle);
        Remeasure();
        ShellChrome.Pin(Handle);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        if (_tip != null) { _tip.Dispose(); _tip = null; }
        if (_labelFont != null) { _labelFont.Dispose(); _labelFont = null; }
        if (_pinsGlyph != null) { _pinsGlyph.Dispose(); _pinsGlyph = null; }
        if (_recentGlyph != null) { _recentGlyph.Dispose(); _recentGlyph = null; }
        base.OnFormClosed(e);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        ChromeVisual.ApplyRoundedRegion(this, Px(ChromeVisual.CornerDip));
        Invalidate();
    }

    public void Remeasure()
    {
        if (IsHandleCreated)
        {
            try { _dpi = GetDpiForWindow(Handle); }
            catch { _dpi = 96f; }
        }
        if (_dpi < 96f) _dpi = 96f;
        DisplayProfile profile = DisplayScale.Measure(_dpi);
        _dpi = profile.Dpi;
        _fit = profile.Fit;
        _textFit = profile.TextFit < 0.8f ? 0.8f : profile.TextFit;
        ShellChrome.SetDipScale(_dpi, _fit);
        Width = Px(ChromeLayout.RailWidthDip);

        if (_labelFont != null) _labelFont.Dispose();
        float em = ChromeLayout.FontEm(_fit, _textFit);
        _labelFont = ChromeFonts.CreateLabel(ChromeLayout.RailLabelPt, em);
        _cell = ChromeLayout.MeasureRailCell(_dpi, _fit, _labelFont);

        if (_pinsGlyph != null) _pinsGlyph.Dispose();
        if (_recentGlyph != null) _recentGlyph.Dispose();
        int glyph = Math.Max(18, Px(ChromeLayout.PaneChromeIconDip));
        _pinsGlyph = ChromeIcons.Pin(glyph);
        _recentGlyph = ChromeIcons.Recent(glyph);

        LayoutHits();
        ChromeVisual.ApplyRoundedRegion(this, Px(ChromeVisual.CornerDip));
        Invalidate();
    }

    public int Px(int dip) { return DisplayScale.Px(dip, _dpi, _fit); }

    public void SetActivePlace(int index)
    {
        if (_active == index && !_pinsSelected && !_recentSelected) return;
        _active = index;
        _pinsSelected = false;
        _recentSelected = false;
        Invalidate();
    }

    public void SetPinsSelected(bool selected)
    {
        if (_pinsSelected == selected && (!selected || (_active < 0 && !_recentSelected)))
        {
            if (selected)
            {
                _active = -1;
                _recentSelected = false;
                Invalidate();
            }
            return;
        }
        _pinsSelected = selected;
        if (selected)
        {
            _active = -1;
            _recentSelected = false;
        }
        Invalidate();
    }

    public void SetRecentSelected(bool selected)
    {
        if (_recentSelected == selected && (!selected || (_active < 0 && !_pinsSelected)))
        {
            if (selected)
            {
                _active = -1;
                _pinsSelected = false;
                Invalidate();
            }
            return;
        }
        _recentSelected = selected;
        if (selected)
        {
            _active = -1;
            _pinsSelected = false;
        }
        Invalidate();
    }

    public void SetMoreSelected(bool selected)
    {
        SetPinsSelected(selected);
    }

    public bool ContainsScreenPoint(Point pt) { return Bounds.Contains(pt); }

    public static string ShortLabel(PlaceEntry place)
    {
        if (place == null) return "";
        switch (place.Id)
        {
            case "thispc": return "This PC";
            case "desktop": return "Desktop";
            case "downloads": return "Downloads";
            case "documents": return "Docs";
            case "pictures": return "Pictures";
            case "music": return "Music";
            case "videos": return "Videos";
            case "profile": return "Profile";
            default: return place.Title;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(UiTheme.Bg);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

        int corner = Px(ChromeVisual.CornerDip);
        ChromeVisual.DrawRoundBorder(g, new Rectangle(0, 0, Width, Height), corner, UiTheme.BorderSoft);

        LayoutHits();
        int chipR = Px(ChromeVisual.ChipCornerDip);

        for (int i = 0; i < _hits.Count; i++)
        {
            Rectangle chip = ChipFromHit(_hits[i]);
            bool selected = (i == PinsHit && _pinsSelected)
                || (i == RecentHit && _recentSelected)
                || (i >= UtilityCount && (i - UtilityCount) == _active);
            bool hot = i == _hot;
            if (selected)
            {
                ChromeVisual.FillRound(g, chip, chipR, UiTheme.Raised);
                using (SolidBrush accent = new SolidBrush(UiTheme.Accent))
                    g.FillRectangle(accent, new Rectangle(chip.X + Px(2), chip.Y + Px(8), Px(3), Math.Max(4, chip.Height - Px(16))));
            }
            else if (hot)
            {
                ChromeVisual.FillRound(g, chip, chipR, UiTheme.Hover);
            }
        }

        PaintUtilityChip(g, PinsHit, _pinsGlyph, "Pins", _pinsSelected);
        PaintUtilityChip(g, RecentHit, _recentGlyph, "Recent", _recentSelected);

        for (int i = 0; i < _places.Count; i++)
        {
            int hitIndex = i + UtilityCount;
            if (hitIndex >= _hits.Count)
                break;
            Rectangle hit = _hits[hitIndex];
            Rectangle chip = ChipFromHit(hit);
            PlaceEntry place = _places[i];
            Bitmap bmp = place.Kind == PlaceKind.ThisPc
                ? IconCache.GetThisPc(true)
                : IconCache.GetForPath(PlacesCatalog.ResolvePath(place) ?? place.Title, true, true);

            int contentTop = hit.Y + _cell.ContentTopPad;
            int iconSize = _cell.IconSize;
            if (bmp != null)
            {
                int x = hit.X + (hit.Width - iconSize) / 2;
                g.DrawImage(bmp, new Rectangle(x, contentTop, iconSize, iconSize));
            }

            if (_labelFont != null)
            {
                int labelTop = contentTop + iconSize + _cell.IconLabelGap;
                Rectangle labelRect = new Rectangle(
                    chip.X + Px(2),
                    labelTop,
                    Math.Max(8, chip.Width - Px(4)),
                    _cell.LabelBand);
                Color ink = i == _active ? UiTheme.Accent : UiTheme.Text;
                using (SolidBrush brush = new SolidBrush(ink))
                using (StringFormat fmt = new StringFormat())
                {
                    fmt.Alignment = StringAlignment.Center;
                    fmt.LineAlignment = StringAlignment.Center;
                    fmt.Trimming = StringTrimming.EllipsisCharacter;
                    fmt.FormatFlags = StringFormatFlags.NoWrap;
                    g.DrawString(ShortLabel(place), _labelFont, brush, labelRect, fmt);
                }
            }
        }
    }

    private void PaintUtilityChip(Graphics g, int hitIndex, Image glyph, string label, bool selected)
    {
        if (hitIndex >= _hits.Count)
            return;
        Rectangle hit = _hits[hitIndex];
        Rectangle chip = ChipFromHit(hit);
        int contentTop = hit.Y + _cell.ContentTopPad;
        if (glyph != null)
        {
            int iw = glyph.Width;
            int ih = glyph.Height;
            int x = hit.X + (hit.Width - iw) / 2;
            int y = contentTop + Math.Max(0, (_cell.IconSize - ih) / 2);
            g.DrawImage(glyph, x, y, iw, ih);
        }
        if (_labelFont != null)
        {
            int labelTop = contentTop + _cell.IconSize + _cell.IconLabelGap;
            Rectangle labelRect = new Rectangle(
                chip.X + Px(2),
                labelTop,
                Math.Max(8, chip.Width - Px(4)),
                _cell.LabelBand);
            Color ink = selected ? UiTheme.Accent : UiTheme.Text;
            using (SolidBrush brush = new SolidBrush(ink))
            using (StringFormat fmt = new StringFormat())
            {
                fmt.Alignment = StringAlignment.Center;
                fmt.LineAlignment = StringAlignment.Center;
                fmt.Trimming = StringTrimming.EllipsisCharacter;
                fmt.FormatFlags = StringFormatFlags.NoWrap;
                g.DrawString(label, _labelFont, brush, labelRect, fmt);
            }
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        _session.NoteActivity();
        int index = HitIndex(e.Location);
        if (index != _hot)
        {
            _hot = index;
            Invalidate();
            if (index == PinsHit)
                _tip.SetToolTip(this, "Pinned folders");
            else if (index == RecentHit)
                _tip.SetToolTip(this, "Recent files and folders");
            else if (index >= UtilityCount && index - UtilityCount < _places.Count)
                _tip.SetToolTip(this, _places[index - UtilityCount].Title);
            else
                _tip.SetToolTip(this, "");
        }
        base.OnMouseMove(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        if (_hot != -1) { _hot = -1; Invalidate(); }
        base.OnMouseLeave(e);
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        _session.NoteActivity();
        if (e.Button != MouseButtons.Left) return;
        int index = HitIndex(e.Location);
        if (index < 0) return;
        if (index == PinsHit)
        {
            _session.OnPinsClicked();
            return;
        }
        if (index == RecentHit)
        {
            _session.OnRecentClicked();
            return;
        }
        int placeIndex = index - UtilityCount;
        if (placeIndex >= 0 && placeIndex < _places.Count)
            _session.OnPlaceClicked(placeIndex, _places[placeIndex]);
    }

    protected override void WndProc(ref Message m)
    {
        if (ShellChrome.SwallowMinimize(ref m)) return;
        base.WndProc(ref m);
        const int WM_DPICHANGED = 0x02E0;
        const int WM_DISPLAYCHANGE = 0x007E;
        if (m.Msg == WM_DPICHANGED || m.Msg == WM_DISPLAYCHANGE)
            _session.OnDisplayChanged();
    }

    private void LayoutHits()
    {
        _hits.Clear();
        int pad = Px(ChromeLayout.RailPadDip);
        int row = _cell.RowHeight > 0 ? _cell.RowHeight : Px(ChromeLayout.RailMinRowDip);
        int y = pad;
        int rows = _places.Count + UtilityCount;
        for (int i = 0; i < rows; i++)
        {
            _hits.Add(new Rectangle(0, y, Width, row));
            y += row;
        }
    }

    private Rectangle ChipFromHit(Rectangle hit)
    {
        return Rectangle.Inflate(hit, -_cell.ChipInsetX, -_cell.ChipInsetY);
    }

    private int HitIndex(Point pt)
    {
        for (int i = 0; i < _hits.Count; i++)
            if (_hits[i].Contains(pt)) return i;
        return -1;
    }
}
