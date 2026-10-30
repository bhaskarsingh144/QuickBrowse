using System;
using System.Drawing;
using System.Windows.Forms;

/// <summary>Small dark rename/new-name prompt.</summary>
internal sealed class NamePromptForm : Form
{
    private readonly TextBox _box;
    private readonly Button _ok;
    private readonly Button _cancel;

    public string ResultName
    {
        get { return _box.Text != null ? _box.Text.Trim() : ""; }
    }

    public NamePromptForm(string title, string prompt, string initial)
    {
        Text = title ?? "Name";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = UiTheme.Surface;
        ForeColor = UiTheme.Text;
        ClientSize = new Size(360, 110);
        Font = ChromeFonts.CreateUi(ChromeLayout.UiPt, 1f);

        Label label = new Label();
        label.Text = string.IsNullOrEmpty(prompt) ? (title ?? "Name") : prompt;
        label.ForeColor = UiTheme.TextSecondary;
        label.AutoSize = false;
        label.SetBounds(16, 12, 328, 20);

        _box = new TextBox();
        _box.BorderStyle = BorderStyle.FixedSingle;
        _box.BackColor = UiTheme.Raised;
        _box.ForeColor = UiTheme.Text;
        _box.Text = initial ?? "";
        _box.SetBounds(16, 36, 328, 24);
        _box.KeyDown += OnBoxKeyDown;

        _ok = new Button();
        _ok.Text = "OK";
        _ok.FlatStyle = FlatStyle.Flat;
        _ok.BackColor = UiTheme.Chip;
        _ok.ForeColor = UiTheme.Text;
        _ok.FlatAppearance.BorderColor = UiTheme.BorderSoft;
        _ok.SetBounds(180, 70, 78, 28);
        _ok.Click += delegate { DialogResult = DialogResult.OK; Close(); };

        _cancel = new Button();
        _cancel.Text = "Cancel";
        _cancel.FlatStyle = FlatStyle.Flat;
        _cancel.BackColor = UiTheme.Chip;
        _cancel.ForeColor = UiTheme.Text;
        _cancel.FlatAppearance.BorderColor = UiTheme.BorderSoft;
        _cancel.SetBounds(266, 70, 78, 28);
        _cancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };

        AcceptButton = _ok;
        CancelButton = _cancel;
        Controls.Add(label);
        Controls.Add(_box);
        Controls.Add(_ok);
        Controls.Add(_cancel);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _box.Focus();
        _box.SelectAll();
    }

    private void OnBoxKeyDown(object sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    public static string Ask(IWin32Window owner, string title, string prompt, string initial)
    {
        using (NamePromptForm form = new NamePromptForm(title, prompt, initial))
        {
            if (form.ShowDialog(owner) != DialogResult.OK)
                return null;
            string name = form.ResultName;
            return string.IsNullOrEmpty(name) ? null : name;
        }
    }
}
