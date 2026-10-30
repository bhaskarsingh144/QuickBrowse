using System;
using System.Drawing;
using System.Windows.Forms;

/// <summary>
/// Hover info card that can sit above ShellChrome topmost pane/rail.
/// Native ListView / WinForms tooltips render behind the explorer chrome.
/// </summary>
internal sealed class ChromeHoverTip : Form
{
    private readonly Label _label;
    private string _text = "";

    public ChromeHoverTip()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = false;
        BackColor = UiTheme.Raised;
        ForeColor = UiTheme.Text;
        Opacity = 1.0;
        AutoScaleMode = AutoScaleMode.None;
        DoubleBuffered = true;
        Text = "QuickBrowse Tip";
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);

        _label = new Label();
        _label.AutoSize = false;
        _label.Dock = DockStyle.Fill;
        _label.Padding = new Padding(10, 8, 10, 8);
        _label.ForeColor = UiTheme.Text;
        _label.BackColor = UiTheme.Raised;
        _label.TextAlign = ContentAlignment.TopLeft;
        Controls.Add(_label);
    }

    protected override bool ShowWithoutActivation
    {
        get { return true; }
    }

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            ShellChrome.Prepare(cp);
            // Tool window + no activate.
            cp.ExStyle |= 0x00000008; // WS_EX_TOPMOST — keep tip above pinned chrome
            return cp;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ChromeVisual.PreferDarkMode(Handle);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        ChromeVisual.DrawRoundBorder(e.Graphics, new Rectangle(0, 0, Width, Height), 8, UiTheme.BorderSoft);
    }

    public void ShowTip(IWin32Window owner, string text, Point screenAnchor, Font font)
    {
        text = text ?? "";
        if (text.Length == 0)
        {
            HideTip();
            return;
        }

        bool same = Visible && string.Equals(_text, text, StringComparison.Ordinal);
        _text = text;
        if (font != null)
            _label.Font = font;
        _label.Text = text;

        Size measured = TextRenderer.MeasureText(
            text,
            _label.Font ?? Font,
            new Size(360, 0),
            TextFormatFlags.Left | TextFormatFlags.TextBoxControl | TextFormatFlags.WordBreak);
        int w = Math.Min(380, Math.Max(160, measured.Width + 24));
        int h = Math.Min(220, Math.Max(40, measured.Height + 20));
        Size = new Size(w, h);

        Point loc = new Point(screenAnchor.X + 16, screenAnchor.Y + 18);
        Rectangle wa = Screen.FromPoint(screenAnchor).WorkingArea;
        if (loc.X + Width > wa.Right)
            loc.X = Math.Max(wa.Left, screenAnchor.X - Width - 8);
        if (loc.Y + Height > wa.Bottom)
            loc.Y = Math.Max(wa.Top, screenAnchor.Y - Height - 8);
        Location = loc;

        if (!Visible)
            Show(owner);
        else if (!same)
            Invalidate();

        if (IsHandleCreated)
        {
            ShellChrome.ZOrder(Handle, true);
            ChromeVisual.ApplyRoundedRegion(this, 8);
        }
    }

    public void HideTip()
    {
        _text = "";
        if (Visible)
            Hide();
    }
}
