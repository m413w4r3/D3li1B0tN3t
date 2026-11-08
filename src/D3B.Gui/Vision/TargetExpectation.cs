using System.Collections.Immutable;
using D3B.Gui.Calibration;
using OpenCvSharp;

namespace D3B.Gui.Vision;

/// <summary>Describes the logical target and bounded visual search constraints.</summary>
/// <remarks>The caller owns <see cref="Template"/> and remains responsible for disposing it.</remarks>
public sealed record TargetExpectation
{
    /// <summary>Gets the expected map identifier.</summary>
    public long MapId { get; }

    /// <summary>Gets the expected interactive element identifier.</summary>
    public int ElementId { get; }

    /// <summary>Gets the expected logical map cell.</summary>
    public LogicalCell Cell { get; }

    /// <summary>Gets the expected graphics identifier.</summary>
    public int GfxId { get; }

    /// <summary>Gets the optional expected skill identifier.</summary>
    public int? ExpectedSkillId { get; }

    /// <summary>Gets the caller-owned OpenCV template.</summary>
    public Mat Template { get; }

    /// <summary>Gets the square search radius in pixels; the initial default is 96 px.</summary>
    public int SearchRadiusPixels { get; }

    /// <summary>Gets the required safety margin from the image edge; the initial default is 8 px.</summary>
    public int MinMarginPixels { get; }

    /// <summary>Gets the maximum candidate-center distance from the projected cell; the initial default is 64 px.</summary>
    public int MaxDistanceFromProjectedCellPixels { get; }

    /// <summary>Gets rectangles where the target is obscured or unsafe to use.</summary>
    public ImmutableArray<WindowLocalRect> Exclusions { get; }

    /// <summary>Gets the other known logical element cells on this map.</summary>
    public ImmutableArray<LogicalCell> OtherElements { get; }

    /// <summary>Creates a target expectation and its bounded visual checks.</summary>
    public TargetExpectation(
        long mapId,
        int elementId,
        LogicalCell cell,
        int gfxId,
        int? expectedSkillId,
        Mat template,
        int searchRadiusPixels = 96,
        int minMarginPixels = 8,
        int maxDistanceFromProjectedCellPixels = 64,
        ImmutableArray<WindowLocalRect> exclusions = default,
        ImmutableArray<LogicalCell> otherElements = default)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentOutOfRangeException.ThrowIfNegative(searchRadiusPixels);
        ArgumentOutOfRangeException.ThrowIfNegative(minMarginPixels);
        ArgumentOutOfRangeException.ThrowIfNegative(maxDistanceFromProjectedCellPixels);

        MapId = mapId;
        ElementId = elementId;
        Cell = cell;
        GfxId = gfxId;
        ExpectedSkillId = expectedSkillId;
        Template = template;
        SearchRadiusPixels = searchRadiusPixels;
        MinMarginPixels = minMarginPixels;
        MaxDistanceFromProjectedCellPixels = maxDistanceFromProjectedCellPixels;
        Exclusions = exclusions.IsDefault ? ImmutableArray<WindowLocalRect>.Empty : exclusions;
        OtherElements = otherElements.IsDefault ? ImmutableArray<LogicalCell>.Empty : otherElements;
    }
}
