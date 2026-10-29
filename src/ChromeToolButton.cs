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
            int gw = _glyph.Width;
            int gh = _glyph.Height;
            int x = (Width - gw) / 2;
            int y = (Height - gh) / 2;
            // Never dispose _glyph here — disabled Back/Up used to `using` it and then
            // painted a red-X placeholder on the next frame.
            g.DrawImage(_glyph, x, y, gw, gh);
            if (!Enabled)
            {
                using (SolidBrush dim = new SolidBrush(Color.FromArgb(140, Parent != null ? Parent.BackColor : UiTheme.Surface)))
                    g.FillRectangle(dim, x, y, gw, gh);
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
