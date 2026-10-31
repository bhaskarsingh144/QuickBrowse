using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

/// <summary>
/// Browse-pane-sized Recent surface: date sections + icon tiles (not the slim Pins flyout).
/// </summary>
internal sealed class RecentPanelForm : Form
{
    private sealed class TileHit
    {
        public Rectangle Bounds;
        public RecentEntry Entry;
        public int Index;
    }

    private sealed class SectionHit
    {
        public Rectangle Bounds;
        public string Title;
    }

    private readonly ShellHost _session;
    private readonly Panel _header;
    private readonly Label _title;
    private readonly ChromeToolButton _closeBtn;
    private readonly Panel _body;
    private readonly ContextMenuStrip _menu;
    private readonly ToolStripMenuItem _openItem;
    private readonly ToolStripMenuItem _explorerItem;
    private readonly ToolStripMenuItem _copyItem;
    private readonly ChromeHoverTip _tip;

    private readonly List<RecentEntry> _entries = new List<RecentEntry>();
    private readonly List<RecentBucketGroup> _groups = new List<RecentBucketGroup>();
    private readonly List<TileHit> _tiles = new List<TileHit>();
    private readonly List<SectionHit> _sections = new List<SectionHit>();

    private Font _titleFont;
    private Font _sectionFont;
    private Font _labelFont;
    private Font _metaFont;
    private Font _emptyFont;
    private Image _closeGlyph;
    private float _dpi = 96f;
    private float _fit = 1f;
    private int _hot = -1;
    private int _active = -1;
    private int _menuIndex = -1;
    private int _scrollYPreserve;

    public RecentPanelForm(ShellHost session)
    {
        _session = session;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = false;
        BackColor = UiTheme.Bg;
        Opacity = 0.0;
        DoubleBuffered = true;
        Text = "QuickBrowse Recent";
        AutoScaleMode = AutoScaleMode.None;
        KeyPreview = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);

        _body = new DoubleBufferedPanel();
        _body.BackColor = UiTheme.Bg;
        _body.Dock = DockStyle.Fill;
        _body.AutoScroll = true;
        _body.Paint += OnBodyPaint;
        _body.MouseMove += OnBodyMouseMove;
        _body.MouseLeave += OnBodyMouseLeave;
        _body.MouseClick += OnBodyMouseClick;
        _body.Scroll += delegate { _body.Invalidate(); };
        Controls.Add(_body);

        _header = new Panel();
        _header.BackColor = UiTheme.Surface;
        _header.Dock = DockStyle.Top;
        Controls.Add(_header);

        _title = new Label();
        _title.AutoSize = false;
        _title.Text = "Recent";
        _title.ForeColor = UiTheme.Text;
        _title.BackColor = Color.Transparent;
        _title.TextAlign = ContentAlignment.MiddleLeft;
        _header.Controls.Add(_title);

        _closeBtn = new ChromeToolButton();
        _closeBtn.Click += delegate { _session.CloseRecentPanel(); };
        _header.Controls.Add(_closeBtn);

        _tip = new ChromeHoverTip();

        _menu = new ContextMenuStrip();
        _openItem = new ToolStripMenuItem("Open", null, delegate { ActivateIndex(_menuIndex); });
        _explorerItem = new ToolStripMenuItem("Show in Explorer", null, delegate { ShowInExplorerMenu(); });
        _copyItem = new ToolStripMenuItem("Copy path", null, delegate { CopyPathMenu(); });
        _menu.Items.Add(_openItem);
        _menu.Items.Add(_explorerItem);
        _menu.Items.Add(_copyItem);
    }

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
        if (_tip != null) _tip.Dispose();
        if (_menu != null) _menu.Dispose();
        DisposeFonts();
        if (_closeGlyph != null) { _closeGlyph.Dispose(); _closeGlyph = null; }
        base.OnFormClosed(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        ChromeVisual.DrawRoundBorder(e.Graphics, new Rectangle(0, 0, Width, Height), Px(ChromeVisual.CornerDip), UiTheme.BorderSoft);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        _session.NoteActivity();
        if (keyData == Keys.Escape)
        {
            _session.CloseRecentPanel();
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void WndProc(ref Message m)
    {
        if (ShellChrome.SwallowMinimize(ref m))
            return;
        base.WndProc(ref m);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        LayoutChrome();
        LayoutContent();
        ChromeVisual.ApplyRoundedRegion(this, Px(ChromeVisual.CornerDip));
        Invalidate();
    }

    private int Px(int dip)
    {
        return DisplayScale.Px(dip, _dpi, _fit);
    }

    private void DisposeFonts()
    {
        if (_titleFont != null) { _titleFont.Dispose(); _titleFont = null; }
        if (_sectionFont != null) { _sectionFont.Dispose(); _sectionFont = null; }
        if (_labelFont != null) { _labelFont.Dispose(); _labelFont = null; }
        if (_metaFont != null) { _metaFont.Dispose(); _metaFont = null; }
        if (_emptyFont != null) { _emptyFont.Dispose(); _emptyFont = null; }
    }

    public void Remeasure()
    {
        try
        {
            if (IsHandleCreated)
                _dpi = NativeDpi();
        }
        catch { _dpi = 96f; }
        if (_dpi < 96f) _dpi = 96f;
        DisplayProfile profile = DisplayScale.Measure(_dpi);
        _dpi = profile.Dpi;
        _fit = profile.Fit;
        float textFit = profile.TextFit < 0.8f ? 0.8f : profile.TextFit;
        float em = ChromeLayout.FontEm(_fit, textFit);

        DisposeFonts();
        _titleFont = ChromeFonts.CreateLabel(ChromeLayout.TitlePt + 1f, em);
        _sectionFont = ChromeFonts.CreateLabel(ChromeLayout.UiPt - 0.5f, em);
        _labelFont = ChromeFonts.CreateLabel(ChromeLayout.UiPt - 0.5f, em);
        _metaFont = ChromeFonts.CreateUi(ChromeLayout.MetricMinPt + 0.5f, em);
        _emptyFont = ChromeFonts.CreateUi(ChromeLayout.UiPt, em);

        if (_closeGlyph != null) _closeGlyph.Dispose();
        int icon = Math.Max(16, Px(ChromeLayout.PaneChromeIconDip) - 4);
        _closeGlyph = ChromeIcons.Close(icon);
        _closeBtn.Glyph = _closeGlyph;

        _title.Font = _titleFont;
        LayoutChrome();
        LayoutContent();
        ChromeVisual.ApplyRoundedRegion(this, Px(ChromeVisual.CornerDip));
        Invalidate();
        _body.Invalidate();
    }

    public void SetEntries(IList<RecentEntry> entries, string activePath)
    {
        _scrollYPreserve = -_body.AutoScrollPosition.Y;
        _entries.Clear();
        if (entries != null)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i] != null)
                    _entries.Add(entries[i]);
            }
        }
        _groups.Clear();
        IList<RecentBucketGroup> grouped = RecentSource.GroupByDateBucket(_entries);
        for (int i = 0; i < grouped.Count; i++)
            _groups.Add(grouped[i]);

        _active = -1;
        string norm = PinStore.Normalize(activePath);
        if (norm != null)
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                if (string.Equals(PinStore.Normalize(_entries[i].Path), norm, StringComparison.OrdinalIgnoreCase))
                {
                    _active = i;
                    break;
                }
            }
        }
        _hot = -1;
        LayoutContent();
        try
        {
            if (_scrollYPreserve > 0)
                _body.AutoScrollPosition = new Point(0, _scrollYPreserve);
        }
        catch { }
        _body.Invalidate();
    }

    private void LayoutChrome()
    {
        int headerH = Math.Max(Px(40), Px(ChromeLayout.RecentHeaderDip));
        _header.Height = headerH;
        int edge = Px(12);
        int btn = Math.Max(Px(32), Px(ChromeLayout.PaneChromeBtnDip) - 4);
        _closeBtn.SetBounds(Width - edge - btn, (headerH - btn) / 2, btn, btn);
        _title.SetBounds(edge, 0, Math.Max(8, _closeBtn.Left - edge - Px(8)), headerH);
    }

    private void LayoutContent()
    {
        _tiles.Clear();
        _sections.Clear();
        int pad = Px(ChromeLayout.RecentPadDip);
        int tileW = Math.Max(Px(96), Px(ChromeLayout.RecentTileWidthDip));
        int tileH = Math.Max(Px(88), Px(ChromeLayout.RecentTileHeightDip));
        int gap = Px(ChromeLayout.RecentTileGapDip);
        int sectionH = Px(ChromeLayout.RecentSectionHeaderDip);
        int sectionGap = Px(ChromeLayout.RecentSectionGapDip);
        int contentW = Math.Max(tileW, _body.ClientSize.Width - SystemInformation.VerticalScrollBarWidth);
        int cols = Math.Max(1, (contentW - pad * 2 + gap) / (tileW + gap));
        int y = pad;
        int flatIndex = 0;

        if (_groups.Count == 0)
        {
            _body.AutoScrollMinSize = new Size(0, Math.Max(_body.ClientSize.Height, pad * 2 + Px(120)));
            return;
        }

        for (int g = 0; g < _groups.Count; g++)
        {
            RecentBucketGroup group = _groups[g];
            if (g > 0)
                y += sectionGap;
            Rectangle section = new Rectangle(pad, y, Math.Max(8, contentW - pad * 2), sectionH);
            _sections.Add(new SectionHit { Bounds = section, Title = group.Title });
            y += sectionH + Px(4);

            int col = 0;
            int rowTop = y;
            for (int i = 0; i < group.Entries.Count; i++)
            {
                RecentEntry entry = group.Entries[i];
                int x = pad + col * (tileW + gap);
                Rectangle tile = new Rectangle(x, rowTop, tileW, tileH);
                _tiles.Add(new TileHit { Bounds = tile, Entry = entry, Index = flatIndex });
                flatIndex++;
                col++;
                if (col >= cols)
                {
                    col = 0;
                    rowTop += tileH + gap;
                }
            }
            if (col != 0)
                rowTop += tileH + gap;
            y = rowTop;
        }

        _body.AutoScrollMinSize = new Size(0, y + pad);
    }

    private void OnBodyPaint(object sender, PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(UiTheme.Bg);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

        Point scroll = _body.AutoScrollPosition;
        g.TranslateTransform(scroll.X, scroll.Y);

        if (_entries.Count == 0)
        {
            if (_emptyFont != null)
            {
                Rectangle r = new Rectangle(
                    Px(24),
                    Math.Max(Px(40), (_body.ClientSize.Height - Px(80)) / 2),
                    Math.Max(8, _body.ClientSize.Width - Px(48)),
                    Px(80));
                TextRenderer.DrawText(g,
                    "No recent items yet\nOpen files in Explorer or QuickBrowse to build this list",
                    _emptyFont,
                    r,
                    UiTheme.Muted,
                    TextFormatFlags.WordBreak | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
            return;
        }

        if (_sectionFont != null)
        {
            for (int i = 0; i < _sections.Count; i++)
            {
                SectionHit s = _sections[i];
                TextRenderer.DrawText(g, s.Title, _sectionFont, s.Bounds, UiTheme.TextSecondary,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
            }
        }

        int iconSize = Math.Max(Px(40), Px(ChromeLayout.RecentTileIconDip));
        int chipR = Math.Max(6, Px(ChromeVisual.ChipCornerDip));
        DateTime nowUtc = DateTime.UtcNow;

        for (int i = 0; i < _tiles.Count; i++)
        {
            TileHit hit = _tiles[i];
            Rectangle chip = Rectangle.Inflate(hit.Bounds, -Px(2), -Px(2));
            if (hit.Index == _active)
                ChromeVisual.FillRound(g, chip, chipR, UiTheme.Raised);
            else if (hit.Index == _hot)
                ChromeVisual.FillRound(g, chip, chipR, UiTheme.Hover);

            RecentEntry entry = hit.Entry;
            Bitmap bmp = IconCache.GetForPath(entry.Path, entry.IsDirectory, true);
            int ix = chip.X + (chip.Width - iconSize) / 2;
            int iy = chip.Y + Px(10);
            if (bmp != null)
                g.DrawImage(bmp, new Rectangle(ix, iy, iconSize, iconSize));

            if (_labelFont != null)
            {
                Rectangle name = new Rectangle(chip.X + Px(4), iy + iconSize + Px(6), chip.Width - Px(8), Px(18));
                Color ink = hit.Index == _active ? UiTheme.Accent : UiTheme.Text;
                TextRenderer.DrawText(g, entry.Title ?? "", _labelFont, name, ink,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
            }
            if (_metaFont != null)
            {
                string meta = RecentSource.FormatRelativeTime(entry.LastAccessUtc, nowUtc);
                Rectangle metaR = new Rectangle(chip.X + Px(4), iy + iconSize + Px(24), chip.Width - Px(8), Px(16));
                TextRenderer.DrawText(g, meta, _metaFont, metaR, UiTheme.Muted,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
            }
        }
    }

    private void OnBodyMouseMove(object sender, MouseEventArgs e)
    {
        _session.NoteActivity();
        Point content = ToContent(e.Location);
        int index = HitTile(content);
        if (index != _hot)
        {
            _hot = index;
            _body.Invalidate();
            if (index >= 0 && index < _tiles.Count)
            {
                Point screen = _body.PointToScreen(e.Location);
                _tip.ShowTip(this, _tiles[index].Entry.Path, screen, _metaFont ?? Font);
            }
            else
                _tip.HideTip();
        }
    }

    private void OnBodyMouseLeave(object sender, EventArgs e)
    {
        if (_hot != -1)
        {
            _hot = -1;
            _body.Invalidate();
        }
        _tip.HideTip();
    }

    private void OnBodyMouseClick(object sender, MouseEventArgs e)
    {
        _session.NoteActivity();
        Point content = ToContent(e.Location);
        int index = HitTile(content);
        if (index < 0 || index >= _tiles.Count)
            return;
        if (e.Button == MouseButtons.Left)
            ActivateIndex(index);
        else if (e.Button == MouseButtons.Right)
        {
            _menuIndex = index;
            _menu.Show(_body, e.Location);
        }
    }

    private Point ToContent(Point client)
    {
        Point scroll = _body.AutoScrollPosition;
        return new Point(client.X - scroll.X, client.Y - scroll.Y);
    }

    private int HitTile(Point content)
    {
        for (int i = 0; i < _tiles.Count; i++)
        {
            if (_tiles[i].Bounds.Contains(content))
                return i;
        }
        return -1;
    }

    private sealed class DoubleBufferedPanel : Panel
    {
        public DoubleBufferedPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            UpdateStyles();
        }
    }

    private void ActivateIndex(int index)
    {
        if (index < 0 || index >= _tiles.Count)
            return;
        _session.OnRecentItemClicked(_tiles[index].Entry.Path);
    }

    private void ShowInExplorerMenu()
    {
        if (_menuIndex < 0 || _menuIndex >= _tiles.Count)
            return;
        string path = _tiles[_menuIndex].Entry.Path;
        try
        {
            Process.Start("explorer.exe", "/select,\"" + path + "\"");
        }
        catch
        {
        }
    }

    private void CopyPathMenu()
    {
        if (_menuIndex < 0 || _menuIndex >= _tiles.Count)
            return;
        try
        {
            Clipboard.SetText(_tiles[_menuIndex].Entry.Path ?? "");
        }
        catch
        {
        }
    }

    private uint NativeDpi()
    {
        try { return GetDpiForWindow(Handle); }
        catch { return 96; }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);
}
