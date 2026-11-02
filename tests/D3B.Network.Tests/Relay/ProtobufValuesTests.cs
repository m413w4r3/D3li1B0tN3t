using System.Text;
using D3B.Network.Relay;

namespace D3B.Network.Tests.Relay;

public sealed class ProtobufValuesTests
{
    private static List<string> Texts(byte[] message)
    {
        return [.. ProtobufValues.LengthDelimited(message).Select(value => Encoding.UTF8.GetString(message, value.Start, value.Length))];
    }

    [Fact]
    public void EveryValueIsFoundAtAnyDepthOuterFirst()
    {
        var message = TestFrames.Presenting("ticket");
        var texts = Texts(message);

        Assert.Equal(5, texts.Count);
        Assert.Equal("type.test/abc", texts[1]);
        Assert.Equal("ticket", texts[4]);
    }

    [Fact]
    public void AValueThatReadsAsNoMessageIsALeafWhateverItsStartReadAs()
    {
        // The inner value starts as a message holding "ab", then cuts a field short: "ab" is no value of its own.
        var message = TestFrames.Field(1, [.. TestFrames.Field(2, Encoding.UTF8.GetBytes("ab")), 0x0A, 0x7F]);

        Assert.Single(ProtobufValues.LengthDelimited(message));
    }

    [Fact]
    public void BytesThatAreNoProtobufEndTheWalkKeepingWhatCameBefore()
    {
        byte[] message = [.. TestFrames.Field(1, Encoding.UTF8.GetBytes("kept")), 0x0A, 0x7F, 0x01];

        Assert.Equal(["kept"], Texts(message));
    }
}
