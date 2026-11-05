using System.Buffers.Binary;
using System.Text.Json;
using D3B.Diagnostic.Contract;
using D3B.Diagnostic.Replay;

namespace D3B.Diagnostic.Ipc;

/// <summary>Stateful length-prefixed JSON client for one diagnostic engine session.</summary>
public sealed class DiagnosticSidecarClient : IDiagnosticSidecar
{
    private readonly Stream _stream;
    private readonly SidecarOptions _options;
    private readonly SidecarProcess? _process;
    private bool _started;
    private bool _completed;
    private bool _disposed;
    private long _previousSequence = -1;
    private DiagnosticSchema? _schema;

    /// <summary>Creates a client over an already connected stream.</summary>
    public DiagnosticSidecarClient(Stream stream, SidecarOptions options, SidecarProcess? process = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(options);
        _stream = stream;
        _options = options;
        _process = process;
    }

    /// <inheritdoc />
    public async Task<DiagnosticEngineHello> StartAsync(DiagnosticSchema schema, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(schema);
        if (_started)
        {
            throw new InvalidOperationException("The sidecar session has already started.");
        }

        _started = true;
        _schema = schema;
        try
        {
            using var timeout = CreateTimeout(_options.HandshakeTimeout, cancellationToken);
            await WriteMessageAsync(DiagnosticJson.SerializeHello(new DiagnosticHello
            {
                ClientVersion = schema.ClientVersion,
                SchemaSha256 = schema.SchemaSha256,
            }), timeout.Token).ConfigureAwait(false);
            await WriteMessageAsync(DiagnosticJson.SerializeSchema(schema), timeout.Token).ConfigureAwait(false);
            byte[] response = await ReadMessageAsync(timeout.Token).ConfigureAwait(false);
            await ThrowIfEngineErrorAsync(response).ConfigureAwait(false);
            DiagnosticEngineHello hello = DiagnosticJson.DeserializeHello(response);
            if (hello.Kind != "hello" || hello.Engine != "d3b-diagnostic" ||
                hello.ProtocolVersion != DiagnosticProtocol.ProtocolVersion)
            {
                throw new DiagnosticProtocolException("The engine hello is incompatible.");
            }

            return hello;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new DiagnosticSidecarException(DiagnosticErrorCode.IoError);
        }
    }

    /// <inheritdoc />
    public async Task<DiagnosticAnalysis> AnalyzeAsync(DiagnosticFrame frame, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(frame);
        EnsureReady();
        if (frame.Index <= _previousSequence)
        {
            throw new DiagnosticProtocolException("Frame sequences must be strictly increasing.");
        }

        try
        {
            using var timeout = CreateTimeout(_options.ResponseTimeout, cancellationToken);
            byte[] request = SerializeFrame(frame);
            await WriteMessageAsync(request, timeout.Token).ConfigureAwait(false);
            byte[] response = await ReadMessageAsync(timeout.Token).ConfigureAwait(false);
            await ThrowIfEngineErrorAsync(response).ConfigureAwait(false);
            DiagnosticAnalysis analysis = DiagnosticJson.DeserializeAnalysis(ConvertSequenceToIndex(response));
            if (analysis.Kind != "analysis" || analysis.Index != frame.Index ||
                analysis.Direction != frame.Direction || analysis.CarriedType != frame.CarriedType ||
                analysis.TypeUrlOk != IsTypeUrlValid(frame))
            {
                throw new DiagnosticProtocolException("The engine analysis did not match its input frame.");
            }

            _previousSequence = frame.Index;
            return analysis;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new DiagnosticSidecarException(DiagnosticErrorCode.IoError);
        }
    }

    /// <inheritdoc />
    public async Task<DiagnosticSummary> CompleteAsync(CancellationToken cancellationToken = default)
    {
        EnsureReady();
        try
        {
            using var timeout = CreateTimeout(_options.ResponseTimeout, cancellationToken);
            await WriteMessageAsync("{\"kind\":\"bye\"}"u8.ToArray(), timeout.Token).ConfigureAwait(false);
            byte[] response = await ReadMessageAsync(timeout.Token).ConfigureAwait(false);
            await ThrowIfEngineErrorAsync(response).ConfigureAwait(false);
            DiagnosticSummary summary = DiagnosticJson.DeserializeSummary(response);
            if (summary.Kind != "summary" || summary.ProtocolVersion != DiagnosticProtocol.ProtocolVersion ||
                summary.ClientVersion != _schema!.ClientVersion || summary.SchemaSha256 != _schema.SchemaSha256)
            {
                throw new DiagnosticProtocolException("The engine summary is incompatible.");
            }

            _completed = true;
            return summary;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new DiagnosticSidecarException(DiagnosticErrorCode.IoError);
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await _stream.DisposeAsync().ConfigureAwait(false);
        if (_process is not null)
        {
            await _process.DisposeAsync().ConfigureAwait(false);
        }
    }

    private void EnsureReady()
    {
        if (!_started || _completed || _disposed)
        {
            throw new InvalidOperationException("The sidecar session is not ready for this operation.");
        }
    }

    private async Task WriteMessageAsync(byte[] json, CancellationToken cancellationToken)
    {
        if (json.Length is 0 or > DiagnosticProtocol.MessageLimit)
        {
            throw new DiagnosticProtocolException("The IPC message length is invalid.");
        }

        byte[] prefix = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(prefix, json.Length);
        await _stream.WriteAsync(prefix, cancellationToken).ConfigureAwait(false);
        await _stream.WriteAsync(json, cancellationToken).ConfigureAwait(false);
        await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<byte[]> ReadMessageAsync(CancellationToken cancellationToken)
    {
        byte[] prefix = new byte[sizeof(int)];
        await ReadExactlyAsync(prefix, cancellationToken).ConfigureAwait(false);
        int length = BinaryPrimitives.ReadInt32BigEndian(prefix);
        if (length is <= 0 or > DiagnosticProtocol.MessageLimit)
        {
            throw new DiagnosticProtocolException("The IPC message length is invalid.");
        }

        byte[] json = new byte[length];
        await ReadExactlyAsync(json, cancellationToken).ConfigureAwait(false);
        return json;
    }

    private async Task ReadExactlyAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        int offset = 0;
        while (offset < buffer.Length)
        {
            int read = await _stream.ReadAsync(buffer[offset..], cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                throw new DiagnosticProtocolException("The engine closed the IPC stream unexpectedly.");
            }

            offset += read;
        }
    }

    private static byte[] SerializeFrame(DiagnosticFrame frame)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("kind", "frame");
            writer.WriteNumber("sequence", frame.Index);
            writer.WriteString("direction", frame.Direction);
            if (frame.TypeUrl is null) writer.WriteNull("typeUrl"); else writer.WriteString("typeUrl", frame.TypeUrl);
            if (frame.CarriedType is null) writer.WriteNull("carriedType"); else writer.WriteString("carriedType", frame.CarriedType);
            writer.WriteString("payloadHex", Convert.ToHexStringLower(frame.Payload));
            writer.WriteEndObject();
        }

        byte[] json = buffer.ToArray();
        if (json.Length > DiagnosticProtocol.MessageLimit)
        {
            throw new DiagnosticProtocolException("The IPC message exceeds the protocol limit.");
        }

        return json;
    }

    private static bool IsTypeUrlValid(DiagnosticFrame frame)
    {
        return frame.TypeUrl is not null && frame.CarriedType is not null &&
            string.Equals(frame.TypeUrl, DiagnosticProtocol.TypeUrlPrefix + frame.CarriedType, StringComparison.Ordinal);
    }

    private static byte[] ConvertSequenceToIndex(byte[] json)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("sequence", out JsonElement sequence) ||
                root.TryGetProperty("index", out _))
            {
                throw new DiagnosticProtocolException("The engine analysis sequence is invalid.");
            }

            using var buffer = new MemoryStream();
            using (var writer = new Utf8JsonWriter(buffer))
            {
                writer.WriteStartObject();
                foreach (JsonProperty property in root.EnumerateObject())
                {
                    if (property.NameEquals("sequence"))
                    {
                        writer.WritePropertyName("index");
                        sequence.WriteTo(writer);
                    }
                    else
                    {
                        property.WriteTo(writer);
                    }
                }

                writer.WriteEndObject();
            }

            return buffer.ToArray();
        }
        catch (JsonException)
        {
            throw new DiagnosticProtocolException("The engine response was malformed.");
        }
    }

    private static Task ThrowIfEngineErrorAsync(byte[] json)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind == JsonValueKind.Object &&
                document.RootElement.TryGetProperty("kind", out JsonElement kind) &&
                kind.ValueKind == JsonValueKind.String && kind.GetString() == "error")
            {
                DiagnosticError error = DiagnosticJson.DeserializeError(json);
                throw new DiagnosticSidecarException(error.Code);
            }

            return Task.CompletedTask;
        }
        catch (JsonException)
        {
            throw new DiagnosticProtocolException("The engine response was malformed.");
        }
    }

    private static CancellationTokenSource CreateTimeout(TimeSpan duration, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(duration, TimeSpan.Zero);

        var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(duration);
        return timeout;
    }
}
