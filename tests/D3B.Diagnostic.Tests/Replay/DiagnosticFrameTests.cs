using D3B.Diagnostic.Replay;

namespace D3B.Diagnostic.Tests.Replay;

public sealed class DiagnosticFrameTests
{
    [Fact]
    public void AcceptsNullAndMaximumLengthMetadata()
    {
        var frame = new DiagnosticFrame(0, "server", new string('é', 128), new string('a', 128), []);
        var nullMetadataFrame = new DiagnosticFrame(0, "server", null, null, []);

        Assert.Equal(256, System.Text.Encoding.UTF8.GetByteCount(frame.TypeUrl!));
        Assert.Null(nullMetadataFrame.TypeUrl);
        Assert.Null(nullMetadataFrame.CarriedType);
    }

    [Fact]
    public void RejectsTypeUrlLongerThan256Bytes()
    {
        string typeUrl = new('é', 129);

        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => new DiagnosticFrame(0, "server", typeUrl, null, []));

        Assert.Equal("typeUrl", exception.ParamName);
    }

    [Fact]
    public void RejectsEmptyCarriedType()
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => new DiagnosticFrame(0, "server", null, string.Empty, []));

        Assert.Equal("carriedType", exception.ParamName);
    }

    [Fact]
    public void RejectsCarriedTypeLongerThan128Bytes()
    {
        string carriedType = new('a', 129);

        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => new DiagnosticFrame(0, "server", null, carriedType, []));

        Assert.Equal("carriedType", exception.ParamName);
    }

    [Fact]
    public void RejectsCarriedTypeOutsideAllowedAlphabet()
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => new DiagnosticFrame(0, "server", null, "has-hyphen", []));

        Assert.Equal("carriedType", exception.ParamName);
    }
}
