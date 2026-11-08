using System.Collections.Immutable;
using D3B.Data.Maps;
using D3B.Engine.Maps;
using D3B.Gui.Calibration;
using D3B.Gui.Capture;
using D3B.Gui.Windowing;

namespace D3B.Gui.Vision;

/// <summary>Projects a terrain cell center and rejects points outside measured safe bounds.</summary>
/// <remarks>
/// A verified result proves only affine projection, frame/client-area bounds, and exclusion checks. It does not
/// recognize terrain, a walkable cell, or any game-scene content.
/// </remarks>
public static class TerrainCellTargetLocator
{
    /// <summary>Locates the projected center of a terrain cell without performing image recognition or input.</summary>
    public static TargetRegion Locate(
        CapturedFrame frame,
        CalibrationProfile calibration,
        long mapId,
        LogicalCell targetCell,
        ClientAreaGeometry clientAreaGeometry,
        int safetyMargin,
        ImmutableArray<WindowLocalRect> exclusions,
        IReadOnlySet<int> interactiveElementCells)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(calibration);
        ArgumentNullException.ThrowIfNull(interactiveElementCells);

        ImmutableArray<TargetEvidence>.Builder evidence = ImmutableArray.CreateBuilder<TargetEvidence>();
        WindowLocalPixels center = default;

        if (safetyMargin < 0)
            return Unverified(TargetEvidence.InvalidSafetyMargin);

        if (mapId <= 0)
            return Unverified(TargetEvidence.InvalidMapId);

        if (mapId != calibration.MapId)
            return Unverified(TargetEvidence.MapMismatch);

        if (frame.Status != CaptureStatus.Captured
            || !frame.ContentUsable
            || !HasUsableImage(frame))
        {
            return Unverified(TargetEvidence.FrameUnusable);
        }

        if (frame.PixelWidth != frame.Image.Cols
            || frame.PixelHeight != frame.Image.Rows
            || frame.PixelWidth != calibration.CaptureWidth
            || frame.PixelHeight != calibration.CaptureHeight
            || frame.PixelWidth != frame.WindowSnapshotAfter.WindowWidth
            || frame.PixelHeight != frame.WindowSnapshotAfter.WindowHeight)
        {
            return Unverified(TargetEvidence.FrameBindingMismatch);
        }

        if (!IsSameIdentity(calibration.WindowIdentity, frame.WindowIdentity)
            || calibration.WindowIdentity.Status != GameWindowIdentityStatus.Verified)
        {
            return Unverified(TargetEvidence.WindowMismatch);
        }

        if (!FrameObservationsMatch(frame)
            || !GeometryMatches(clientAreaGeometry, frame.WindowSnapshotAfter)
            || clientAreaGeometry != calibration.ClientAreaGeometry)
        {
            return Unverified(TargetEvidence.FrameBindingMismatch);
        }

        if (!IsValidClientArea(clientAreaGeometry, frame.PixelWidth, frame.PixelHeight))
            return Unverified(TargetEvidence.InvalidClientAreaGeometry);

        if (!AreValidInteractiveCells(interactiveElementCells))
            return Unverified(TargetEvidence.InvalidInteractiveCell);

        if (!TryProject(calibration, targetCell, out center))
            return Unverified(TargetEvidence.ProjectionUnrepresentable);

        evidence.Add(TargetEvidence.ProjectedByCalibration);

        if (center.X < 0 || center.Y < 0 || center.X >= frame.PixelWidth || center.Y >= frame.PixelHeight)
            return Unverified(TargetEvidence.RegionOutOfFrame);

        long left = clientAreaGeometry.OriginInWindow.X;
        long top = clientAreaGeometry.OriginInWindow.Y;
        long right = left + clientAreaGeometry.Width;
        long bottom = top + clientAreaGeometry.Height;

        if (center.X < left || center.X >= right || center.Y < top || center.Y >= bottom)
            return Unverified(TargetEvidence.OutsideClientArea);

        if (center.X < left + safetyMargin
            || center.X >= right - safetyMargin
            || center.Y < top + safetyMargin
            || center.Y >= bottom - safetyMargin)
        {
            return Unverified(TargetEvidence.InsufficientClientAreaMargin);
        }

        evidence.Add(TargetEvidence.InsideFrame);

        ImmutableArray<WindowLocalRect> checkedExclusions = exclusions.IsDefault
            ? ImmutableArray<WindowLocalRect>.Empty
            : exclusions;

        foreach (WindowLocalRect exclusion in checkedExclusions)
        {
            if (!IsValidExclusion(exclusion, frame.PixelWidth, frame.PixelHeight))
                return Unverified(TargetEvidence.InvalidExclusion);
        }

        foreach (WindowLocalRect exclusion in checkedExclusions)
        {
            if (exclusion.Contains(center))
                return Unverified(TargetEvidence.BlockedByExclusion);
        }

        evidence.Add(TargetEvidence.ClearOfExclusions);

        if (interactiveElementCells.Contains(targetCell.Value))
            return Unverified(TargetEvidence.InteractiveElementCell);

        return new TargetRegion(
            mapId,
            0,
            targetCell,
            0,
            null,
            center,
            null,
            center,
            new TargetConfidence(0, -1, 0, 0, 0, evidence.ToImmutable()),
            TargetStatus.Verified,
            null,
            frame.CaptureId,
            calibration.CalibrationId,
            center,
            Math.Max(0, frame.PixelWidth),
            Math.Max(0, frame.PixelHeight));

        TargetRegion Unverified(TargetEvidence reason)
        {
            evidence.Add(reason);
            return new TargetRegion(
                mapId,
                0,
                targetCell,
                0,
                null,
                center,
                null,
                null,
                new TargetConfidence(0, -1, 0, 0, 0, evidence.ToImmutable()),
                TargetStatus.Unverified,
                reason,
                frame.CaptureId,
                calibration.CalibrationId,
                center,
                Math.Max(0, frame.PixelWidth),
                Math.Max(0, frame.PixelHeight));
        }
    }

    private static bool HasUsableImage(CapturedFrame frame)
    {
        try
        {
            return frame.PixelWidth > 0
                && frame.PixelHeight > 0
                && !frame.Image.Empty();
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
    }

    private static bool FrameObservationsMatch(CapturedFrame frame)
    {
        GameWindowSnapshot before = frame.WindowSnapshotBefore;
        GameWindowSnapshot after = frame.WindowSnapshotAfter;

        return IsSameIdentity(frame.WindowIdentity, before.Identity)
            && IsSameIdentity(frame.WindowIdentity, after.Identity)
            && before.Identity.Status == GameWindowIdentityStatus.Verified
            && after.Identity.Status == GameWindowIdentityStatus.Verified
            && before.Exists
            && after.Exists
            && before.IsMapped
            && after.IsMapped
            && before.IsViewable
            && after.IsViewable
            && !before.IsMinimized
            && !after.IsMinimized
            && before.IsMapped == after.IsMapped
            && before.IsViewable == after.IsViewable
            && before.IsMinimized == after.IsMinimized
            && before.GeometryReliable
            && after.GeometryReliable
            && before.DesktopOrigin == after.DesktopOrigin
            && before.WindowWidth == after.WindowWidth
            && before.WindowHeight == after.WindowHeight
            && before.BorderWidth == after.BorderWidth
            && before.ClientWidth == after.ClientWidth
            && before.ClientHeight == after.ClientHeight;
    }

    private static bool GeometryMatches(ClientAreaGeometry geometry, GameWindowSnapshot window)
    {
        long expectedDesktopX = (long)window.DesktopOrigin.X + window.BorderWidth;
        long expectedDesktopY = (long)window.DesktopOrigin.Y + window.BorderWidth;

        return geometry.Width == window.ClientWidth
            && geometry.Height == window.ClientHeight
            && geometry.OriginInWindow.X == window.BorderWidth
            && geometry.OriginInWindow.Y == window.BorderWidth
            && geometry.OriginDesktop.X == expectedDesktopX
            && geometry.OriginDesktop.Y == expectedDesktopY;
    }

    private static bool IsValidClientArea(ClientAreaGeometry geometry, int frameWidth, int frameHeight)
    {
        long right = (long)geometry.OriginInWindow.X + geometry.Width;
        long bottom = (long)geometry.OriginInWindow.Y + geometry.Height;

        return geometry.Width > 0
            && geometry.Height > 0
            && geometry.OriginInWindow.X >= 0
            && geometry.OriginInWindow.Y >= 0
            && right <= frameWidth
            && bottom <= frameHeight;
    }

    private static bool IsValidExclusion(WindowLocalRect exclusion, int frameWidth, int frameHeight)
    {
        long right = (long)exclusion.X + exclusion.Width;
        long bottom = (long)exclusion.Y + exclusion.Height;

        return exclusion.Width > 0
            && exclusion.Height > 0
            && exclusion.X >= 0
            && exclusion.Y >= 0
            && right <= frameWidth
            && bottom <= frameHeight;
    }

    private static bool AreValidInteractiveCells(IReadOnlySet<int> cells)
    {
        foreach (int cell in cells)
        {
            if (cell < 0 || cell >= MapData.CellCount)
                return false;
        }

        return true;
    }

    private static bool TryProject(CalibrationProfile calibration, LogicalCell cell, out WindowLocalPixels point)
    {
        try
        {
            point = calibration.Mapper.Map(MapGeometry.PointOf(cell.Value));
            return true;
        }
        catch (OverflowException)
        {
            point = default;
            return false;
        }
    }

    private static bool IsSameIdentity(GameWindowIdentity expected, GameWindowIdentity actual)
    {
        return expected.Xid == actual.Xid
            && expected.ProcessId == actual.ProcessId
            && string.Equals(expected.ExecutablePath, actual.ExecutablePath, StringComparison.Ordinal)
            && expected.Status == actual.Status;
    }
}
