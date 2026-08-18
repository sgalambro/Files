using System.Drawing.Drawing2D;

namespace RelayBoxMatcher.App.Controls;

public enum CanvasMode
{
    None,
    DrawRect,
    PickPoints
}

public class RectOverlay
{
    public RectangleF Rect { get; set; }
    public Color Color { get; set; }
    public string Label { get; set; } = "";
}

public class PointOverlay
{
    public PointF Point { get; set; }
    public string Label { get; set; } = "";
}

/// <summary>
/// Controllo per mostrare un'immagine con zoom (rotella, centrato sul cursore) e pan (trascinamento
/// col tasto destro). In modalità DrawRect permette di disegnare un rettangolo con il tasto sinistro
/// (usato per definire gli slot dei relè sul campione); in modalità PickPoints permette di cliccare
/// punti di riferimento in ordine (usato sia per i punti di calibrazione sul campione sia per la
/// calibrazione di ogni foto di test). Tutte le coordinate esposte agli eventi sono nello spazio
/// dell'immagine originale (pixel), non del controllo.
/// </summary>
public class ImageCanvas : Control
{
    public Bitmap? Image { get; private set; }
    public CanvasMode Mode { get; set; } = CanvasMode.None;

    public List<RectOverlay> OverlayRects { get; } = new();
    public List<PointOverlay> OverlayPoints { get; } = new();

    public event EventHandler<RectangleF>? RectangleDrawn;
    public event EventHandler<PointF>? PointPicked;

    private float _zoom = 1f;
    private PointF _pan;

    private bool _isPanning;
    private Point _panStartMouse;
    private PointF _panStartPan;

    private PointF? _rectStartImage;
    private RectangleF? _rectPreviewImage;

    public ImageCanvas()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
        BackColor = Color.FromArgb(45, 45, 48);
    }

    public void LoadImage(Bitmap image)
    {
        Image = image;
        ZoomToFit();
    }

    public void ZoomToFit()
    {
        if (Image == null || Width <= 0 || Height <= 0) return;

        float zx = (float)Width / Image.Width;
        float zy = (float)Height / Image.Height;
        _zoom = Math.Min(zx, zy) * 0.96f;
        if (_zoom <= 0) _zoom = 1f;

        var imgW = Image.Width * _zoom;
        var imgH = Image.Height * _zoom;
        _pan = new PointF((Width - imgW) / 2f, (Height - imgH) / 2f);
        Invalidate();
    }

    public void ClearOverlays()
    {
        OverlayRects.Clear();
        OverlayPoints.Clear();
        Invalidate();
    }

    public void RemoveLastPoint()
    {
        if (OverlayPoints.Count > 0)
        {
            OverlayPoints.RemoveAt(OverlayPoints.Count - 1);
            Invalidate();
        }
    }

    private PointF ImageToControl(PointF p) => new(p.X * _zoom + _pan.X, p.Y * _zoom + _pan.Y);

    private PointF ControlToImage(PointF p) => new((p.X - _pan.X) / _zoom, (p.Y - _pan.Y) / _zoom);

    private RectangleF ImageToControlRect(RectangleF r)
    {
        var tl = ImageToControl(r.Location);
        return new RectangleF(tl.X, tl.Y, r.Width * _zoom, r.Height * _zoom);
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        if (Image == null) return;

        var imageBeforeZoom = ControlToImage(e.Location);
        float factor = e.Delta > 0 ? 1.15f : 1f / 1.15f;
        _zoom = Math.Clamp(_zoom * factor, 0.02f, 20f);

        // Ricalcola il pan per mantenere il punto sotto il cursore fermo durante lo zoom.
        var afterControl = ImageToControl(imageBeforeZoom);
        _pan.X += e.Location.X - afterControl.X;
        _pan.Y += e.Location.Y - afterControl.Y;

        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();

        if (e.Button == MouseButtons.Right)
        {
            _isPanning = true;
            _panStartMouse = e.Location;
            _panStartPan = _pan;
            return;
        }

        if (e.Button != MouseButtons.Left || Image == null) return;

        var imgPt = ControlToImage(e.Location);

        if (Mode == CanvasMode.DrawRect)
        {
            _rectStartImage = imgPt;
            _rectPreviewImage = new RectangleF(imgPt.X, imgPt.Y, 0, 0);
        }
        else if (Mode == CanvasMode.PickPoints)
        {
            PointPicked?.Invoke(this, imgPt);
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (_isPanning)
        {
            _pan = new PointF(
                _panStartPan.X + (e.X - _panStartMouse.X),
                _panStartPan.Y + (e.Y - _panStartMouse.Y));
            Invalidate();
            return;
        }

        if (_rectStartImage.HasValue && Image != null)
        {
            var cur = ControlToImage(e.Location);
            float x = Math.Min(_rectStartImage.Value.X, cur.X);
            float y = Math.Min(_rectStartImage.Value.Y, cur.Y);
            float w = Math.Abs(cur.X - _rectStartImage.Value.X);
            float h = Math.Abs(cur.Y - _rectStartImage.Value.Y);
            _rectPreviewImage = new RectangleF(x, y, w, h);
            Invalidate();
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);

        if (e.Button == MouseButtons.Right)
        {
            _isPanning = false;
            return;
        }

        if (e.Button == MouseButtons.Left && _rectStartImage.HasValue && _rectPreviewImage.HasValue)
        {
            var r = _rectPreviewImage.Value;
            _rectStartImage = null;
            _rectPreviewImage = null;
            Invalidate();

            if (r.Width >= 3 && r.Height >= 3)
                RectangleDrawn?.Invoke(this, r);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.Clear(BackColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;

        if (Image == null) return;

        var dest = ImageToControlRect(new RectangleF(0, 0, Image.Width, Image.Height));
        g.DrawImage(Image, dest);

        using var font = new Font(FontFamily.GenericSansSerif, 9f, FontStyle.Bold);

        foreach (var ov in OverlayRects)
        {
            var r = ImageToControlRect(ov.Rect);
            using var pen = new Pen(ov.Color, 2.5f);
            g.DrawRectangle(pen, r.X, r.Y, r.Width, r.Height);
            if (!string.IsNullOrEmpty(ov.Label))
            {
                using var brush = new SolidBrush(ov.Color);
                var size = g.MeasureString(ov.Label, font);
                g.FillRectangle(Brushes.Black, r.X, Math.Max(0, r.Y - size.Height), size.Width, size.Height);
                g.DrawString(ov.Label, font, brush, r.X, Math.Max(0, r.Y - size.Height));
            }
        }

        if (_rectPreviewImage.HasValue)
        {
            var r = ImageToControlRect(_rectPreviewImage.Value);
            using var pen = new Pen(Color.Cyan, 2f) { DashStyle = DashStyle.Dash };
            g.DrawRectangle(pen, r.X, r.Y, r.Width, r.Height);
        }

        int idx = 1;
        foreach (var pt in OverlayPoints)
        {
            var c = ImageToControl(pt.Point);
            using var pen = new Pen(Color.Yellow, 2f);
            g.DrawEllipse(pen, c.X - 6, c.Y - 6, 12, 12);
            g.DrawLine(pen, c.X - 10, c.Y, c.X + 10, c.Y);
            g.DrawLine(pen, c.X, c.Y - 10, c.X, c.Y + 10);
            var label = string.IsNullOrEmpty(pt.Label) ? idx.ToString() : pt.Label;
            g.FillRectangle(Brushes.Black, c.X + 8, c.Y - 8, g.MeasureString(label, font).Width, font.Height);
            g.DrawString(label, font, Brushes.Yellow, c.X + 8, c.Y - 8);
            idx++;
        }
    }
}
