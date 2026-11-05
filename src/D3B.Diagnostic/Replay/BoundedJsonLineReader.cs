using System.Text;
using D3B.Diagnostic.Contract;

namespace D3B.Diagnostic.Replay;

internal sealed class BoundedJsonLineReader
{
    private readonly Stream _stream;

    public BoundedJsonLineReader(Stream stream)
    {
        _stream = stream;
    }

    public string? ReadLine()
    {
        using var bytes = new MemoryStream();
        while (true)
        {
            var value = _stream.ReadByte();
            if (value < 0)
            {
                if (bytes.Length == 0)
                {
                    return null;
                }

                break;
            }

            if (value == '\n')
            {
                break;
            }

            if (bytes.Length >= DiagnosticProtocol.MessageLimit)
            {
                throw new DiagnosticProtocolException("A replay line exceeds the protocol limit.");
            }

            bytes.WriteByte((byte)value);
        }

        var data = bytes.ToArray();
        if (data.Length > 0 && data[^1] == '\r')
        {
            Array.Resize(ref data, data.Length - 1);
        }

        try
        {
            return new UTF8Encoding(false, true).GetString(data);
        }
        catch (DecoderFallbackException)
        {
            throw new DiagnosticProtocolException("A replay line is not valid UTF-8.");
        }
    }

    public async Task<string?> ReadLineAsync(CancellationToken cancellationToken)
    {
        using var bytes = new MemoryStream();
        var oneByte = new byte[1];
        while (true)
        {
            var read = await _stream.ReadAsync(oneByte, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                if (bytes.Length == 0)
                {
                    return null;
                }

                break;
            }

            if (oneByte[0] == (byte)'\n')
            {
                break;
            }

            if (bytes.Length >= DiagnosticProtocol.MessageLimit)
            {
                throw new DiagnosticProtocolException("A replay line exceeds the protocol limit.");
            }

            bytes.WriteByte(oneByte[0]);
        }

        var data = bytes.ToArray();
        if (data.Length > 0 && data[^1] == (byte)'\r')
        {
            Array.Resize(ref data, data.Length - 1);
        }

        try
        {
            return new UTF8Encoding(false, true).GetString(data);
        }
        catch (DecoderFallbackException)
        {
            throw new DiagnosticProtocolException("A replay line is not valid UTF-8.");
        }
    }
}
