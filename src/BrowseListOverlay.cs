using System;
using System.Drawing;
using System.Windows.Forms;

/// <summary>
/// Centered in-pane overlay for loading / empty / error / no-filter-matches.
/// Drawn above the list; does not steal focus.
/// </summary>
internal sealed class BrowseListOverlay : Panel
{
    private readonly Label _title;
    private readonly Label _detail;
    private readonly ChromeToolButton _retryBtn;
    private BrowseOverlayKind _kind;
    private string _detailText = "";
    private Font _titleFont;
    private Font _detailFont;

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
        if (_retryBtn.Font != null && _retryBtn.Font != SystemFonts.DefaultFont)
        {
            // keep button text readable
        }
        _retryBtn.Font = detail;
        LayoutChildren();
    }

    public void ShowState(BrowseOverlayKind kind, string detail)
    {
        _kind = kind;
        _detailText = detail ?? "";
        if (kind == BrowseOverlayKind.None)
        {
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
        else
            _detail.Text = _detailText;

        _retryBtn.Visible = BrowseOverlayRules.ShowsRetry(kind);
        Visible = true;
        BringToFront();
        LayoutChildren();
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
        if (_kind == BrowseOverlayKind.Loading)
        {
            // Soft raised pill behind the title so loading is obvious on a blank list.
            Rectangle bounds = ClientRectangle;
            int cardW = Math.Min(bounds.Width - 24, 280);
            int cardH = Math.Min(bounds.Height - 24, 96);
            if (cardW < 80 || cardH < 40)
                return;
            Rectangle card = new Rectangle((bounds.Width - cardW) / 2, (bounds.Height - cardH) / 2, cardW, cardH);
            using (SolidBrush fill = new SolidBrush(UiTheme.Surface))
            using (Pen border = new Pen(UiTheme.BorderSoft))
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                int r = 12;
                using (System.Drawing.Drawing2D.GraphicsPath path = RoundRect(card, r))
                {
                    e.Graphics.FillPath(fill, path);
                    e.Graphics.DrawPath(border, path);
                }
            }
        }
    }

    private void LayoutChildren()
    {
        if (Width <= 0 || Height <= 0)
            return;
        int pad = 24;
        int titleH = 28;
        int detailH = 48;
        int gap = 10;
        int contentH = titleH + gap + detailH;
        if (_retryBtn.Visible)
            contentH += gap + _retryBtn.Height;
        int top = Math.Max(pad, (Height - contentH) / 2);
        int width = Math.Max(40, Width - pad * 2);
        _title.SetBounds(pad, top, width, titleH);
        _detail.SetBounds(pad, _title.Bottom + gap, width, detailH);
        if (_retryBtn.Visible)
        {
            int bx = (Width - _retryBtn.Width) / 2;
            _retryBtn.SetBounds(bx, _detail.Bottom + gap, _retryBtn.Width, _retryBtn.Height);
        }
    }

    private static System.Drawing.Drawing2D.GraphicsPath RoundRect(Rectangle r, int radius)
    {
        System.Drawing.Drawing2D.GraphicsPath path = new System.Drawing.Drawing2D.GraphicsPath();
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
