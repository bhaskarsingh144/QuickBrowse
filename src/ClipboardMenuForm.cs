using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

internal enum ClipboardMenuAction
{
    Copy,
    Cut,
    Paste
}

/// <summary>Anchored Copy / Cut / Paste menu matching pane chrome.</summary>
internal sealed class ClipboardMenuForm : Form
{
    private static readonly string[] Labels = new string[] { "Copy", "Cut", "Paste" };
    private static readonly ClipboardMenuAction[] Actions = new ClipboardMenuAction[]
    {
        ClipboardMenuAction.Copy,
        ClipboardMenuAction.Cut,
        ClipboardMenuAction.Paste
    };

    private readonly bool[] _enabled = new bool[3];
    private readonly Rectangle[] _hits = new Rectangle[3];
    private Font _labelFont;
    private float _dpi = 96f;
    private float _fit = 1f;
    private int _hot = -1;
    private int _suspendClose;
    private bool _chosen;

    public event Action<ClipboardMenuAction> ActionChosen;

    public ClipboardMenuForm(bool canCopyCut, bool canPaste, float dpi, float fit)
    {
        _enabled[0] = canCopyCut;
        _enabled[1] = canCopyCut;
        _enabled[2] = canPaste;
        _dpi = dpi < 96f ? 96f : dpi;
        _fit = fit <= 0f ? 1f : fit;

        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = UiTheme.Bg;
        Opacity = 1.0;
        DoubleBuffered = true;
        Text = "Clipboard";
        AutoScaleMode = AutoScaleMode.None;
        KeyPreview = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);

        float em = ChromeLayout.FontEm(_fit, 1f);
        _labelFont = ChromeFonts.CreateLabel(ChromeLayout.UiPt - 0.25f, em);
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
            cp.ExStyle |= 0x00000008;
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
        int x = below.X;
        int y = below.Y + gap;
        if (x + Width > work.Right - clear)
            x = Math.Max(work.Left + Px(8), work.Right - clear - Width);
        if (x < work.Left + Px(8))
            x = work.Left + Px(8);
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
        int row = Math.Max(Px(32), Px(ChromeLayout.PinsFlyoutRowDip));
        Width = Px(148);
        Height = pad * 2 + row * Actions.Length;
        int y = pad;
        for (int i = 0; i < Actions.Length; i++)
        {
            _hits[i] = new Rectangle(pad, y, Width - pad * 2, row);
            y += row;
        }
        if (IsHandleCreated)
            ChromeVisual.ApplyRoundedRegion(this, Px(ChromeVisual.CornerDip - 2));
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
        if (_hot < 0) return;
        _hot = -1;
        Invalidate();
    }

    private void OnMenuMouseClick(object sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
            return;
        int idx = HitIndex(e.Location);
        if (idx < 0 || !_enabled[idx])
            return;
        Choose(Actions[idx]);
    }

    private int HitIndex(Point pt)
    {
        for (int i = 0; i < _hits.Length; i++)
        {
            if (_hits[i].Contains(pt))
                return i;
        }
        return -1;
    }

    private void Choose(ClipboardMenuAction action)
    {
        _chosen = true;
        _suspendClose++;
        try
        {
            Action<ClipboardMenuAction> handler = ActionChosen;
            if (handler != null)
                handler(action);
        }
        finally
        {
            _suspendClose--;
            Close();
        }
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
        for (int i = 0; i < Actions.Length; i++)
        {
            Rectangle hit = _hits[i];
            bool enabled = _enabled[i];
            bool hot = enabled && i == _hot;
            if (hot)
                ChromeVisual.FillRound(g, hit, chipR, UiTheme.Hover);
            Color ink = enabled ? UiTheme.Text : UiTheme.Muted;
            TextRenderer.DrawText(
                g,
                Labels[i],
                _labelFont,
                new Rectangle(hit.X + Px(12), hit.Y, hit.Width - Px(16), hit.Height),
                ink,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
    }
}
