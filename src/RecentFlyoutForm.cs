using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

/// <summary>
/// Dense Recent list beside the rail — date sections, title + relative time, scrollable canvas.
/// (Not a pane-sized icon grid.)
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
    private readonly Panel _scroll;
    private readonly CanvasPanel _canvas;
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
    private int _scrollPreserve;

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

        _scroll = new Panel();
        _scroll.Dock = DockStyle.Fill;
        _scroll.AutoScroll = true;
        _scroll.BackColor = UiTheme.Bg;
        _scroll.Padding = new Padding(0);
        Controls.Add(_scroll);

        _canvas = new CanvasPanel();
        _canvas.BackColor = UiTheme.Bg;
        _canvas.Paint += OnCanvasPaint;
        _canvas.MouseMove += OnCanvasMouseMove;
        _canvas.MouseLeave += OnCanvasMouseLeave;
        _canvas.MouseClick += OnCanvasMouseClick;
        _scroll.Controls.Add(_canvas);

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
        ChromeVisual.PreferDarkScrollBars(this);
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
        _sectionFont = ChromeFonts.CreateLabel(ChromeLayout.MetricMinPt + 1f, em);
        _labelFont = ChromeFonts.CreateLabel(ChromeLayout.UiPt - 0.5f, em);
        _metaFont = ChromeFonts.CreateUi(ChromeLayout.MetricMinPt + 0.5f, em);
        _emptyFont = ChromeFonts.CreateUi(ChromeLayout.MetricMinPt + 1f, em);

        Width = Px(ChromeLayout.RecentFlyoutWidthDip);
        LayoutRows();
        ChromeVisual.ApplyRoundedRegion(this, Px(ChromeVisual.CornerDip));
        Invalidate();
    }

    public void SetEntries(IList<RecentEntry> entries, string activePath)
    {
        _scrollPreserve = -_scroll.AutoScrollPosition.Y;
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
        try
        {
            if (_scrollPreserve > 0)
                _scroll.AutoScrollPosition = new Point(0, _scrollPreserve);
        }
        catch { }
        _canvas.Invalidate();
    }

    /// <summary>Outer form height capped so the list scrolls inside.</summary>
    public int DesiredHeight()
    {
        int pad = Px(ChromeLayout.PinsFlyoutPadDip);
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
        return Math.Max(Px(28), Px(ChromeLayout.PinsFlyoutRowDip));
    }

    private int SectionHeight()
    {
        return Math.Max(Px(20), Px(ChromeLayout.RecentSectionHeaderDip));
    }

    private void LayoutRows()
    {
        _rows.Clear();
        int pad = Px(ChromeLayout.PinsFlyoutPadDip);
        int rowH = RowHeight();
        int sectionH = SectionHeight();
        int y = pad;
        int innerW = Math.Max(8, Width - SystemInformation.VerticalScrollBarWidth);

        if (_entries.Count == 0)
        {
            _canvas.Size = new Size(innerW, pad * 2 + rowH * 2);
            Height = DesiredHeight();
            return;
        }

        IList<RecentBucketGroup> groups = RecentSource.GroupByDateBucket(_entries);
        for (int g = 0; g < groups.Count; g++)
        {
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

        Height = DesiredHeight();
        int clientW = Math.Max(8, _scroll.ClientSize.Width > 0 ? _scroll.ClientSize.Width : innerW);
        _canvas.Location = new Point(0, 0);
        _canvas.Size = new Size(clientW, y + pad);
        for (int i = 0; i < _rows.Count; i++)
            _rows[i].Bounds = new Rectangle(0, _rows[i].Bounds.Y, clientW, _rows[i].Bounds.Height);
    }

    private void OnCanvasPaint(object sender, PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(UiTheme.Bg);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

        if (_entries.Count == 0)
        {
            if (_emptyFont != null)
            {
                Rectangle r = new Rectangle(Px(10), Px(12), Math.Max(8, _canvas.Width - Px(20)), _canvas.Height - Px(24));
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
        int inset = Px(6);
        int gap = Px(8);
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
                    Rectangle label = new Rectangle(Px(10), hit.Bounds.Y, Math.Max(8, hit.Bounds.Width - Px(16)), hit.Bounds.Height);
                    TextRenderer.DrawText(g, hit.SectionTitle ?? "", _sectionFont, label, UiTheme.Muted,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
                }
                continue;
            }

            RecentEntry entry = hit.Entry;
            bool isHot = i == _hot;
            bool isActive = activeNorm != null
                && string.Equals(PinStore.Normalize(entry.Path), activeNorm, StringComparison.OrdinalIgnoreCase);

            Rectangle chip = Rectangle.Inflate(hit.Bounds, -inset, -Px(2));
            if (isActive)
                ChromeVisual.FillRound(g, chip, chipR, UiTheme.Raised);
            else if (isHot)
                ChromeVisual.FillRound(g, chip, chipR, UiTheme.Hover);

            Bitmap bmp = IconCache.GetForPath(entry.Path, entry.IsDirectory, false);
            int ix = chip.X + Px(6);
            int iy = chip.Y + (chip.Height - iconSize) / 2;
            if (bmp != null)
                g.DrawImage(bmp, new Rectangle(ix, iy, iconSize, iconSize));

            string meta = RecentSource.FormatRelativeTime(entry.LastAccessUtc, nowUtc);
            int metaW = 0;
            if (_metaFont != null && !string.IsNullOrEmpty(meta))
            {
                Size mz = TextRenderer.MeasureText(meta, _metaFont, new Size(int.MaxValue, int.MaxValue),
                    TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
                metaW = Math.Min(chip.Width / 2, mz.Width + Px(4));
                Rectangle metaR = new Rectangle(chip.Right - metaW - Px(6), chip.Y, metaW, chip.Height);
                TextRenderer.DrawText(g, meta, _metaFont, metaR, UiTheme.Muted,
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
            }

            if (_labelFont != null)
            {
                int tx = ix + iconSize + gap;
                int tw = Math.Max(8, chip.Right - tx - metaW - Px(10));
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
