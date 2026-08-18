using RelayBoxMatcher.Core.Models;

namespace RelayBoxMatcher.App.Forms;

/// <summary>Piccolo dialogo modale per assegnare nome e classe colore a un rettangolo appena disegnato sul campione.</summary>
public class SlotNameColorDialog : Form
{
    private readonly TextBox _txtName;
    private readonly ComboBox _cmbColor;

    public string SlotName => _txtName.Text.Trim();
    public ColorClass SelectedColor => (ColorClass)_cmbColor.SelectedItem!;

    public SlotNameColorDialog(string suggestedName, ColorClass suggestedColor)
    {
        Text = "Nuovo slot relè";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ClientSize = new Size(320, 150);

        var lblName = new Label { Text = "Nome slot:", Location = new Point(12, 15), AutoSize = true };
        _txtName = new TextBox { Location = new Point(12, 35), Width = 296, Text = suggestedName };

        var lblColor = new Label { Text = "Colore etichetta:", Location = new Point(12, 65), AutoSize = true };
        _cmbColor = new ComboBox
        {
            Location = new Point(12, 85),
            Width = 296,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _cmbColor.Items.AddRange(new object[] { ColorClass.Blu, ColorClass.Rosa, ColorClass.Verde });
        _cmbColor.SelectedItem = suggestedColor is ColorClass.Blu or ColorClass.Rosa or ColorClass.Verde
            ? suggestedColor
            : ColorClass.Rosa;

        var btnOk = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(152, 118), Width = 75 };
        var btnCancel = new Button { Text = "Annulla", DialogResult = DialogResult.Cancel, Location = new Point(233, 118), Width = 75 };

        btnOk.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(_txtName.Text))
            {
                MessageBox.Show(this, "Il nome dello slot non può essere vuoto.", "Nome mancante",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;
            }
        };

        Controls.AddRange(new Control[] { lblName, _txtName, lblColor, _cmbColor, btnOk, btnCancel });
        AcceptButton = btnOk;
        CancelButton = btnCancel;
    }
}
