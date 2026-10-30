using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

/// <summary>
/// Rounded chrome chip — supports colored glyph icons and a selected (toggle) state.
/// </summary>
internal sealed class ChromeToolButton : Button
{
    private bool _hot;
    private bool _pressed;
    private bool _selected;
    private bool _showChevron;
    private Image _glyph;

    public ChromeToolButton()
    {
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        FlatAppearance.MouseOverBackColor = Color.Transparent;
        FlatAppearance.MouseDownBackColor = Color.Transparent;
        BackColor = Color.Transparent;
        ForeColor = UiTheme.Text;
        Cursor = Cursors.Hand;
        TabStop = false;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    public Image Glyph
    {
        get { return _glyph; }
        set
        {
            _glyph = value;
            Invalidate();
        }
    }

    public bool Selected
    {
        get { return _selected; }
        set
        {
            if (_selected == value) return;
            _selected = value;
            Invalidate();
        }
    }

    /// <summary>Draw a small chevron to the right of the glyph (dropdown affordance).</summary>
    public bool ShowChevron
    {
        get { return _showChevron; }
        set
        {
            if (_showChevron == value) return;
            _showChevron = value;
            Invalidate();
        }
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        _hot = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hot = false;
        _pressed = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            _pressed = true;
            Invalidate();
        }
        base.OnMouseDown(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        _pressed = false;
        Invalidate();
        base.OnMouseUp(e);
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        Invalidate();
        base.OnEnabledChanged(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Parent != null ? Parent.BackColor : UiTheme.Surface);

        Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
        int radius = Math.Max(4, Math.Min(Height / 3, 8));

        Color fill = UiTheme.Chip;
        if (!Enabled)
            fill = Color.FromArgb(160, UiTheme.Chip);
        else if (_selected)
            fill = Color.FromArgb(255, 0x2F, 0x3A, 0x48);
        else if (_pressed)
            fill = UiTheme.Press;
        else if (_hot)
            fill = UiTheme.Hover;

        using (GraphicsPath path = ChromeVisual.Round(r, radius))
        using (SolidBrush brush = new SolidBrush(fill))
            g.FillPath(brush, path);

        Color border = _selected ? UiTheme.Accent : (Enabled ? UiTheme.BorderSoft : UiTheme.Border);
        using (GraphicsPath path = ChromeVisual.Round(r, radius))
        using (Pen pen = new Pen(border, _selected ? 1.5f : 1f))
            g.DrawPath(pen, path);

        if (_glyph != null)
        {
            // Glyphs can be swapped/disposed on remasure while a paint is queued —
            // never let a dead Image take down the chrome.
            try
            {
                int gw = _glyph.Width;
                int gh = _glyph.Height;
                int chevronW = _showChevron ? Math.Max(8, Height / 5) : 0;
                int gap = _showChevron ? Math.Max(3, Height / 12) : 0;
                int contentW = gw + gap + chevronW;
                int x = (Width - contentW) / 2;
                int y = (Height - gh) / 2;
                g.DrawImage(_glyph, x, y, gw, gh);
                if (_showChevron)
                {
                    Color chevron = Enabled ? UiTheme.Muted : UiTheme.Border;
                    using (Pen pen = new Pen(chevron, Math.Max(1.2f, Height / 22f)))
                    {
                        pen.StartCap = LineCap.Round;
                        pen.EndCap = LineCap.Round;
                        float cx = x + gw + gap + chevronW * 0.45f;
                        float cy = Height * 0.5f;
                        float arm = Math.Max(2.5f, chevronW * 0.35f);
                        g.DrawLines(pen, new PointF[]
                        {
                            new PointF(cx - arm, cy - arm * 0.55f),
                            new PointF(cx, cy + arm * 0.45f),
                            new PointF(cx + arm, cy - arm * 0.55f)
                        });
                    }
                }
                if (!Enabled)
                {
                    using (SolidBrush dim = new SolidBrush(Color.FromArgb(140, Parent != null ? Parent.BackColor : UiTheme.Surface)))
                        g.FillRectangle(dim, x, y, contentW, gh);
                }
            }
            catch (ArgumentException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
            return;
        }

        Color text = Enabled ? ForeColor : UiTheme.Muted;
        TextRenderer.DrawText(
            g,
            Text,
            Font,
            ClientRectangle,
            text,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
    }
}
