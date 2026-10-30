using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

/// <summary>
/// Anchored view-mode picker — Tiles / Content / Details + Show hidden — matching pane chrome.
/// </summary>
internal sealed class ViewMenuForm : Form
{
    private static readonly BrowseViewMode[] Modes = new BrowseViewMode[]
    {
        BrowseViewMode.Tiles,
        BrowseViewMode.Content,
        BrowseViewMode.Details
    };

    private readonly BrowseViewMode _current;
    private readonly Image[] _icons = new Image[3];
    private readonly Rectangle[] _modeHits = new Rectangle[3];
    private Rectangle _hiddenHit;
    private Rectangle _sepRect;
    private bool _showHidden;
    private Font _labelFont;
    private float _dpi = 96f;
    private float _fit = 1f;
    private int _hot = -1; // 0..2 modes, 3 = hidden
    private int _suspendClose;
    private bool _chosen;

    public event Action<BrowseViewMode> ModeChosen;
    public event Action<bool> ShowHiddenChanged;

    public ViewMenuForm(BrowseViewMode current, bool showHidden, float dpi, float fit)
    {
        _current = current;
        _showHidden = showHidden;
        _dpi = dpi < 96f ? 96f : dpi;
        _fit = fit <= 0f ? 1f : fit;

        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = UiTheme.Bg;
        Opacity = 1.0;
        DoubleBuffered = true;
        Text = "View";
        AutoScaleMode = AutoScaleMode.None;
        KeyPreview = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);

        float textFit = 1f;
        float em = ChromeLayout.FontEm(_fit, textFit);
        _labelFont = ChromeFonts.CreateLabel(ChromeLayout.UiPt - 0.25f, em);

        int icon = Math.Max(16, Px(18));
        _icons[0] = ChromeIcons.TilesView(icon);
        _icons[1] = ChromeIcons.ContentView(icon);
        _icons[2] = ChromeIcons.DetailsView(icon);

        LayoutHits();

        KeyDown += OnKeyDown;
        Deactivate += OnDeactivate;
        MouseMove += OnMenuMouseMove;
        MouseLeave += OnMenuMouseLeave;
        MouseClick += OnMenuMouseClick;
    }

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            ShellChrome.Prepare(cp);
            cp.ExStyle |= 0x00000008; // WS_EX_TOPMOST
            return cp;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ChromeVisual.PreferDarkMode(Handle);
        ChromeVisual.ApplyRoundedRegion(this, Px(ChromeVisual.CornerDip - 2));
        ShellChrome.Pin(Handle);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        if (_labelFont != null) { _labelFont.Dispose(); _labelFont = null; }
        for (int i = 0; i < _icons.Length; i++)
        {
            if (_icons[i] != null)
            {
                _icons[i].Dispose();
                _icons[i] = null;
            }
        }
        base.OnFormClosed(e);
    }

    public void PlaceBelow(Control anchor)
    {
        if (anchor == null || anchor.IsDisposed)
            return;
        LayoutHits();
        Point below = anchor.PointToScreen(new Point(0, anchor.Height));
        Rectangle work = Screen.FromControl(anchor).WorkingArea;
        int gap = Px(6);
        int clear = Px(ChromeLayout.EdgeClearDip);
        int x = below.X + anchor.Width - Width;
        int y = below.Y + gap;
        if (x < work.Left + Px(8))
            x = work.Left + Px(8);
        if (x + Width > work.Right - clear)
            x = Math.Max(work.Left + Px(8), work.Right - clear - Width);
        if (y + Height > work.Bottom - Px(8))
            y = below.Y - gap - Height;
        if (y < work.Top + Px(8))
            y = work.Top + Px(8);
        Location = new Point(x, y);
    }

    private int Px(int dip)
    {
        return DisplayScale.Px(dip, _dpi, _fit);
    }

    private void LayoutHits()
    {
        int pad = Px(8);
        int row = Math.Max(Px(34), Px(ChromeLayout.PinsFlyoutRowDip));
        int sep = Px(9);
        int width = Px(ChromeLayout.ViewMenuWidthDip);
        Width = width;
        Height = pad * 2 + row * Modes.Length + sep + row;
        int y = pad;
        for (int i = 0; i < Modes.Length; i++)
        {
            _modeHits[i] = new Rectangle(pad, y, width - pad * 2, row);
            y += row;
        }
        _sepRect = new Rectangle(pad + Px(4), y + sep / 2, width - pad * 2 - Px(8), 1);
        y += sep;
        _hiddenHit = new Rectangle(pad, y, width - pad * 2, row);
        if (IsHandleCreated)
            ChromeVisual.ApplyRoundedRegion(this, Px(ChromeVisual.CornerDip - 2));
    }

    private static string LabelFor(BrowseViewMode mode)
    {
        switch (mode)
        {
            case BrowseViewMode.Content: return "Content";
            case BrowseViewMode.Details: return "Details";
            default: return "Tiles";
        }
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            e.Handled = true;
            Close();
        }
    }

    private void OnDeactivate(object sender, EventArgs e)
    {
        if (_suspendClose != 0 || _chosen)
            return;
        Timer t = new Timer();
        t.Interval = 160;
        t.Tick += delegate
        {
            t.Stop();
            t.Dispose();
            if (_suspendClose == 0 && !_chosen && !IsDisposed && Form.ActiveForm != this)
                Close();
        };
        t.Start();
    }

    private void OnMenuMouseMove(object sender, MouseEventArgs e)
    {
        int next = HitIndex(e.Location);
        if (next == _hot)
            return;
        _hot = next;
        Invalidate();
    }

    private void OnMenuMouseLeave(object sender, EventArgs e)
    {
        if (_hot < 0)
            return;
        _hot = -1;
        Invalidate();
    }

    private void OnMenuMouseClick(object sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
            return;
        int idx = HitIndex(e.Location);
        if (idx < 0)
            return;
        if (idx == 3)
        {
            ToggleHidden();
            return;
        }
        ChooseMode(Modes[idx]);
    }

    private int HitIndex(Point pt)
    {
        for (int i = 0; i < _modeHits.Length; i++)
        {
            if (_modeHits[i].Contains(pt))
                return i;
        }
        if (_hiddenHit.Contains(pt))
            return 3;
        return -1;
    }

    private void ChooseMode(BrowseViewMode mode)
    {
        _chosen = true;
        _suspendClose++;
        try
        {
            Action<BrowseViewMode> handler = ModeChosen;
            if (handler != null)
                handler(mode);
        }
        finally
        {
            _suspendClose--;
            Close();
        }
    }

    private void ToggleHidden()
    {
        _showHidden = !_showHidden;
        Action<bool> handler = ShowHiddenChanged;
        if (handler != null)
            handler(_showHidden);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(UiTheme.Bg);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        int corner = Px(ChromeVisual.CornerDip - 2);
        ChromeVisual.DrawRoundBorder(g, new Rectangle(0, 0, Width, Height), corner, UiTheme.BorderSoft);

        int chipR = Math.Max(4, Px(ChromeVisual.ChipCornerDip) - 2);
        int iconPad = Px(10);
        int gap = Px(10);
        for (int i = 0; i < Modes.Length; i++)
        {
            Rectangle hit = _modeHits[i];
            bool selected = Modes[i] == _current;
            bool hot = i == _hot;
            if (selected)
                ChromeVisual.FillRound(g, hit, chipR, Color.FromArgb(255, 0x2F, 0x3A, 0x48));
            else if (hot)
                ChromeVisual.FillRound(g, hit, chipR, UiTheme.Hover);

            Image icon = _icons[i];
            int ix = hit.X + iconPad;
            if (icon != null)
            {
                int iy = hit.Y + (hit.Height - icon.Height) / 2;
                g.DrawImage(icon, new Rectangle(ix, iy, icon.Width, icon.Height));
                ix += icon.Width + gap;
            }

            Color ink = selected ? UiTheme.Accent : UiTheme.Text;
            Rectangle textRect = new Rectangle(ix, hit.Y, Math.Max(8, hit.Right - ix - Px(10)), hit.Height);
            TextRenderer.DrawText(
                g,
                LabelFor(Modes[i]),
                _labelFont,
                textRect,
                ink,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);

            if (selected)
                DrawCheck(g, hit);
        }

        using (SolidBrush sep = new SolidBrush(UiTheme.Border))
            g.FillRectangle(sep, _sepRect);

        bool hiddenHot = _hot == 3;
        if (_showHidden)
            ChromeVisual.FillRound(g, _hiddenHit, chipR, Color.FromArgb(255, 0x2F, 0x3A, 0x48));
        else if (hiddenHot)
            ChromeVisual.FillRound(g, _hiddenHit, chipR, UiTheme.Hover);

        Color hiddenInk = _showHidden ? UiTheme.Accent : UiTheme.Text;
        TextRenderer.DrawText(
            g,
            "Show hidden items",
            _labelFont,
            new Rectangle(_hiddenHit.X + iconPad, _hiddenHit.Y, Math.Max(8, _hiddenHit.Width - iconPad - Px(28)), _hiddenHit.Height),
            hiddenInk,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
        if (_showHidden)
            DrawCheck(g, _hiddenHit);
    }

    private void DrawCheck(Graphics g, Rectangle hit)
    {
        using (Pen pen = new Pen(UiTheme.Accent, 1.4f))
        {
            pen.StartCap = LineCap.Round;
            pen.EndCap = LineCap.Round;
            int cx = hit.Right - Px(18);
            int cy = hit.Y + hit.Height / 2;
            g.DrawLines(pen, new Point[]
            {
                new Point(cx - Px(5), cy),
                new Point(cx - Px(1), cy + Px(4)),
                new Point(cx + Px(6), cy - Px(5))
            });
        }
    }
}
