using D3B.Diagnostic.Ipc;

namespace D3B.Diagnostic.Tests.Ipc;

public sealed class SidecarOptionsTests
{
    [Theory]
    [InlineData(1_048_575)]
    [InlineData(1_073_741_825)]
    public void QueueByteCapacityOutsideSupportedRangeIsRejected(long capacity)
    {
        SidecarOptions options = new() { QueueByteCapacity = capacity };

        Assert.Throws<ArgumentOutOfRangeException>(options.Validate);
    }

    [Fact]
    public void QueueByteCapacityDefaultsToSixteenMiB()
    {
        var options = new SidecarOptions();

        Assert.Equal(16L * 1024 * 1024, options.QueueByteCapacity);
        options.Validate();
    }
}
