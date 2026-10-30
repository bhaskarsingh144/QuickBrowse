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

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        if (!e.Item.Enabled)
            e.TextColor = UiTheme.Muted;
        else if (e.Item.Selected)
            e.TextColor = UiTheme.Accent;
        else
            e.TextColor = UiTheme.Text;
        base.OnRenderItemText(e);
    }
}
