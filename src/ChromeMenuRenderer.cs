using System.Drawing;
using System.Windows.Forms;

/// <summary>Dark ToolStrip colors matching QuickBrowse chrome.</summary>
internal sealed class ChromeMenuColorTable : ProfessionalColorTable
{
    public override Color MenuBorder { get { return UiTheme.BorderSoft; } }
    public override Color MenuItemBorder { get { return UiTheme.Hover; } }
    public override Color MenuItemSelected { get { return UiTheme.Hover; } }
    public override Color MenuItemSelectedGradientBegin { get { return UiTheme.Hover; } }
    public override Color MenuItemSelectedGradientEnd { get { return UiTheme.Hover; } }
    public override Color MenuItemPressedGradientBegin { get { return UiTheme.Press; } }
    public override Color MenuItemPressedGradientEnd { get { return UiTheme.Press; } }
    public override Color MenuStripGradientBegin { get { return UiTheme.Surface; } }
    public override Color MenuStripGradientEnd { get { return UiTheme.Surface; } }
    public override Color ImageMarginGradientBegin { get { return UiTheme.Surface; } }
    public override Color ImageMarginGradientMiddle { get { return UiTheme.Surface; } }
    public override Color ImageMarginGradientEnd { get { return UiTheme.Surface; } }
    public override Color ToolStripDropDownBackground { get { return UiTheme.Surface; } }
    public override Color SeparatorDark { get { return UiTheme.Border; } }
    public override Color SeparatorLight { get { return UiTheme.Border; } }
}

internal sealed class ChromeMenuRenderer : ToolStripProfessionalRenderer
{
    public ChromeMenuRenderer()
        : base(new ChromeMenuColorTable())
    {
    }

    protected override void OnRenderItemImage(ToolStripItemImageRenderEventArgs e)
    {
        if (e.Image == null)
            return;

        // Vertically center glyph with the label (default ToolStrip layout drifts with padding).
        Rectangle content = e.Item.ContentRectangle;
        int x = e.ImageRectangle.X;
        int y = content.Y + (content.Height - e.Image.Height) / 2;
        Rectangle dest = new Rectangle(x, y, e.Image.Width, e.Image.Height);
        if (e.Item.Enabled)
            e.Graphics.DrawImage(e.Image, dest);
        else
            ControlPaint.DrawImageDisabled(e.Graphics, e.Image, dest.X, dest.Y, Color.Transparent);
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        if (!e.Item.Enabled)
            e.TextColor = UiTheme.Muted;
        else if (e.Item.Selected)
            e.TextColor = UiTheme.Accent;
        else
            e.TextColor = UiTheme.Text;

        Rectangle content = e.Item.ContentRectangle;
        Rectangle tr = e.TextRectangle;
        e.TextRectangle = new Rectangle(tr.X, content.Y, tr.Width, content.Height);
        e.TextFormat = TextFormatFlags.Left
            | TextFormatFlags.VerticalCenter
            | TextFormatFlags.NoPrefix
            | TextFormatFlags.EndEllipsis;
        base.OnRenderItemText(e);
    }
}
