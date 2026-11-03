using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

/// <summary>
/// Centered in-pane overlay for loading / empty / error / no-filter-matches.
/// Loading uses a pulsing shade + sweeping highlight so slow folders are obvious.
/// </summary>
internal sealed class BrowseListOverlay : Panel
{
    private readonly Label _title;
    private readonly Label _detail;
    private readonly ChromeToolButton _retryBtn;
    private readonly Timer _pulseTimer;
    private BrowseOverlayKind _kind;
    private string _detailText = "";
    private Font _titleFont;
    private Font _detailFont;
    private float _pulsePhase;

    public event Action RetryRequested;

    public BrowseListOverlay()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        BackColor = UiTheme.Bg;
        Visible = false;
        TabStop = false;

        _title = new Label();
        _title.AutoSize = false;
        _title.TextAlign = ContentAlignment.MiddleCenter;
        _title.ForeColor = UiTheme.Text;
        _title.BackColor = Color.Transparent;

        _detail = new Label();
        _detail.AutoSize = false;
        _detail.TextAlign = ContentAlignment.TopCenter;
        _detail.ForeColor = UiTheme.TextSecondary;
        _detail.BackColor = Color.Transparent;

        _retryBtn = new ChromeToolButton();
        _retryBtn.Text = "Retry";
        _retryBtn.Visible = false;
        _retryBtn.Click += delegate
        {
            if (RetryRequested != null)
                RetryRequested();
        };

        _pulseTimer = new Timer();
        _pulseTimer.Interval = 33;
        _pulseTimer.Tick += delegate
        {
            if (_kind != BrowseOverlayKind.Loading || !Visible)
            {
                _pulseTimer.Stop();
                return;
            }
            _pulsePhase += 0.08f;
            if (_pulsePhase > (float)(Math.PI * 2))
                _pulsePhase -= (float)(Math.PI * 2);
            Invalidate();
        };

        Controls.Add(_retryBtn);
        Controls.Add(_detail);
        Controls.Add(_title);
    }

    public BrowseOverlayKind Kind
    {
        get { return _kind; }
    }

    public void ApplyScale(float dpi, float fit, Font uiFont)
    {
        int titlePt = Math.Max(11, (int)Math.Round(13f * fit));
        int detailPt = Math.Max(9, (int)Math.Round(11f * fit));
        Font title = new Font(uiFont != null ? uiFont.FontFamily : SystemFonts.MessageBoxFont.FontFamily, titlePt, FontStyle.Regular);
        Font detail = new Font(uiFont != null ? uiFont.FontFamily : SystemFonts.MessageBoxFont.FontFamily, detailPt, FontStyle.Regular);
        if (_titleFont != null)
        {
            try { _titleFont.Dispose(); } catch { }
        }
        if (_detailFont != null)
        {
            try { _detailFont.Dispose(); } catch { }
        }
        _titleFont = title;
        _detailFont = detail;
        _title.Font = _titleFont;
        _detail.Font = _detailFont;
        int btnH = DisplayScale.Px(ChromeLayout.PaneChromeBtnDip, dpi, fit);
        int btnW = DisplayScale.Px(88, dpi, fit);
        _retryBtn.Size = new Size(btnW, btnH);
        _retryBtn.Font = detail;
        LayoutChildren();
    }

    public void ShowState(BrowseOverlayKind kind, string detail)
    {
        _kind = kind;
        _detailText = detail ?? "";
        if (kind == BrowseOverlayKind.None)
        {
            _pulseTimer.Stop();
            Visible = false;
            return;
        }

        _title.Text = BrowseOverlayRules.Title(kind);
        if (kind == BrowseOverlayKind.Error)
            _detail.Text = string.IsNullOrEmpty(_detailText) ? "Check permissions or try again." : _detailText;
        else if (kind == BrowseOverlayKind.NoMatches)
            _detail.Text = string.IsNullOrEmpty(_detailText) ? "Clear the filter to see all items." : _detailText;
        else if (kind == BrowseOverlayKind.Empty)
            _detail.Text = string.IsNullOrEmpty(_detailText) ? "Drop files here or create a new folder." : _detailText;
        else if (kind == BrowseOverlayKind.Loading)
            _detail.Text = string.IsNullOrEmpty(_detailText) ? "Reading folder contents" : _detailText;
        else
            _detail.Text = _detailText;

        _retryBtn.Visible = BrowseOverlayRules.ShowsRetry(kind);
        Visible = true;
        BringToFront();
        LayoutChildren();
        if (kind == BrowseOverlayKind.Loading)
        {
            if (!_pulseTimer.Enabled)
                _pulseTimer.Start();
        }
        else
            _pulseTimer.Stop();
        Invalidate();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        LayoutChildren();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (_kind != BrowseOverlayKind.Loading)
            return;

        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        Rectangle bounds = ClientRectangle;

        // Soft pulsing wash over the blank list so loading is never a static empty pane.
        float pulse = (float)((Math.Sin(_pulsePhase) + 1.0) * 0.5); // 0..1
        int washAlpha = 18 + (int)(22 * pulse);
        using (SolidBrush wash = new SolidBrush(Color.FromArgb(washAlpha, UiTheme.Accent)))
            g.FillRectangle(wash, bounds);

        int cardW = Math.Min(bounds.Width - 24, 300);
        int cardH = Math.Min(bounds.Height - 24, 110);
        if (cardW < 80 || cardH < 48)
            return;
        Rectangle card = new Rectangle((bounds.Width - cardW) / 2, (bounds.Height - cardH) / 2, cardW, cardH);
        int r = 14;
        int cardAlpha = 230 + (int)(25 * pulse);
        if (cardAlpha > 255) cardAlpha = 255;
        using (GraphicsPath path = RoundRect(card, r))
        using (SolidBrush fill = new SolidBrush(Color.FromArgb(cardAlpha, UiTheme.Surface)))
        using (Pen border = new Pen(Color.FromArgb(160 + (int)(60 * pulse), UiTheme.Accent)))
        {
            g.FillPath(fill, path);
            g.DrawPath(border, path);
        }

        // Sweeping highlight bar inside the card (shading animation).
        int barH = Math.Max(4, card.Height / 10);
        int barY = card.Y + card.Height - barH - 14;
        Rectangle barTrack = new Rectangle(card.X + 18, barY, card.Width - 36, barH);
        using (SolidBrush track = new SolidBrush(UiTheme.Raised))
            g.FillRectangle(track, barTrack);

        float sweep = (_pulsePhase / (float)(Math.PI * 2));
        if (sweep < 0) sweep = 0;
        if (sweep > 1) sweep = 1;
        int barW = Math.Max(24, barTrack.Width / 3);
        int barX = barTrack.X + (int)((barTrack.Width - barW) * sweep);
        Rectangle bar = new Rectangle(barX, barTrack.Y, barW, barTrack.Height);
        using (LinearGradientBrush shine = new LinearGradientBrush(
            bar,
            Color.FromArgb(40, UiTheme.Accent),
            Color.FromArgb(220, UiTheme.Accent),
            LinearGradientMode.Horizontal))
        {
            g.FillRectangle(shine, bar);
        }
    }

    private void LayoutChildren()
    {
        if (Width <= 0 || Height <= 0)
            return;
        int pad = 24;
        int titleH = 28;
        int detailH = 40;
        int gap = 8;
        // Leave room at bottom of the centered card for the animated bar.
        int loadingExtra = _kind == BrowseOverlayKind.Loading ? 22 : 0;
        int contentH = titleH + gap + detailH + loadingExtra;
        if (_retryBtn.Visible)
            contentH += gap + _retryBtn.Height;
        int top = Math.Max(pad, (Height - contentH) / 2 - (_kind == BrowseOverlayKind.Loading ? 8 : 0));
        int width = Math.Max(40, Width - pad * 2);
        _title.SetBounds(pad, top, width, titleH);
        _detail.SetBounds(pad, _title.Bottom + gap, width, detailH);
        if (_retryBtn.Visible)
        {
            int bx = (Width - _retryBtn.Width) / 2;
            _retryBtn.SetBounds(bx, _detail.Bottom + gap, _retryBtn.Width, _retryBtn.Height);
        }
    }

    private static GraphicsPath RoundRect(Rectangle r, int radius)
    {
        GraphicsPath path = new GraphicsPath();
        int d = radius * 2;
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_pulseTimer != null)
            {
                _pulseTimer.Stop();
                _pulseTimer.Dispose();
            }
            if (_titleFont != null)
            {
                try { _titleFont.Dispose(); } catch { }
                _titleFont = null;
            }
            if (_detailFont != null)
            {
                try { _detailFont.Dispose(); } catch { }
                _detailFont = null;
            }
        }
        base.Dispose(disposing);
    }
}
