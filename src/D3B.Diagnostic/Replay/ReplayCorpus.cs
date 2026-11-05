using System.Text;
using System.Text.Json;
using D3B.Diagnostic.Contract;

namespace D3B.Diagnostic.Replay;

/// <summary>A validated replay input corpus.</summary>
public sealed class ReplayCorpus
{
    private ReplayCorpus(DiagnosticHello hello, DiagnosticSchema schema, IReadOnlyList<DiagnosticFrame> frames)
    {
        Hello = hello;
        Schema = schema;
        Frames = frames;
    }

    /// <summary>The corpus handshake.</summary>
    public DiagnosticHello Hello { get; }

    /// <summary>The in-band schema.</summary>
    public DiagnosticSchema Schema { get; }

    /// <summary>The frames in their input order.</summary>
    public IReadOnlyList<DiagnosticFrame> Frames { get; }

    /// <summary>Reads and validates a JSONL replay corpus.</summary>
    public static ReplayCorpus Read(Stream input)
    {
        ArgumentNullException.ThrowIfNull(input);
        try
        {
            return ReadCore(input);
        }
        catch (DiagnosticProtocolException)
        {
            throw;
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException or InvalidOperationException or
            KeyNotFoundException or OverflowException or JsonException)
        {
            throw new DiagnosticProtocolException("The replay corpus is malformed.");
        }
    }

    private static ReplayCorpus ReadCore(Stream input)
    {
        var reader = new BoundedJsonLineReader(input);
        var helloLine = reader.ReadLine();
        if (helloLine is null)
        {
            throw new DiagnosticProtocolException("The replay corpus is empty.");
        }

        var hello = DiagnosticJson.DeserializeClientHello(Encoding.UTF8.GetBytes(helloLine));
        if (hello.Kind != "hello" || hello.ProtocolVersion != DiagnosticProtocol.ProtocolVersion)
        {
            throw new DiagnosticProtocolException("The replay protocol version is unsupported.");
        }

        if (hello.SchemaSha256.Length != 64 || hello.SchemaSha256.Any(character =>
            !(character is >= '0' and <= '9' or >= 'a' and <= 'f')))
        {
            throw new DiagnosticProtocolException("The replay schema fingerprint is invalid.");
        }
        var schemaLine = reader.ReadLine() ?? throw new DiagnosticProtocolException("The replay corpus has no schema line.");
        var parsedSchema = DiagnosticJson.DeserializeSchema(Encoding.UTF8.GetBytes(schemaLine));
        var schema = parsedSchema with { ClientVersion = hello.ClientVersion, SchemaSha256 = hello.SchemaSha256 };
        var frames = new List<DiagnosticFrame>();
        long previousIndex = -1;
        var ended = false;
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            using var document = ParseLine(line);
            var root = document.RootElement;
            var kind = root.GetProperty("kind").GetString();
            if (kind == "end")
            {
                if (ended || reader.ReadLine() is not null)
                {
                    throw new DiagnosticProtocolException("The replay corpus has data after end.");
                }

                ended = true;
                break;
            }

            if (kind != "frame")
            {
                throw new DiagnosticProtocolException("The replay corpus contains an unexpected line.");
            }

            var index = root.GetProperty("index").GetInt64();
            if (index < 0 || index <= previousIndex)
            {
                throw new DiagnosticProtocolException("Replay frame indices must be strictly increasing.");
            }

            var payloadHex = root.GetProperty("payloadHex").GetString() ?? string.Empty;
            if ((payloadHex.Length & 1) != 0 || payloadHex.Length > 2_097_152)
            {
                throw new DiagnosticProtocolException("Replay payload size or encoding is invalid.");
            }

            if (payloadHex.Any(character => !(character is >= '0' and <= '9' or >= 'a' and <= 'f')))
            {
                throw new DiagnosticProtocolException("Replay payload encoding is invalid.");
            }

            byte[] payload;
            try
            {
                payload = Convert.FromHexString(payloadHex);
            }
            catch (FormatException)
            {
                throw new DiagnosticProtocolException("Replay payload encoding is invalid.");
            }

            frames.Add(new DiagnosticFrame(
                index,
                root.GetProperty("direction").GetString() ?? string.Empty,
                GetNullableString(root, "typeUrl"),
                GetNullableString(root, "carriedType"),
                payload));
            previousIndex = index;
        }

        if (!ended)
        {
            throw new DiagnosticProtocolException("The replay corpus has no end line.");
        }

        return new ReplayCorpus(hello, schema, frames);
    }

    private static string? GetNullableString(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out JsonElement value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return value.GetString();
    }

    private static JsonDocument ParseLine(string line)
    {
        try
        {
            return JsonDocument.Parse(line);
        }
        catch (JsonException)
        {
            throw new DiagnosticProtocolException("A replay line is malformed JSON.");
        }
    }
}
