using D3B.Engine.Maps;
using D3B.Gui.Windowing;
using OpenCvSharp;

namespace D3B.Gui.Calibration;

/// <summary>Maps logical map points to pixels using a six-parameter affine transform.</summary>
public sealed class AffineScreenMapper
{
    /// <summary>The relative determinant threshold below which the linear transform is considered singular.</summary>
    public const double SingularityEpsilon = 1e-12;

    /// <summary>Gets the horizontal map-X coefficient.</summary>
    public double A { get; }

    /// <summary>Gets the horizontal map-Y coefficient.</summary>
    public double B { get; }

    /// <summary>Gets the horizontal translation in pixels.</summary>
    public double Tx { get; }

    /// <summary>Gets the vertical map-X coefficient.</summary>
    public double C { get; }

    /// <summary>Gets the vertical map-Y coefficient.</summary>
    public double D { get; }

    /// <summary>Gets the vertical translation in pixels.</summary>
    public double Ty { get; }

    /// <summary>Gets the determinant of the linear 2×2 component.</summary>
    public double Determinant { get; }

    private AffineScreenMapper(double a, double b, double tx, double c, double d, double ty, double determinant)
    {
        A = a;
        B = b;
        Tx = tx;
        C = c;
        D = d;
        Ty = ty;
        Determinant = determinant;
    }

    /// <summary>Creates a mapper from six finite, non-singular coefficients.</summary>
    /// <param name="coefficients">Coefficients ordered as a, b, tx, c, d, ty.</param>
    /// <param name="mapper">The mapper when the coefficients are valid.</param>
    /// <returns>Whether a mapper was created.</returns>
    public static bool TryCreate(double[]? coefficients, out AffineScreenMapper? mapper)
    {
        mapper = null;

        if (coefficients is null || coefficients.Length != 6)
            return false;

        for (var index = 0; index < coefficients.Length; index++)
        {
            if (!double.IsFinite(coefficients[index]))
                return false;
        }

        var a = coefficients[0];
        var b = coefficients[1];
        var tx = coefficients[2];
        var c = coefficients[3];
        var d = coefficients[4];
        var ty = coefficients[5];
        var scale = Math.Max(Math.Max(Math.Abs(a), Math.Abs(b)), Math.Max(Math.Abs(c), Math.Abs(d)));

        if (scale == 0)
            return false;

        var normalizedDeterminant = (a / scale * (d / scale)) - (b / scale * (c / scale));
        var determinant = (a * d) - (b * c);

        if (!double.IsFinite(determinant) || Math.Abs(normalizedDeterminant) <= SingularityEpsilon)
            return false;

        mapper = new AffineScreenMapper(a, b, tx, c, d, ty, determinant);
        return true;
    }

    /// <summary>Creates a mapper from OpenCV's two-row, three-column double matrix.</summary>
    /// <param name="matrix">The OpenCV matrix.</param>
    /// <param name="mapper">The mapper when the matrix is valid.</param>
    /// <returns>Whether a mapper was created.</returns>
    public static bool TryCreate(Mat? matrix, out AffineScreenMapper? mapper)
    {
        mapper = null;

        if (matrix is null || matrix.Empty())
            return false;

        var rows = matrix.Rows;
        var columns = matrix.Cols;

        if (rows != 2 || columns != 3 || matrix.Type() != MatType.CV_64FC1)
            return false;

        var coefficients = new double[]
        {
            matrix.At<double>(0, 0),
            matrix.At<double>(0, 1),
            matrix.At<double>(0, 2),
            matrix.At<double>(1, 0),
            matrix.At<double>(1, 1),
            matrix.At<double>(1, 2),
        };

        return TryCreate(coefficients, out mapper);
    }

    /// <summary>Projects a point without rounding, for residual calculation.</summary>
    /// <param name="point">The logical map point.</param>
    /// <returns>The projected pixel coordinates as doubles.</returns>
    public (double X, double Y) Project(MapPoint point)
    {
        return ((A * point.X) + (B * point.Y) + Tx, (C * point.X) + (D * point.Y) + Ty);
    }

    /// <summary>Maps a point to the nearest integer pixel, rounding midpoint values away from zero.</summary>
    /// <param name="point">The logical map point.</param>
    /// <returns>The integer window-local pixel point.</returns>
    /// <exception cref="OverflowException">A projected coordinate does not fit in an integer pixel.</exception>
    public WindowLocalPixels Map(MapPoint point)
    {
        var projected = Project(point);

        if (!double.IsFinite(projected.X) || !double.IsFinite(projected.Y)
            || projected.X < int.MinValue || projected.X > int.MaxValue
            || projected.Y < int.MinValue || projected.Y > int.MaxValue)
        {
            throw new OverflowException("The affine projection does not fit in integer pixel coordinates.");
        }

        return new WindowLocalPixels(
            checked((int)Math.Round(projected.X, MidpointRounding.AwayFromZero)),
            checked((int)Math.Round(projected.Y, MidpointRounding.AwayFromZero)));
    }
}
