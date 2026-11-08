using D3B.Gui.Safety;

namespace D3B.Gui.Tests.Safety;

public sealed class GuiOptionsTests
{
    [Fact]
    public void DefaultsMatchTheP2NConfiguration()
    {
        var options = new GuiOptions();

        Assert.False(options.Enabled);
        Assert.Equal(GuiBackend.X11, options.Backend);
        Assert.Equal(GuiMode.Preview, options.Mode);
        Assert.False(options.AllowRealInput);
        Assert.Equal(2000, options.CaptureTimeoutMs);
        Assert.Equal(16777216, options.MaxCaptureBytes);
        Assert.Equal(3840, options.MaxWidth);
        Assert.Equal(2160, options.MaxHeight);
        Assert.Equal(30, options.CalibrationMaxAgeSeconds);
    }

    [Fact]
    public void DefaultOptionsAreValid()
    {
        var result = GuiOptionsValidator.Validate(new GuiOptions());

        Assert.True(result.IsValid);
        Assert.Empty(result.Problems);
    }

    [Fact]
    public void RealInputIsRefused()
    {
        var result = GuiOptionsValidator.Validate(new GuiOptions { AllowRealInput = true });

        Assert.False(result.IsValid);
        Assert.Contains(result.Problems, problem => problem.Contains("real input is refused", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(99)]
    [InlineData(10001)]
    public void CaptureTimeoutMustBeWithinItsBounds(int value)
    {
        AssertInvalid(new GuiOptions { CaptureTimeoutMs = value }, "CaptureTimeoutMs");
    }

    [Theory]
    [InlineData(1048575)]
    [InlineData(67108865)]
    public void CaptureBytesMustBeWithinTheirBounds(int value)
    {
        AssertInvalid(new GuiOptions { MaxCaptureBytes = value }, "MaxCaptureBytes");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7681)]
    public void WidthMustBeWithinItsBounds(int value)
    {
        AssertInvalid(new GuiOptions { MaxWidth = value }, "MaxWidth");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7681)]
    public void HeightMustBeWithinItsBounds(int value)
    {
        AssertInvalid(new GuiOptions { MaxHeight = value }, "MaxHeight");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3601)]
    public void CalibrationAgeMustBeWithinItsBounds(int value)
    {
        AssertInvalid(new GuiOptions { CalibrationMaxAgeSeconds = value }, "CalibrationMaxAgeSeconds");
    }

    [Fact]
    public void UndefinedBackendIsRejected()
    {
        AssertInvalid(new GuiOptions { Backend = (GuiBackend)99 }, "Backend");
    }

    [Fact]
    public void UndefinedModeIsRejected()
    {
        AssertInvalid(new GuiOptions { Mode = (GuiMode)99 }, "Mode");
    }

    [Fact]
    public void InclusiveOptionBoundsAreValid()
    {
        var result = GuiOptionsValidator.Validate(new GuiOptions
        {
            CaptureTimeoutMs = 100,
            MaxCaptureBytes = 1_048_576,
            MaxWidth = 1,
            MaxHeight = 7680,
            CalibrationMaxAgeSeconds = 3600,
        });

        Assert.True(result.IsValid);
        Assert.Empty(result.Problems);
    }

    private static void AssertInvalid(GuiOptions options, string optionName)
    {
        var result = GuiOptionsValidator.Validate(options);

        Assert.False(result.IsValid);
        Assert.Contains(result.Problems, problem => problem.Contains(optionName, StringComparison.Ordinal));
    }
}
