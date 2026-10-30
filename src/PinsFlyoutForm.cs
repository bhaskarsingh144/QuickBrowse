using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Diagnostics;
using System.Windows.Forms;

internal enum ListFlyoutMode
{
    Pins,
    Recent
}

/// <summary>
/// Compact list flyout beside the rail — Pins or Recent, same dense chrome.
/// </summary>
internal sealed class PinsFlyoutForm : Form
{
    private readonly ShellHost _session;
    private readonly List<PinEntry> _entries = new List<PinEntry>();
    private readonly List<Rectangle> _hits = new List<Rectangle>();
    private readonly ContextMenuStrip _menu;
    private readonly ToolStripMenuItem _openItem;
    private readonly ToolStripMenuItem _unpinItem;
    private readonly ToolStripMenuItem _explorerItem;
    private readonly ToolTip _tip;
    private Font _labelFont;
    private Font _emptyFont;
    private float _dpi = 96f;
    private float _fit = 1f;
    private int _hot = -1;
    private int _active = -1;
    private int _menuIndex = -1;
    private ListFlyoutMode _mode = ListFlyoutMode.Pins;

    public PinsFlyoutForm(ShellHost session)
    {
        _session = session;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = false;
        BackColor = UiTheme.Bg;
        Opacity = 0.0;
        DoubleBuffered = true;
        Text = "QuickBrowse Pins";
        AutoScaleMode = AutoScaleMode.None;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        _tip = new ToolTip();
        _tip.Popup += delegate
        {
            BeginInvoke(new MethodInvoker(ShellChrome.ElevateProcessToolTips));
        };
        _menu = new ContextMenuStrip();
        _openItem = new ToolStripMenuItem("Open", null, delegate { OpenMenuEntry(); });
        _unpinItem = new ToolStripMenuItem("Unpin", null, delegate { UnpinMenuEntry(); });
        _explorerItem = new ToolStripMenuItem("Show in Explorer", null, delegate { ShowInExplorerMenu(); });
        _menu.Items.Add(_openItem);
        _menu.Items.Add(_unpinItem);
        _menu.Items.Add(_explorerItem);
    }

    public ListFlyoutMode Mode { get { return _mode; } }

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
        if (_tip != null) { _tip.Dispose(); }
        if (_menu != null) { _menu.Dispose(); }
        if (_labelFont != null) { _labelFont.Dispose(); }
        if (_emptyFont != null) { _emptyFont.Dispose(); }
        base.OnFormClosed(e);
    }

    private int Px(int dip)
    {
        return DisplayScale.Px(dip, _dpi, _fit);
    }

    private int RowHeight()
    {
        return Math.Max(Px(28), Px(ChromeLayout.PinsFlyoutRowDip));
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
        if (_labelFont != null) _labelFont.Dispose();
        if (_emptyFont != null) _emptyFont.Dispose();
        _labelFont = ChromeFonts.CreateLabel(ChromeLayout.UiPt - 0.5f, em);
        _emptyFont = ChromeFonts.CreateUi(ChromeLayout.MetricMinPt + 1f, em);
        Width = Px(ChromeLayout.PinsFlyoutWidthDip);
        LayoutHits();
        ChromeVisual.ApplyRoundedRegion(this, Px(ChromeVisual.CornerDip));
        Invalidate();
    }

    public void SetMode(ListFlyoutMode mode)
    {
        _mode = mode;
        Text = mode == ListFlyoutMode.Recent ? "QuickBrowse Recent" : "QuickBrowse Pins";
        _unpinItem.Visible = mode == ListFlyoutMode.Pins;
        _explorerItem.Visible = mode == ListFlyoutMode.Recent;
        Invalidate();
    }

    public void SetPins(IList<PinEntry> pins, string activePath)
    {
        SetMode(ListFlyoutMode.Pins);
        SetEntries(pins, activePath);
    }

    public void SetEntries(IList<PinEntry> entries, string activePath)
    {
        _entries.Clear();
        if (entries != null)
        {
            for (int i = 0; i < entries.Count; i++)
                _entries.Add(entries[i]);
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
        LayoutHits();
        Invalidate();
    }

    public int DesiredHeight()
    {
        int pad = Px(ChromeLayout.PinsFlyoutPadDip);
        int row = RowHeight();
        int count = Math.Max(1, Math.Min(_entries.Count, ChromeLayout.PinsFlyoutMaxVisible));
        if (_entries.Count == 0)
            count = 2;
        return pad * 2 + count * row;
    }

    private uint NativeDpi()
    {
        try { return GetDpiForWindow(Handle); }
        catch { return 96; }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);

    private void LayoutHits()
    {
        _hits.Clear();
        int pad = Px(ChromeLayout.PinsFlyoutPadDip);
        int row = RowHeight();
        int y = pad;
        int visible = Math.Min(_entries.Count, ChromeLayout.PinsFlyoutMaxVisible);
        for (int i = 0; i < visible; i++)
        {
            _hits.Add(new Rectangle(0, y, Width, row));
            y += row;
        }
        int wantH = DesiredHeight();
        if (Height != wantH)
            Height = wantH;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(UiTheme.Bg);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        int corner = Px(ChromeVisual.CornerDip);
        ChromeVisual.DrawRoundBorder(g, new Rectangle(0, 0, Width, Height), corner, UiTheme.BorderSoft);

        if (_entries.Count == 0)
        {
            if (_emptyFont != null)
            {
                Rectangle r = new Rectangle(
                    Px(10),
                    Px(12),
                    Math.Max(8, Width - Px(20)),
                    Height - Px(24));
                string empty = _mode == ListFlyoutMode.Recent
                    ? "No recent items yet"
                    : "No pins yet — open a folder and pin it";
                TextRenderer.DrawText(g,
                    empty,
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
        for (int i = 0; i < _hits.Count && i < _entries.Count; i++)
        {
            Rectangle hit = _hits[i];
            Rectangle chip = Rectangle.Inflate(hit, -inset, -Px(2));
            if (i == _active)
                ChromeVisual.FillRound(g, chip, chipR, UiTheme.Raised);
            else if (i == _hot)
                ChromeVisual.FillRound(g, chip, chipR, UiTheme.Hover);

            PinEntry entry = _entries[i];
            bool isDir = entry.IsDirectory;
            Bitmap bmp = IconCache.GetForPath(entry.Path, isDir, false);
            int ix = chip.X + Px(6);
            int iy = chip.Y + (chip.Height - iconSize) / 2;
            if (bmp != null)
                g.DrawImage(bmp, new Rectangle(ix, iy, iconSize, iconSize));

            if (_labelFont != null)
            {
                int tx = ix + iconSize + gap;
                Rectangle label = new Rectangle(tx, chip.Y, Math.Max(8, chip.Right - tx - Px(6)), chip.Height);
                Color ink = i == _active ? UiTheme.Accent : UiTheme.Text;
                TextRenderer.DrawText(g, entry.Title ?? "", _labelFont, label, ink,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
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
            if (index >= 0 && index < _entries.Count)
                _tip.SetToolTip(this, _entries[index].Path);
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
        int index = HitIndex(e.Location);
        if (index < 0 || index >= _entries.Count)
            return;
        if (e.Button == MouseButtons.Left)
            ActivateIndex(index);
        else if (e.Button == MouseButtons.Right)
        {
            _menuIndex = index;
            _unpinItem.Visible = _mode == ListFlyoutMode.Pins;
            _explorerItem.Visible = _mode == ListFlyoutMode.Recent;
            _menu.Show(this, e.Location);
        }
    }

    protected override void WndProc(ref Message m)
    {
        if (ShellChrome.SwallowMinimize(ref m))
            return;
        base.WndProc(ref m);
    }

    private void ActivateIndex(int index)
    {
        if (index < 0 || index >= _entries.Count)
            return;
        string path = _entries[index].Path;
        if (_mode == ListFlyoutMode.Recent)
            _session.OnRecentItemClicked(path);
        else
            _session.OnPinClicked(path);
    }

    private void OpenMenuEntry()
    {
        ActivateIndex(_menuIndex);
    }

    private void UnpinMenuEntry()
    {
        if (_menuIndex >= 0 && _menuIndex < _entries.Count)
            _session.OnUnpinPath(_entries[_menuIndex].Path);
    }

    private void ShowInExplorerMenu()
    {
        if (_menuIndex < 0 || _menuIndex >= _entries.Count)
            return;
        string path = _entries[_menuIndex].Path;
        try
        {
            Process.Start("explorer.exe", "/select,\"" + path + "\"");
        }
        catch
        {
        }
    }

    private int HitIndex(Point pt)
    {
        for (int i = 0; i < _hits.Count; i++)
            if (_hits[i].Contains(pt)) return i;
        return -1;
    }
}
