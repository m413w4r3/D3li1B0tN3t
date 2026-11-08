using OpenCvSharp;

namespace D3B.Gui.Tests;

public sealed class OpenCvNativeSmokeTests
{
    [Fact]
    public void OpenCvLoadsItsNativeLibraryAndEncodesPng()
    {
        using var source = new Mat(8, 8, MatType.CV_8UC3, Scalar.All(0));
        source.Set(0, 0, new Vec3b(0, 0, 255));

        Assert.True(Cv2.ImEncode(".png", source, out var encoded));

        using var decoded = Cv2.ImDecode(encoded, ImreadModes.Color);

        Assert.Equal(8, decoded.Rows);
        Assert.Equal(8, decoded.Cols);
        Assert.False(string.IsNullOrWhiteSpace(Cv2.GetVersionString()));
    }
}
