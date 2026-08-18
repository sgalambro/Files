namespace RelayBoxMatcher.Core.Imaging;

public readonly record struct PointD(double X, double Y);

public readonly record struct RectangleD(double X, double Y, double Width, double Height)
{
    public System.Drawing.Rectangle ToRectangle(int clampWidth, int clampHeight)
    {
        int x = Math.Max(0, (int)Math.Floor(X));
        int y = Math.Max(0, (int)Math.Floor(Y));
        int right = Math.Min(clampWidth, (int)Math.Ceiling(X + Width));
        int bottom = Math.Min(clampHeight, (int)Math.Ceiling(Y + Height));
        int w = Math.Max(0, right - x);
        int h = Math.Max(0, bottom - y);
        return new System.Drawing.Rectangle(x, y, w, h);
    }
}

/// <summary>
/// Omografia planare (trasformazione prospettica) stimata da almeno 4 coppie di punti corrispondenti.
/// Usata per proiettare le posizioni dei relè note sulla foto campione sulla foto di test, che può avere
/// risoluzione, rotazione e leggera prospettiva diverse: è il motivo per cui non si possono riusare
/// direttamente le stesse coordinate in pixel tra campione e test.
/// </summary>
public class Homography
{
    private readonly double[,] _h; // 3x3, h[2,2] = 1

    private Homography(double[,] h) => _h = h;

    public static Homography Fit(IReadOnlyList<PointD> src, IReadOnlyList<PointD> dst)
    {
        if (src.Count != dst.Count)
            throw new ArgumentException("Il numero di punti sorgente e destinazione deve coincidere.");
        if (src.Count < 4)
            throw new ArgumentException("Servono almeno 4 punti di riferimento per calcolare la calibrazione.");

        var ata = new double[8, 8];
        var atb = new double[8];

        for (int i = 0; i < src.Count; i++)
        {
            double x = src[i].X, y = src[i].Y, u = dst[i].X, v = dst[i].Y;

            var rowU = new[] { x, y, 1.0, 0.0, 0.0, 0.0, -x * u, -y * u };
            Accumulate(ata, atb, rowU, u);

            var rowV = new[] { 0.0, 0.0, 0.0, x, y, 1.0, -x * v, -y * v };
            Accumulate(ata, atb, rowV, v);
        }

        var hVec = SolveLinearSystem(ata, atb);

        var h = new double[3, 3]
        {
            { hVec[0], hVec[1], hVec[2] },
            { hVec[3], hVec[4], hVec[5] },
            { hVec[6], hVec[7], 1.0 }
        };

        return new Homography(h);
    }

    private static void Accumulate(double[,] ata, double[] atb, double[] row, double target)
    {
        for (int i = 0; i < 8; i++)
        {
            atb[i] += row[i] * target;
            for (int j = 0; j < 8; j++)
                ata[i, j] += row[i] * row[j];
        }
    }

    /// <summary>Risolve A*x = b con eliminazione di Gauss-Jordan e pivot parziale (A è quadrata NxN).</summary>
    private static double[] SolveLinearSystem(double[,] a, double[] b)
    {
        int n = b.Length;
        var m = (double[,])a.Clone();
        var rhs = (double[])b.Clone();

        for (int col = 0; col < n; col++)
        {
            int pivot = col;
            double best = Math.Abs(m[col, col]);
            for (int r = col + 1; r < n; r++)
            {
                double v = Math.Abs(m[r, col]);
                if (v > best) { best = v; pivot = r; }
            }

            if (pivot != col)
            {
                for (int c = 0; c < n; c++) (m[col, c], m[pivot, c]) = (m[pivot, c], m[col, c]);
                (rhs[col], rhs[pivot]) = (rhs[pivot], rhs[col]);
            }

            double diag = m[col, col];
            if (Math.Abs(diag) < 1e-9)
                throw new InvalidOperationException(
                    "Calibrazione degenere: i punti di riferimento scelti sono troppo allineati o coincidenti. Scegli 4 punti ben distribuiti e non collineari.");

            for (int c = col; c < n; c++) m[col, c] /= diag;
            rhs[col] /= diag;

            for (int r = 0; r < n; r++)
            {
                if (r == col) continue;
                double factor = m[r, col];
                if (factor == 0) continue;
                for (int c = col; c < n; c++) m[r, c] -= factor * m[col, c];
                rhs[r] -= factor * rhs[col];
            }
        }

        return rhs;
    }

    public PointD Transform(PointD p)
    {
        double x = p.X, y = p.Y;
        double u = _h[0, 0] * x + _h[0, 1] * y + _h[0, 2];
        double v = _h[1, 0] * x + _h[1, 1] * y + _h[1, 2];
        double w = _h[2, 0] * x + _h[2, 1] * y + _h[2, 2];
        if (Math.Abs(w) < 1e-12) w = 1e-12;
        return new PointD(u / w, v / w);
    }

    /// <summary>Proietta i 4 angoli del rettangolo e restituisce il bounding box del risultato
    /// (l'omografia può introdurre una lieve rotazione/prospettiva, quindi il proiettato non è
    /// in generale un rettangolo assiale: usiamo il suo bounding box come ROI di campionamento).</summary>
    public RectangleD TransformRect(System.Drawing.Rectangle r)
    {
        var pts = new[]
        {
            Transform(new PointD(r.Left, r.Top)),
            Transform(new PointD(r.Right, r.Top)),
            Transform(new PointD(r.Left, r.Bottom)),
            Transform(new PointD(r.Right, r.Bottom)),
        };
        double minX = pts.Min(p => p.X), maxX = pts.Max(p => p.X);
        double minY = pts.Min(p => p.Y), maxY = pts.Max(p => p.Y);
        return new RectangleD(minX, minY, maxX - minX, maxY - minY);
    }
}
