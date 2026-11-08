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

    /// <summary>Gets the pixel offset from the projected cell center to the expected visual center.</summary>
    public WindowLocalOffset AnchorOffset { get; }

    /// <summary>Gets the pixel offset from the matched template center to the proposed click point.</summary>
    public WindowLocalOffset ClickOffset { get; }

    /// <summary>Gets the square search radius in pixels; the default is 96 px.</summary>
    public int SearchRadiusPixels { get; }

    /// <summary>Gets the required safety margin from the image edge; the initial default is 8 px.</summary>
    public int MinMarginPixels { get; }

    /// <summary>Gets the maximum candidate-center distance from the expected visual center; the default is 64 px.</summary>
    public int MaxCenterDistancePixels { get; }

    /// <summary>Gets the legacy name for <see cref="MaxCenterDistancePixels"/>.</summary>
    public int MaxDistanceFromProjectedCellPixels => MaxCenterDistancePixels;

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
        int maxCenterDistancePixels = 64,
        ImmutableArray<WindowLocalRect> exclusions = default,
        ImmutableArray<LogicalCell> otherElements = default,
        WindowLocalOffset anchorOffset = default,
        WindowLocalOffset clickOffset = default)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(searchRadiusPixels);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(searchRadiusPixels, 4096);
        ArgumentOutOfRangeException.ThrowIfNegative(minMarginPixels);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(minMarginPixels, 4096);
        ArgumentOutOfRangeException.ThrowIfNegative(maxCenterDistancePixels);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maxCenterDistancePixels, 8192);

        MapId = mapId;
        ElementId = elementId;
        Cell = cell;
        GfxId = gfxId;
        ExpectedSkillId = expectedSkillId;
        Template = template;
        AnchorOffset = anchorOffset;
        ClickOffset = clickOffset;
        SearchRadiusPixels = searchRadiusPixels;
        MinMarginPixels = minMarginPixels;
        MaxCenterDistancePixels = maxCenterDistancePixels;
        Exclusions = ValidateExclusions(exclusions.IsDefault ? ImmutableArray<WindowLocalRect>.Empty : exclusions);
        OtherElements = otherElements.IsDefault ? ImmutableArray<LogicalCell>.Empty : otherElements;
    }

    private static ImmutableArray<WindowLocalRect> ValidateExclusions(ImmutableArray<WindowLocalRect> exclusions)
    {
        foreach (var exclusion in exclusions)
        {
            if (exclusion.X < 0 || exclusion.Y < 0
                || exclusion.Width <= 0 || exclusion.Height <= 0
                || (long)exclusion.X + exclusion.Width > int.MaxValue
                || (long)exclusion.Y + exclusion.Height > int.MaxValue)
            {
                throw new ArgumentException("Every exclusion must have positive dimensions and representable window-local bounds.", nameof(exclusions));
            }
        }

        return exclusions;
    }
}
