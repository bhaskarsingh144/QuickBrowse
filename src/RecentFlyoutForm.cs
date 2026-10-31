using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

/// <summary>
/// Dense Recent list beside the rail — date sections, title + relative time.
/// Uses a QuickView-style custom scrollbar (no native white thumb).
/// </summary>
internal sealed class RecentFlyoutForm : Form
{
    private sealed class RowHit
    {
        public Rectangle Bounds;
        public RecentEntry Entry;
        public bool IsSection;
        public string SectionTitle;
    }

    private readonly ShellHost _session;
    private readonly Panel _viewport;
    private readonly CanvasPanel _canvas;
    private readonly ChromeScrollBar _bar;
    private readonly ContextMenuStrip _menu;
    private readonly ToolStripMenuItem _openItem;
    private readonly ToolStripMenuItem _explorerItem;
    private readonly ToolStripMenuItem _copyItem;
    private readonly ToolTip _tip;

    private readonly List<RecentEntry> _entries = new List<RecentEntry>();
    private readonly List<RowHit> _rows = new List<RowHit>();

    private Font _sectionFont;
    private Font _labelFont;
    private Font _metaFont;
    private Font _emptyFont;
    private float _dpi = 96f;
    private float _fit = 1f;
    private int _hot = -1;
    private int _active = -1;
    private int _menuIndex = -1;
    private int _scrollY;
    private int _contentH;
    private bool _syncingScroll;

    public RecentFlyoutForm(ShellHost session)
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

        _viewport = new Panel();
        _viewport.Dock = DockStyle.Fill;
        _viewport.BackColor = UiTheme.Bg;
        _viewport.AutoScroll = false;
        _viewport.MouseWheel += OnViewportMouseWheel;

        _bar = new ChromeScrollBar();
        _bar.Dock = DockStyle.Right;
        _bar.ValueChanged += OnBarValueChanged;

        // Higher z-order docks first: bar Right, then viewport Fill.
        Controls.Add(_viewport);
        Controls.Add(_bar);

        _canvas = new CanvasPanel();
        _canvas.BackColor = UiTheme.Bg;
        _canvas.Paint += OnCanvasPaint;
        _canvas.MouseMove += OnCanvasMouseMove;
        _canvas.MouseLeave += OnCanvasMouseLeave;
        _canvas.MouseClick += OnCanvasMouseClick;
        _canvas.MouseWheel += OnViewportMouseWheel;
        _viewport.Controls.Add(_canvas);

        _tip = new ToolTip();
        _tip.Popup += delegate
        {
            BeginInvoke(new MethodInvoker(ShellChrome.ElevateProcessToolTips));
        };

        _menu = new ContextMenuStrip();
        _openItem = new ToolStripMenuItem("Open", null, delegate { ActivateHit(_menuIndex); });
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
        base.OnFormClosed(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        ChromeVisual.DrawRoundBorder(e.Graphics, new Rectangle(0, 0, Width, Height), Px(ChromeVisual.CornerDip), UiTheme.BorderSoft);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        SyncScrollMetrics(false);
        ChromeVisual.ApplyRoundedRegion(this, Px(ChromeVisual.CornerDip));
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        _session.NoteActivity();
        if (keyData == Keys.Escape)
        {
            _session.CloseRecentFlyout();
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

    private int Px(int dip)
    {
        return DisplayScale.Px(dip, _dpi, _fit);
    }

    private void DisposeFonts()
    {
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
        _sectionFont = ChromeFonts.CreateLabel(ChromeLayout.UiPt - 1f, em);
        _labelFont = ChromeFonts.CreateLabel(ChromeLayout.UiPt - 0.5f, em);
        _metaFont = ChromeFonts.CreateUi(ChromeLayout.MetricMinPt + 0.5f, em);
        _emptyFont = ChromeFonts.CreateUi(ChromeLayout.MetricMinPt + 1f, em);

        Width = Px(ChromeLayout.RecentFlyoutWidthDip);
        _bar.Width = Math.Max(Px(8), Px(ChromeLayout.ChromeScrollWidthDip));
        LayoutRows();
        ChromeVisual.ApplyRoundedRegion(this, Px(ChromeVisual.CornerDip));
        Invalidate();
    }

    public void SetEntries(IList<RecentEntry> entries, string activePath)
    {
        int preserve = _scrollY;
        _entries.Clear();
        if (entries != null)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i] != null)
                    _entries.Add(entries[i]);
            }
        }

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
        LayoutRows();
        _scrollY = preserve;
        SyncScrollMetrics(true);
        _canvas.Invalidate();
    }

    public int DesiredHeight()
    {
        int pad = Px(ChromeLayout.RecentFlyoutPadDip);
        int row = RowHeight();
        int section = SectionHeight();
        int content = pad * 2;
        if (_entries.Count == 0)
            content += row * 2;
        else
        {
            IList<RecentBucketGroup> groups = RecentSource.GroupByDateBucket(_entries);
            for (int g = 0; g < groups.Count; g++)
            {
                if (g > 0)
                    content += Px(ChromeLayout.RecentSectionGapDip);
                content += section;
                content += groups[g].Entries.Count * row;
            }
        }
        int max = Px(ChromeLayout.RecentFlyoutMaxHeightDip);
        if (content > max)
            return max;
        int min = pad * 2 + row * 2;
        return content < min ? min : content;
    }

    private int RowHeight()
    {
        return Math.Max(Px(30), Px(ChromeLayout.PinsFlyoutRowDip) + Px(2));
    }

    private int SectionHeight()
    {
        return Math.Max(Px(24), Px(ChromeLayout.RecentSectionHeaderDip));
    }

    private void LayoutRows()
    {
        _rows.Clear();
        int pad = Px(ChromeLayout.RecentFlyoutPadDip);
        int rowH = RowHeight();
        int sectionH = SectionHeight();
        int sectionGap = Px(ChromeLayout.RecentSectionGapDip);
        int y = pad;

        Height = DesiredHeight();
        int innerW = Math.Max(8, _viewport.ClientSize.Width > 0 ? _viewport.ClientSize.Width : Width - _bar.Width);

        if (_entries.Count == 0)
        {
            _contentH = pad * 2 + rowH * 2;
            _canvas.Size = new Size(innerW, Math.Max(_viewport.ClientSize.Height, _contentH));
            SyncScrollMetrics(true);
            return;
        }

        IList<RecentBucketGroup> groups = RecentSource.GroupByDateBucket(_entries);
        for (int g = 0; g < groups.Count; g++)
        {
            if (g > 0)
                y += sectionGap;
            RecentBucketGroup group = groups[g];
            _rows.Add(new RowHit
            {
                Bounds = new Rectangle(0, y, innerW, sectionH),
                IsSection = true,
                SectionTitle = group.Title
            });
            y += sectionH;

            for (int i = 0; i < group.Entries.Count; i++)
            {
                _rows.Add(new RowHit
                {
                    Bounds = new Rectangle(0, y, innerW, rowH),
                    Entry = group.Entries[i],
                    IsSection = false
                });
                y += rowH;
            }
        }

        _contentH = y + pad;
        _canvas.Size = new Size(innerW, Math.Max(_viewport.ClientSize.Height, _contentH));
        SyncScrollMetrics(true);
    }

    private void SyncScrollMetrics(bool clampValue)
    {
        int viewH = Math.Max(1, _viewport.ClientSize.Height);
        int maxScroll = Math.Max(0, _contentH - viewH);
        if (clampValue || _scrollY > maxScroll)
            _scrollY = Math.Min(_scrollY, maxScroll);
        if (_scrollY < 0)
            _scrollY = 0;

        _syncingScroll = true;
        try
        {
            // Gutter always reserved; when content fits, thumb fills the track (no white native bar).
            _bar.SetMetrics(0, Math.Max(viewH, _contentH), viewH, _scrollY);
            _bar.Enabled = maxScroll > 0;
        }
        finally
        {
            _syncingScroll = false;
        }
        ApplyCanvasOffset();
    }

    private void ApplyCanvasOffset()
    {
        _canvas.Location = new Point(0, -_scrollY);
    }

    private void OnBarValueChanged(object sender, EventArgs e)
    {
        if (_syncingScroll)
            return;
        _scrollY = _bar.Value;
        ApplyCanvasOffset();
        _session.NoteActivity();
    }

    private void OnViewportMouseWheel(object sender, MouseEventArgs e)
    {
        _session.NoteActivity();
        int step = Math.Max(RowHeight(), Math.Abs(e.Delta) / 3);
        if (e.Delta > 0)
            _scrollY -= step;
        else
            _scrollY += step;
        SyncScrollMetrics(true);
        if (_bar.Visible)
            _bar.Value = _scrollY;
    }

    private void OnCanvasPaint(object sender, PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(UiTheme.Bg);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

        int side = Px(ChromeLayout.RecentFlyoutPadDip);

        if (_entries.Count == 0)
        {
            if (_emptyFont != null)
            {
                Rectangle r = new Rectangle(side, Px(16), Math.Max(8, _canvas.Width - side * 2), Math.Max(40, _viewport.Height - Px(32)));
                TextRenderer.DrawText(g,
                    "No recent items yet",
                    _emptyFont,
                    r,
                    UiTheme.TextSecondary,
                    TextFormatFlags.WordBreak | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
            return;
        }

        int chipR = Math.Max(4, Px(ChromeVisual.ChipCornerDip) - 2);
        int iconSize = Px(ChromeLayout.PinsFlyoutIconDip);
        int insetX = side;
        int insetY = Px(2);
        int gap = Px(10);
        DateTime nowUtc = DateTime.UtcNow;
        string activeNorm = _active >= 0 && _active < _entries.Count
            ? PinStore.Normalize(_entries[_active].Path)
            : null;

        for (int i = 0; i < _rows.Count; i++)
        {
            RowHit hit = _rows[i];
            if (hit.IsSection)
            {
                if (_sectionFont != null)
                {
                    Rectangle label = new Rectangle(
                        insetX,
                        hit.Bounds.Y,
                        Math.Max(8, hit.Bounds.Width - insetX * 2),
                        hit.Bounds.Height);
                    TextRenderer.DrawText(g, hit.SectionTitle ?? "", _sectionFont, label, UiTheme.Muted,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
                }
                continue;
            }

            RecentEntry entry = hit.Entry;
            bool isHot = i == _hot;
            bool isActive = activeNorm != null
                && string.Equals(PinStore.Normalize(entry.Path), activeNorm, StringComparison.OrdinalIgnoreCase);

            Rectangle chip = new Rectangle(
                insetX,
                hit.Bounds.Y + insetY,
                Math.Max(8, hit.Bounds.Width - insetX * 2),
                hit.Bounds.Height - insetY * 2);
            if (isActive)
                ChromeVisual.FillRound(g, chip, chipR, UiTheme.Raised);
            else if (isHot)
                ChromeVisual.FillRound(g, chip, chipR, UiTheme.Hover);

            Bitmap bmp = IconCache.GetForPath(entry.Path, entry.IsDirectory, false);
            int ix = chip.X + Px(8);
            int iy = chip.Y + (chip.Height - iconSize) / 2;
            if (bmp != null)
                g.DrawImage(bmp, new Rectangle(ix, iy, iconSize, iconSize));

            string meta = RecentSource.FormatRelativeTime(entry.LastAccessUtc, nowUtc);
            int metaW = 0;
            if (_metaFont != null && !string.IsNullOrEmpty(meta))
            {
                Size mz = TextRenderer.MeasureText(meta, _metaFont, new Size(int.MaxValue, int.MaxValue),
                    TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
                metaW = Math.Min(chip.Width / 3, mz.Width + Px(4));
                Rectangle metaR = new Rectangle(chip.Right - metaW - Px(8), chip.Y, metaW, chip.Height);
                TextRenderer.DrawText(g, meta, _metaFont, metaR, UiTheme.Muted,
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
            }

            if (_labelFont != null)
            {
                int tx = ix + iconSize + gap;
                int tw = Math.Max(8, chip.Right - tx - metaW - Px(12));
                Rectangle titleR = new Rectangle(tx, chip.Y, tw, chip.Height);
                Color ink = isActive ? UiTheme.Accent : UiTheme.Text;
                TextRenderer.DrawText(g, entry.Title ?? "", _labelFont, titleR, ink,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
            }
        }
    }

    private void OnCanvasMouseMove(object sender, MouseEventArgs e)
    {
        _session.NoteActivity();
        int index = HitIndex(e.Location);
        if (index != _hot)
        {
            _hot = index;
            _canvas.Invalidate();
            if (index >= 0 && index < _rows.Count && !_rows[index].IsSection && _rows[index].Entry != null)
                _tip.SetToolTip(_canvas, _rows[index].Entry.Path);
            else
                _tip.SetToolTip(_canvas, "");
        }
    }

    private void OnCanvasMouseLeave(object sender, EventArgs e)
    {
        if (_hot != -1)
        {
            _hot = -1;
            _canvas.Invalidate();
        }
    }

    private void OnCanvasMouseClick(object sender, MouseEventArgs e)
    {
        _session.NoteActivity();
        int index = HitIndex(e.Location);
        if (index < 0 || index >= _rows.Count || _rows[index].IsSection)
            return;
        if (e.Button == MouseButtons.Left)
            ActivateHit(index);
        else if (e.Button == MouseButtons.Right)
        {
            _menuIndex = index;
            _menu.Show(_canvas, e.Location);
        }
    }

    private void ActivateHit(int rowIndex)
    {
        if (rowIndex < 0 || rowIndex >= _rows.Count || _rows[rowIndex].IsSection)
            return;
        RecentEntry entry = _rows[rowIndex].Entry;
        if (entry == null)
            return;
        _session.OnRecentItemClicked(entry.Path);
    }

    private void ShowInExplorerMenu()
    {
        if (_menuIndex < 0 || _menuIndex >= _rows.Count || _rows[_menuIndex].IsSection)
            return;
        string path = _rows[_menuIndex].Entry.Path;
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
        if (_menuIndex < 0 || _menuIndex >= _rows.Count || _rows[_menuIndex].IsSection)
            return;
        try
        {
            Clipboard.SetText(_rows[_menuIndex].Entry.Path ?? "");
        }
        catch
        {
        }
    }

    private int HitIndex(Point pt)
    {
        for (int i = 0; i < _rows.Count; i++)
        {
            if (_rows[i].Bounds.Contains(pt))
                return i;
        }
        return -1;
    }

    private uint NativeDpi()
    {
        try { return GetDpiForWindow(Handle); }
        catch { return 96; }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);

    private sealed class CanvasPanel : Panel
    {
        public CanvasPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            UpdateStyles();
        }
    }
}
