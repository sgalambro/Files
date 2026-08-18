using System.Text.Json;
using RelayBoxMatcher.App.Controls;
using RelayBoxMatcher.Core.Models;
using RelayBoxMatcher.Core.Services;

namespace RelayBoxMatcher.App.Forms;

public class MainForm : Form
{
    // --- Stato: editor campione ---
    private Bitmap? _sampleImage;
    private string? _sampleImagePath;
    private readonly List<TemplateSlotInput> _slots = new();
    private readonly List<ReferencePoint> _refPoints = new();
    private ImageCanvas _canvasSample = null!;
    private ListBox _lstSlots = null!;
    private ListBox _lstRefPoints = null!;
    private RadioButton _rbDrawSlot = null!;
    private RadioButton _rbPickRefPoint = null!;
    private Label _lblTemplateFileStatus = null!;

    // --- Stato: modello caricato/usato per il match ---
    private RelayTemplate? _loadedTemplate;
    private string? _templateFolder;
    private ExpectedSet? _expectedSet;

    // --- Stato: test singolo ---
    private ImageCanvas _canvasTest = null!;
    private Bitmap? _testImage;
    private string? _testImagePath;
    private MatchingResult? _lastMatching;
    private TestReport? _lastReport;
    private DataGridView _dgvResults = null!;
    private Label _lblSummary = null!;
    private Label _lblTemplateStatusTest = null!;

    // --- Stato: batch ---
    private readonly List<string> _batchFiles = new();
    private readonly List<BatchSummaryRow> _batchRows = new();
    private ListBox _lstBatchFiles = null!;
    private DataGridView _dgvBatch = null!;
    private TextBox _txtBatchOutput = null!;
    private Label _lblTemplateStatusBatch = null!;

    // SplitContainer da posizionare solo a form visibile (vedi SafeSetSplitterDistance).
    private readonly List<(SplitContainer split, int distance)> _pendingSplitters = new();

    public MainForm()
    {
        Text = "Relay Box Matcher — controllo assemblaggio scatole relè";
        Width = 1300;
        Height = 850;
        StartPosition = FormStartPosition.CenterScreen;

        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(BuildTemplateTab());
        tabs.TabPages.Add(BuildTestTab());
        tabs.TabPages.Add(BuildBatchTab());
        Controls.Add(tabs);

        // Impostare SplitterDistance nell'inizializzatore, prima che il controllo abbia una
        // dimensione reale (non è ancora agganciato al form), lancia ArgumentOutOfRangeException:
        // i pannelli vengono quindi dimensionati qui, quando il form è già visibile e dimensionato.
        Shown += (_, _) =>
        {
            foreach (var (split, distance) in _pendingSplitters)
                SafeSetSplitterDistance(split, distance);
        };
    }

    private static void SafeSetSplitterDistance(SplitContainer split, int distance)
    {
        try
        {
            int min = split.Panel1MinSize;
            int max = Math.Max(min, split.Width - split.Panel2MinSize);
            split.SplitterDistance = Math.Clamp(distance, min, max);
        }
        catch
        {
            // Il layout non è ancora pronto: resta la posizione di default dello splitter, non blocchiamo l'avvio.
        }
    }

    // ========================= TAB 1: MODELLO (CAMPIONE) =========================

    private TabPage BuildTemplateTab()
    {
        var page = new TabPage("1. Modello (campione)");

        var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(6) };
        var btnLoadSample = new Button { Text = "Carica foto campione...", Width = 170, Height = 30 };
        var btnLoadExisting = new Button { Text = "Carica modello esistente...", Width = 190, Height = 30 };
        var btnSaveTemplate = new Button { Text = "Salva modello...", Width = 150, Height = 30 };
        _lblTemplateFileStatus = new Label { Text = "Nessun campione caricato.", AutoSize = true, Margin = new Padding(20, 8, 0, 0) };
        top.Controls.AddRange(new Control[] { btnLoadSample, btnLoadExisting, btnSaveTemplate, _lblTemplateFileStatus });

        var split = new SplitContainer { Dock = DockStyle.Fill };
        _pendingSplitters.Add((split, 900));

        _canvasSample = new ImageCanvas { Dock = DockStyle.Fill, Mode = CanvasMode.DrawRect };
        _canvasSample.RectangleDrawn += OnSampleRectangleDrawn;
        _canvasSample.PointPicked += OnSampleRefPointPicked;
        split.Panel1.Controls.Add(_canvasSample);

        var side = BuildTemplateSidePanel();
        split.Panel2.Controls.Add(side);

        page.Controls.Add(split);
        page.Controls.Add(top);

        btnLoadSample.Click += (_, _) =>
        {
            using var ofd = new OpenFileDialog { Filter = "Immagini|*.png;*.jpg;*.jpeg;*.bmp" };
            if (ofd.ShowDialog(this) != DialogResult.OK) return;

            _sampleImage?.Dispose();
            _sampleImage = new Bitmap(ofd.FileName);
            _sampleImagePath = ofd.FileName;
            _slots.Clear();
            _refPoints.Clear();
            _canvasSample.LoadImage(_sampleImage);
            RefreshSampleOverlays();
            RefreshSlotList();
            RefreshRefPointList();
            _lblTemplateFileStatus.Text = $"Campione: {Path.GetFileName(ofd.FileName)} ({_sampleImage.Width}x{_sampleImage.Height})";
        };

        btnLoadExisting.Click += (_, _) =>
        {
            using var ofd = new OpenFileDialog { Filter = "templates_meta.json|templates_meta.json|Tutti i file JSON|*.json" };
            if (ofd.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                _loadedTemplate = TemplateService.Load(ofd.FileName);
                _templateFolder = Path.GetDirectoryName(ofd.FileName)!;

                _slots.Clear();
                _refPoints.Clear();
                _refPoints.AddRange(_loadedTemplate.ReferencePoints);
                foreach (var s in _loadedTemplate.Templates)
                    _slots.Add(new TemplateSlotInput { Name = s.Name, ColorClass = s.ColorClass, Rect = s.Bbox.ToRectangle() });

                var samplePath = Path.Combine(_templateFolder, "sample.png");
                if (File.Exists(samplePath))
                {
                    _sampleImage?.Dispose();
                    _sampleImage = new Bitmap(samplePath);
                    _sampleImagePath = samplePath;
                    _canvasSample.LoadImage(_sampleImage);
                }

                RefreshSampleOverlays();
                RefreshSlotList();
                RefreshRefPointList();
                _lblTemplateFileStatus.Text = $"Modello: {ofd.FileName} — {_slots.Count} slot, {_refPoints.Count} punti di riferimento.";
                UpdateTemplateStatusLabels();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Errore caricamento modello", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };

        btnSaveTemplate.Click += (_, _) =>
        {
            if (_sampleImage == null) { Warn("Carica prima una foto campione."); return; }
            if (_slots.Count == 0) { Warn("Disegna almeno uno slot relè sul campione."); return; }
            if (_refPoints.Count < 4) { Warn("Definisci almeno 4 punti di riferimento (modalità \"Punti di riferimento\")."); return; }

            using var fbd = new FolderBrowserDialog { Description = "Cartella dove salvare il modello (templates_meta.json + ritagli)" };
            if (fbd.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                var metaPath = TemplateService.Save(fbd.SelectedPath, _sampleImage, _slots, _refPoints);
                if (_sampleImagePath != null)
                    File.Copy(_sampleImagePath, Path.Combine(fbd.SelectedPath, "sample.png"), overwrite: true);

                _loadedTemplate = TemplateService.Load(metaPath);
                _templateFolder = fbd.SelectedPath;
                UpdateTemplateStatusLabels();

                MessageBox.Show(this, $"Modello salvato in:\n{metaPath}", "Salvato", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Errore salvataggio modello", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };

        return page;
    }

    private Control BuildTemplateSidePanel()
    {
        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10) };

        var grpMode = new GroupBox { Text = "Modalità disegno", Location = new Point(10, 10), Size = new Size(260, 90) };
        _rbDrawSlot = new RadioButton { Text = "Disegna slot relè (trascina un rettangolo)", Location = new Point(10, 24), AutoSize = true, Checked = true };
        _rbPickRefPoint = new RadioButton { Text = "Punti di riferimento (clic singolo)", Location = new Point(10, 52), AutoSize = true };
        _rbDrawSlot.CheckedChanged += (_, _) => { if (_rbDrawSlot.Checked) _canvasSample.Mode = CanvasMode.DrawRect; };
        _rbPickRefPoint.CheckedChanged += (_, _) => { if (_rbPickRefPoint.Checked) _canvasSample.Mode = CanvasMode.PickPoints; };
        grpMode.Controls.AddRange(new Control[] { _rbDrawSlot, _rbPickRefPoint });

        var lblSlots = new Label { Text = "Slot relè definiti (2 blu, 4 rosa, 1 verde nel caso tipico):", Location = new Point(10, 108), AutoSize = true, MaximumSize = new Size(260, 0) };
        _lstSlots = new ListBox { Location = new Point(10, 132), Size = new Size(260, 160) };
        var btnRemoveSlot = new Button { Text = "Rimuovi slot selezionato", Location = new Point(10, 298), Size = new Size(260, 28) };
        btnRemoveSlot.Click += (_, _) =>
        {
            if (_lstSlots.SelectedIndex < 0) return;
            _slots.RemoveAt(_lstSlots.SelectedIndex);
            RefreshSampleOverlays();
            RefreshSlotList();
        };

        var lblPoints = new Label { Text = "Punti di riferimento (min. 4, non allineati):", Location = new Point(10, 334), AutoSize = true };
        _lstRefPoints = new ListBox { Location = new Point(10, 356), Size = new Size(260, 110) };
        var btnRemovePoint = new Button { Text = "Rimuovi ultimo punto", Location = new Point(10, 470), Size = new Size(260, 28) };
        btnRemovePoint.Click += (_, _) =>
        {
            if (_refPoints.Count == 0) return;
            _refPoints.RemoveAt(_refPoints.Count - 1);
            RefreshSampleOverlays();
            RefreshRefPointList();
        };

        var lblHint = new Label
        {
            Text = "Suggerimento: come punti di riferimento usa 4 elementi fisici facili da ritrovare identici in ogni foto (es. le 4 viti di fissaggio o gli angoli della scatola), non i relè stessi.",
            Location = new Point(10, 506),
            Size = new Size(260, 90),
            ForeColor = Color.DimGray
        };

        panel.Controls.AddRange(new Control[] { grpMode, lblSlots, _lstSlots, btnRemoveSlot, lblPoints, _lstRefPoints, btnRemovePoint, lblHint });
        return panel;
    }

    private void OnSampleRectangleDrawn(object? sender, RectangleF rectF)
    {
        if (_sampleImage == null) return;
        var rect = Rectangle.Round(rectF);

        using var dlg = new SlotNameColorDialog($"rosa_{_slots.Count + 1}", ColorClass.Rosa);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        _slots.Add(new TemplateSlotInput { Name = dlg.SlotName, ColorClass = dlg.SelectedColor, Rect = rect });
        RefreshSampleOverlays();
        RefreshSlotList();
    }

    private void OnSampleRefPointPicked(object? sender, PointF ptF)
    {
        if (_sampleImage == null) return;

        using var dlg = new TextInputDialog("Nuovo punto di riferimento", "Nome punto (es. vite alto-sinistra):", $"P{_refPoints.Count + 1}");
        if (dlg.ShowDialog(this) != DialogResult.OK || string.IsNullOrWhiteSpace(dlg.Value)) return;

        _refPoints.Add(new ReferencePoint { Name = dlg.Value, X = ptF.X, Y = ptF.Y });
        RefreshSampleOverlays();
        RefreshRefPointList();
    }

    private void RefreshSampleOverlays()
    {
        _canvasSample.OverlayRects.Clear();
        foreach (var s in _slots)
            _canvasSample.OverlayRects.Add(new RectOverlay { Rect = s.Rect, Color = ColorForClass(s.ColorClass), Label = s.Name });

        _canvasSample.OverlayPoints.Clear();
        for (int i = 0; i < _refPoints.Count; i++)
            _canvasSample.OverlayPoints.Add(new PointOverlay { Point = new PointF((float)_refPoints[i].X, (float)_refPoints[i].Y), Label = $"{i + 1}:{_refPoints[i].Name}" });

        _canvasSample.Invalidate();
    }

    private void RefreshSlotList()
    {
        _lstSlots.Items.Clear();
        foreach (var s in _slots)
            _lstSlots.Items.Add($"{s.Name} [{s.ColorClass}] {s.Rect.Width}x{s.Rect.Height} @({s.Rect.X},{s.Rect.Y})");
    }

    private void RefreshRefPointList()
    {
        _lstRefPoints.Items.Clear();
        for (int i = 0; i < _refPoints.Count; i++)
            _lstRefPoints.Items.Add($"{i + 1}: {_refPoints[i].Name} ({_refPoints[i].X:F0},{_refPoints[i].Y:F0})");
    }

    // ========================= TAB 2: TEST SINGOLO =========================

    private TabPage BuildTestTab()
    {
        var page = new TabPage("2. Test singolo");

        var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(6) };
        var btnLoadTemplate = new Button { Text = "Carica modello...", Width = 150, Height = 30 };
        var btnLoadTestImage = new Button { Text = "Carica foto di test...", Width = 160, Height = 30 };
        var btnCalibrateMatch = new Button { Text = "Calibra ed esegui match", Width = 180, Height = 30 };
        var btnLoadExpected = new Button { Text = "Carica expected.json...", Width = 170, Height = 30 };
        var btnExport = new Button { Text = "Esporta report...", Width = 140, Height = 30 };
        _lblTemplateStatusTest = new Label { Text = "Nessun modello caricato.", AutoSize = true, Margin = new Padding(20, 8, 0, 0) };
        top.Controls.AddRange(new Control[] { btnLoadTemplate, btnLoadTestImage, btnCalibrateMatch, btnLoadExpected, btnExport, _lblTemplateStatusTest });

        var split = new SplitContainer { Dock = DockStyle.Fill };
        _pendingSplitters.Add((split, 800));
        _canvasTest = new ImageCanvas { Dock = DockStyle.Fill, Mode = CanvasMode.None };
        split.Panel1.Controls.Add(_canvasTest);

        var rightPanel = new Panel { Dock = DockStyle.Fill };
        _lblSummary = new Label { Dock = DockStyle.Top, Height = 70, Padding = new Padding(8), Font = new Font(FontFamily.GenericSansSerif, 10f, FontStyle.Bold), Text = "Carica un modello e una foto di test, poi calibra ed esegui il match." };
        _dgvResults = BuildResultsGrid();
        _dgvResults.Dock = DockStyle.Fill;
        rightPanel.Controls.Add(_dgvResults);
        rightPanel.Controls.Add(_lblSummary);
        split.Panel2.Controls.Add(rightPanel);

        page.Controls.Add(split);
        page.Controls.Add(top);

        btnLoadTemplate.Click += (_, _) =>
        {
            using var ofd = new OpenFileDialog { Filter = "templates_meta.json|templates_meta.json|Tutti i file JSON|*.json" };
            if (ofd.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                _loadedTemplate = TemplateService.Load(ofd.FileName);
                _templateFolder = Path.GetDirectoryName(ofd.FileName)!;
                UpdateTemplateStatusLabels();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Errore caricamento modello", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };

        btnLoadTestImage.Click += (_, _) =>
        {
            using var ofd = new OpenFileDialog { Filter = "Immagini|*.png;*.jpg;*.jpeg;*.bmp" };
            if (ofd.ShowDialog(this) != DialogResult.OK) return;

            _testImage?.Dispose();
            _testImage = new Bitmap(ofd.FileName);
            _testImagePath = ofd.FileName;
            _canvasTest.LoadImage(_testImage);
            _canvasTest.ClearOverlays();
            _lastMatching = null;
            _lastReport = null;
            _dgvResults.DataSource = null;
            _lblSummary.Text = $"Foto di test caricata: {Path.GetFileName(ofd.FileName)}. Premi \"Calibra ed esegui match\".";
        };

        btnCalibrateMatch.Click += (_, _) => RunCalibrateAndMatch();

        btnLoadExpected.Click += (_, _) =>
        {
            using var ofd = new OpenFileDialog { Filter = "expected.json|expected.json|Tutti i file JSON|*.json" };
            if (ofd.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                _expectedSet = JsonSerializer.Deserialize<ExpectedSet>(File.ReadAllText(ofd.FileName));
                MessageBox.Show(this, $"Ground truth caricata: {_expectedSet?.Images.Count ?? 0} immagini.", "expected.json", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Errore caricamento expected.json", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };

        btnExport.Click += (_, _) =>
        {
            if (_lastReport == null || _lastMatching == null || _testImage == null) { Warn("Esegui prima un match."); return; }

            using var fbd = new FolderBrowserDialog { Description = "Cartella dove salvare test_report.json, summary.csv e l'immagine annotata" };
            if (fbd.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                ReportService.WriteTestReport(fbd.SelectedPath, _lastReport);
                using var annotated = ReportService.DrawAnnotated(_testImage, _lastMatching.Detections);
                ReportService.SaveAnnotated(fbd.SelectedPath, annotated);
                var row = ScoringService.ToSummaryRow(_lastReport, _loadedTemplate?.Templates.Count ?? _lastReport.Results.Count);
                ReportService.AppendSummaryCsv(fbd.SelectedPath, row);

                MessageBox.Show(this, $"Report esportato in:\n{fbd.SelectedPath}", "Esportato", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Errore esportazione", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };

        return page;
    }

    private void RunCalibrateAndMatch()
    {
        if (_loadedTemplate == null || _templateFolder == null) { Warn("Carica prima un modello (tab 1 o pulsante \"Carica modello...\")."); return; }
        if (_testImage == null) { Warn("Carica prima una foto di test."); return; }

        var pointNames = _loadedTemplate.ReferencePoints.Select(p => p.Name).ToList();
        using var calForm = new CalibrationForm(_testImage, pointNames);
        if (calForm.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            var matching = MatchingService.Match(_templateFolder, _loadedTemplate, _testImage, calForm.Points, Path.GetFileName(_testImagePath!));
            _lastMatching = matching;

            var expectedImage = _expectedSet?.FindByFilename(Path.GetFileName(_testImagePath!));
            var report = ScoringService.Build(matching, expectedImage);
            _lastReport = report;

            var annotated = ReportService.DrawAnnotated(_testImage, matching.Detections);
            _canvasTest.LoadImage(annotated);
            _canvasTest.ClearOverlays();

            _dgvResults.DataSource = null;
            _dgvResults.DataSource = report.Results;

            UpdateSummaryLabel(report);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Errore durante il match", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void UpdateSummaryLabel(TestReport report)
    {
        int presenti = report.Results.Count(r => r.PresenceStatus == nameof(PresenceStatus.Presente));
        int assenti = report.Results.Count(r => r.PresenceStatus == nameof(PresenceStatus.Assente));
        int coloreErrato = report.Results.Count(r => r.PresenceStatus == nameof(PresenceStatus.ColoreErrato));
        int incerti = report.Results.Count(r => r.PresenceStatus == nameof(PresenceStatus.Incerto));

        string baseText = $"Presenti OK: {presenti}   Assenti: {assenti}   Colore errato: {coloreErrato}   Incerti: {incerti}";

        if (report.Precision.HasValue)
        {
            _lblSummary.Text = $"{baseText}\n{(report.Pass ? "PASS" : "FAIL")} — P={report.Precision:P0} R={report.Recall:P0} F1={report.F1:P0}";
            _lblSummary.ForeColor = report.Pass ? Color.DarkGreen : Color.DarkRed;
        }
        else
        {
            _lblSummary.Text = $"{baseText}\n(nessuna ground truth caricata: solo rilevazione)";
            _lblSummary.ForeColor = Color.Black;
        }
    }

    private DataGridView BuildResultsGrid()
    {
        var grid = new DataGridView
        {
            AutoGenerateColumns = false,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            ReadOnly = true,
            RowHeadersVisible = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        };

        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Slot", DataPropertyName = nameof(MatchResult.Template) });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Colore atteso", DataPropertyName = nameof(MatchResult.ExpectedColor) });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Colore rilevato", DataPropertyName = nameof(MatchResult.DetectedColor) });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Stato", DataPropertyName = nameof(MatchResult.PresenceStatus) });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Colore errato", DataPropertyName = nameof(MatchResult.WrongColorMismatch) });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Esito QA", DataPropertyName = nameof(MatchResult.Status) });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Confidenza", DataPropertyName = nameof(MatchResult.Score), DefaultCellStyle = new DataGridViewCellStyle { Format = "P0" } });

        grid.CellFormatting += (_, e) =>
        {
            if (grid.Columns[e.ColumnIndex].DataPropertyName != nameof(MatchResult.PresenceStatus)) return;
            if (e.Value == null) return;

            var text = e.Value.ToString();
            e.CellStyle.BackColor = text switch
            {
                nameof(PresenceStatus.Presente) => Color.PaleGreen,
                nameof(PresenceStatus.Assente) => Color.LightCoral,
                nameof(PresenceStatus.ColoreErrato) => Color.Orange,
                nameof(PresenceStatus.Incerto) => Color.LightGoldenrodYellow,
                _ => e.CellStyle.BackColor
            };
        };

        return grid;
    }

    // ========================= TAB 3: BATCH =========================

    private TabPage BuildBatchTab()
    {
        var page = new TabPage("3. Batch (più foto)");

        var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(6) };
        var btnAdd = new Button { Text = "Aggiungi immagini...", Width = 150, Height = 30 };
        var btnRemove = new Button { Text = "Rimuovi selezionata", Width = 150, Height = 30 };
        var btnBrowseOut = new Button { Text = "Cartella output...", Width = 140, Height = 30 };
        var btnStart = new Button { Text = "Avvia batch", Width = 130, Height = 30 };
        _lblTemplateStatusBatch = new Label { Text = "Nessun modello caricato.", AutoSize = true, Margin = new Padding(20, 8, 0, 0) };
        top.Controls.AddRange(new Control[] { btnAdd, btnRemove, btnBrowseOut, btnStart, _lblTemplateStatusBatch });

        var outPanel = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 34, Padding = new Padding(6, 0, 6, 0) };
        var lblOut = new Label { Text = "Cartella output:", AutoSize = true, Margin = new Padding(0, 8, 6, 0) };
        _txtBatchOutput = new TextBox { Width = 500, Margin = new Padding(0, 4, 0, 0) };
        outPanel.Controls.AddRange(new Control[] { lblOut, _txtBatchOutput });

        var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical };
        _pendingSplitters.Add((split, 320));
        _lstBatchFiles = new ListBox { Dock = DockStyle.Fill };
        split.Panel1.Controls.Add(_lstBatchFiles);

        _dgvBatch = new DataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            ReadOnly = true,
            RowHeadersVisible = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        };
        split.Panel2.Controls.Add(_dgvBatch);

        page.Controls.Add(split);
        page.Controls.Add(outPanel);
        page.Controls.Add(top);

        btnAdd.Click += (_, _) =>
        {
            using var ofd = new OpenFileDialog { Filter = "Immagini|*.png;*.jpg;*.jpeg;*.bmp", Multiselect = true };
            if (ofd.ShowDialog(this) != DialogResult.OK) return;
            foreach (var f in ofd.FileNames)
            {
                if (!_batchFiles.Contains(f))
                {
                    _batchFiles.Add(f);
                    _lstBatchFiles.Items.Add(Path.GetFileName(f));
                }
            }
        };

        btnRemove.Click += (_, _) =>
        {
            if (_lstBatchFiles.SelectedIndex < 0) return;
            _batchFiles.RemoveAt(_lstBatchFiles.SelectedIndex);
            _lstBatchFiles.Items.RemoveAt(_lstBatchFiles.SelectedIndex);
        };

        btnBrowseOut.Click += (_, _) =>
        {
            using var fbd = new FolderBrowserDialog { Description = "Cartella dove salvare i risultati del batch" };
            if (fbd.ShowDialog(this) == DialogResult.OK) _txtBatchOutput.Text = fbd.SelectedPath;
        };

        btnStart.Click += (_, _) => RunBatch();

        return page;
    }

    private void RunBatch()
    {
        if (_loadedTemplate == null || _templateFolder == null) { Warn("Carica prima un modello."); return; }
        if (_batchFiles.Count == 0) { Warn("Aggiungi almeno un'immagine di test."); return; }
        if (string.IsNullOrWhiteSpace(_txtBatchOutput.Text)) { Warn("Scegli una cartella di output."); return; }

        var pointNames = _loadedTemplate.ReferencePoints.Select(p => p.Name).ToList();
        _batchRows.Clear();

        foreach (var path in _batchFiles.ToList())
        {
            using var img = new Bitmap(path);
            using var calForm = new CalibrationForm(img, pointNames) { Text = $"Calibrazione — {Path.GetFileName(path)}" };

            if (calForm.ShowDialog(this) != DialogResult.OK)
            {
                var choice = MessageBox.Show(this,
                    $"Calibrazione annullata per {Path.GetFileName(path)}. Continuare con le immagini successive?",
                    "Batch", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (choice == DialogResult.No) break;
                continue;
            }

            try
            {
                var matching = MatchingService.Match(_templateFolder, _loadedTemplate, img, calForm.Points, Path.GetFileName(path));
                var expectedImage = _expectedSet?.FindByFilename(Path.GetFileName(path));
                var report = ScoringService.Build(matching, expectedImage);

                var imgOutFolder = Path.Combine(_txtBatchOutput.Text, Path.GetFileNameWithoutExtension(path));
                ReportService.WriteTestReport(imgOutFolder, report);
                using (var annotated = ReportService.DrawAnnotated(img, matching.Detections))
                    ReportService.SaveAnnotated(imgOutFolder, annotated);

                var row = ScoringService.ToSummaryRow(report, _loadedTemplate.Templates.Count);
                ReportService.AppendSummaryCsv(_txtBatchOutput.Text, row);
                _batchRows.Add(row);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Errore su {Path.GetFileName(path)}: {ex.Message}", "Errore batch", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        _dgvBatch.DataSource = null;
        _dgvBatch.DataSource = _batchRows;
        MessageBox.Show(this, $"Batch completato: {_batchRows.Count}/{_batchFiles.Count} immagini elaborate.\nOutput in:\n{_txtBatchOutput.Text}",
            "Batch completato", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    // ========================= UTILITÀ COMUNI =========================

    private void UpdateTemplateStatusLabels()
    {
        var text = _loadedTemplate == null
            ? "Nessun modello caricato."
            : $"Modello: {_loadedTemplate.Templates.Count} slot, {_loadedTemplate.ReferencePoints.Count} punti di riferimento ({_templateFolder}).";

        _lblTemplateStatusTest.Text = text;
        _lblTemplateStatusBatch.Text = text;
    }

    private static Color ColorForClass(ColorClass c) => c switch
    {
        ColorClass.Blu => Color.DeepSkyBlue,
        ColorClass.Rosa => Color.HotPink,
        ColorClass.Verde => Color.LimeGreen,
        _ => Color.Gray
    };

    private void Warn(string message) =>
        MessageBox.Show(this, message, "Attenzione", MessageBoxButtons.OK, MessageBoxIcon.Warning);
}
