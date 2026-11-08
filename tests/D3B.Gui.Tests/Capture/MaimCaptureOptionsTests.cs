using D3B.Gui.Capture;

namespace D3B.Gui.Tests.Capture;

public sealed class MaimCaptureOptionsTests
{
    [Fact]
    public void AcceptsTheMandatedMaximums()
    {
        MaimCaptureOptions options = new(
            "/usr/bin/maim",
            TimeSpan.FromMilliseconds(MaimCaptureOptions.AbsoluteMaximumTimeoutMilliseconds),
            MaimCaptureOptions.AbsoluteMaximumCaptureBytes,
            MaimCaptureOptions.AbsoluteMaximumWidth,
            MaimCaptureOptions.AbsoluteMaximumHeight);

        Assert.Equal(TimeSpan.FromMilliseconds(2_000), options.Timeout);
        Assert.Equal(16_777_216, options.MaxCaptureBytes);
        Assert.Equal(3_840, options.MaxWidth);
        Assert.Equal(2_160, options.MaxHeight);
    }

    [Fact]
    public void RejectsTimeoutAboveTwoSeconds()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new MaimCaptureOptions(timeout: TimeSpan.FromMilliseconds(2_001)));
    }

    [Fact]
    public void RejectsCaptureBytesAboveSixteenMebibytes()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new MaimCaptureOptions(maxCaptureBytes: 16_777_217));
    }

    [Fact]
    public void RejectsWidthAboveThreeThousandEightHundredFortyPixels()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new MaimCaptureOptions(maxWidth: 3_841));
    }

    [Fact]
    public void RejectsHeightAboveTwoThousandOneHundredSixtyPixels()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new MaimCaptureOptions(maxHeight: 2_161));
    }
}
