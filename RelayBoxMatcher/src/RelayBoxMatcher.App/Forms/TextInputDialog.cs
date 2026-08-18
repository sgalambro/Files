namespace RelayBoxMatcher.App.Forms;

/// <summary>Dialogo generico a una riga di testo (usato per nominare i punti di riferimento di calibrazione).</summary>
public class TextInputDialog : Form
{
    private readonly TextBox _txt;

    public string Value => _txt.Text.Trim();

    public TextInputDialog(string title, string prompt, string defaultValue)
    {
        Text = title;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ClientSize = new Size(320, 115);

        var lbl = new Label { Text = prompt, Location = new Point(12, 12), AutoSize = true, MaximumSize = new Size(296, 0) };
        _txt = new TextBox { Location = new Point(12, 45), Width = 296, Text = defaultValue };

        var btnOk = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(152, 78), Width = 75 };
        var btnCancel = new Button { Text = "Annulla", DialogResult = DialogResult.Cancel, Location = new Point(233, 78), Width = 75 };

        Controls.AddRange(new Control[] { lbl, _txt, btnOk, btnCancel });
        AcceptButton = btnOk;
        CancelButton = btnCancel;
        _txt.SelectAll();
    }
}
