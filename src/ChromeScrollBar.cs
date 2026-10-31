using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

/// <summary>
/// Thin QuickView-style vertical scrollbar — dark track, muted gray thumb (not native white).
/// </summary>
internal sealed class ChromeScrollBar : Control
{
    private int _minimum;
    private int _maximum = 100;
    private int _largeChange = 10;
    private int _value;
    private bool _dragging;
    private bool _thumbHot;
    private int _dragThumbOffset;
    private Rectangle _trackRect;
    private Rectangle _thumbRect;

    public ChromeScrollBar()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.UserPaint
            | ControlStyles.ResizeRedraw
            | ControlStyles.Selectable, true);
        TabStop = false;
        Width = 10;
        BackColor = UiTheme.Bg;
        Cursor = Cursors.Default;
    }

    public event EventHandler ValueChanged;

    public int Minimum
    {
        get { return _minimum; }
        set
        {
            _minimum = value;
            if (_maximum < _minimum)
                _maximum = _minimum;
            Value = _value;
            Invalidate();
        }
    }

    public int Maximum
    {
        get { return _maximum; }
        set
        {
            _maximum = value < _minimum ? _minimum : value;
            Value = _value;
            Invalidate();
        }
    }

    /// <summary>Viewport size in the same units as Maximum (page size).</summary>
    public int LargeChange
    {
        get { return _largeChange; }
        set
        {
            _largeChange = value < 1 ? 1 : value;
            Invalidate();
        }
    }

    public int Value
    {
        get { return _value; }
        set
        {
            int v = value;
            int maxVal = Math.Max(_minimum, _maximum - Math.Max(0, _largeChange));
            if (v < _minimum) v = _minimum;
            if (v > maxVal) v = maxVal;
            if (v == _value)
                return;
            _value = v;
            Invalidate();
            EventHandler h = ValueChanged;
            if (h != null)
                h(this, EventArgs.Empty);
        }
    }

    public void SetMetrics(int minimum, int maximum, int largeChange, int value)
    {
        _minimum = minimum;
        _maximum = maximum < minimum ? minimum : maximum;
        _largeChange = largeChange < 1 ? 1 : largeChange;
        int maxVal = Math.Max(_minimum, _maximum - Math.Max(0, _largeChange));
        if (value < _minimum) value = _minimum;
        if (value > maxVal) value = maxVal;
        bool changed = value != _value;
        _value = value;
        Invalidate();
        if (changed)
        {
            EventHandler h = ValueChanged;
            if (h != null)
                h(this, EventArgs.Empty);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(UiTheme.Bg);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        int pad = Math.Max(1, Width / 5);
        _trackRect = new Rectangle(pad, pad, Math.Max(2, Width - pad * 2), Math.Max(2, Height - pad * 2));
        int radius = Math.Max(2, _trackRect.Width / 2);
        ChromeVisual.FillRound(g, _trackRect, radius, UiTheme.Raised);

        LayoutThumb();
        if (_thumbRect.Height >= 2 && _thumbRect.Width >= 2)
        {
            Color thumb = _dragging || _thumbHot
                ? UiTheme.TextSecondary
                : UiTheme.BorderSoft;
            ChromeVisual.FillRound(g, _thumbRect, radius, thumb);
        }
    }

    private void LayoutThumb()
    {
        int range = _maximum - _minimum;
        if (range <= 0 || _trackRect.Height < 4)
        {
            _thumbRect = Rectangle.Empty;
            return;
        }

        int view = _largeChange;
        if (view < 1)
            view = 1;
        float ratio = view / (float)Math.Max(view, range);
        int thumbH = (int)(_trackRect.Height * ratio);
        int minThumb = Math.Max(16, _trackRect.Width * 3);
        if (thumbH < minThumb)
            thumbH = minThumb;
        if (thumbH > _trackRect.Height)
            thumbH = _trackRect.Height;

        int span = _trackRect.Height - thumbH;
        int maxVal = Math.Max(0, range - view);
        int top = _trackRect.Y;
        if (maxVal > 0 && span > 0)
            top = _trackRect.Y + (int)(span * ((_value - _minimum) / (float)maxVal));
        _thumbRect = new Rectangle(_trackRect.X, top, _trackRect.Width, thumbH);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left)
            return;
        LayoutThumb();
        if (_thumbRect.Contains(e.Location))
        {
            _dragging = true;
            _dragThumbOffset = e.Y - _thumbRect.Y;
            Capture = true;
            Invalidate();
            return;
        }
        if (_trackRect.Contains(e.Location))
        {
            // Page jump toward click.
            if (e.Y < _thumbRect.Y)
                Value = _value - _largeChange;
            else
                Value = _value + _largeChange;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        LayoutThumb();
        bool hot = _thumbRect.Contains(e.Location);
        if (hot != _thumbHot)
        {
            _thumbHot = hot;
            Invalidate();
        }
        if (!_dragging)
            return;

        int span = _trackRect.Height - _thumbRect.Height;
        if (span < 1)
            return;
        int top = e.Y - _dragThumbOffset;
        if (top < _trackRect.Y)
            top = _trackRect.Y;
        if (top > _trackRect.Bottom - _thumbRect.Height)
            top = _trackRect.Bottom - _thumbRect.Height;
        int maxVal = Math.Max(0, _maximum - _minimum - _largeChange);
        int next = _minimum;
        if (span > 0 && maxVal > 0)
            next = _minimum + (int)Math.Round(maxVal * ((top - _trackRect.Y) / (float)span));
        Value = next;
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (_dragging)
        {
            _dragging = false;
            Capture = false;
            Invalidate();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (_thumbHot)
        {
            _thumbHot = false;
            Invalidate();
        }
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        Invalidate();
    }
}
