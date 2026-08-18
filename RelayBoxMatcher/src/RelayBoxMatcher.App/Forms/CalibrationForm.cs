using RelayBoxMatcher.App.Controls;
using RelayBoxMatcher.Core.Imaging;

namespace RelayBoxMatcher.App.Forms;

/// <summary>
/// Dialogo modale per calibrare una foto di test: l'operatore clicca, nello stesso ordine definito sul
/// campione, gli stessi punti fisici di riferimento (es. viti/angoli della scatola). Da questi punti
/// si calcola l'omografia usata per proiettare le posizioni degli slot sulla foto di test, indipendentemente
/// da risoluzione, rotazione o leggera prospettiva diversa dello scatto.
/// </summary>
public class CalibrationForm : Form
{
    private readonly ImageCanvas _canvas;
    private readonly Label _lblStatus;
    private readonly Button _btnOk;
    private readonly IReadOnlyList<string> _pointNames;

    public List<PointD> Points { get; } = new();

    public CalibrationForm(Bitmap image, IReadOnlyList<string> pointNames)
    {
        _pointNames = pointNames;

        Text = "Calibrazione foto di test";
        StartPosition = FormStartPosition.CenterParent;
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(700, 500);

        var bottomPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 64,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(10),
            BackColor = Color.WhiteSmoke
        };

        _lblStatus = new Label
        {
            AutoSize = true,
            Font = new Font(FontFamily.GenericSansSerif, 11f),
            Margin = new Padding(0, 12, 30, 0)
        };

        var btnUndo = new Button { Text = "Annulla ultimo punto", Width = 170, Height = 36, Margin = new Padding(5) };
        var btnCancel = new Button { Text = "Annulla", Width = 100, Height = 36, Margin = new Padding(5) };
        _btnOk = new Button { Text = "Conferma", Width = 120, Height = 36, Margin = new Padding(5), Enabled = false };

        btnUndo.Click += (_, _) =>
        {
            _canvas.RemoveLastPoint();
            if (Points.Count > 0) Points.RemoveAt(Points.Count - 1);
            UpdateStatus();
        };
        btnCancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        _btnOk.Click += (_, _) => { DialogResult = DialogResult.OK; Close(); };

        bottomPanel.Controls.AddRange(new Control[] { _lblStatus, _btnOk, btnUndo, btnCancel });

        _canvas = new ImageCanvas { Dock = DockStyle.Fill, Mode = CanvasMode.PickPoints };
        _canvas.PointPicked += OnPointPicked;

        Controls.Add(bottomPanel);
        Controls.Add(_canvas);

        Load += (_, _) =>
        {
            _canvas.LoadImage(image);
            UpdateStatus();
        };
    }

    private void OnPointPicked(object? sender, PointF pt)
    {
        if (Points.Count >= _pointNames.Count) return;

        var name = _pointNames[Points.Count];
        Points.Add(new PointD(pt.X, pt.Y));
        _canvas.OverlayPoints.Add(new PointOverlay { Point = pt, Label = $"{Points.Count}:{name}" });
        _canvas.Invalidate();
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        if (Points.Count < _pointNames.Count)
        {
            _lblStatus.Text = $"Clicca il punto {Points.Count + 1} di {_pointNames.Count}: \"{_pointNames[Points.Count]}\" — lo stesso punto fisico cliccato sul campione. Tasto destro per spostare la vista, rotella per zoomare.";
            _btnOk.Enabled = false;
        }
        else
        {
            _lblStatus.Text = "Tutti i punti sono posizionati. Controlla che siano sugli stessi riferimenti fisici del campione, poi conferma.";
            _btnOk.Enabled = true;
        }
    }
}
