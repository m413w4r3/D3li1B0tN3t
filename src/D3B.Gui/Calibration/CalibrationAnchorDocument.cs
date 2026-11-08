using System.Collections.Immutable;
using System.Text.Json;
using D3B.Engine.Maps;
using D3B.Gui.Windowing;

namespace D3B.Gui.Calibration;

/// <summary>A versioned JSON set of manually annotated map-cell screen points.</summary>
public sealed record CalibrationAnchorDocument
{
    /// <summary>The supported annotation document version.</summary>
    public const int CurrentFormatVersion = 1;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    /// <summary>Gets the document format version.</summary>
    public int FormatVersion { get; }

    /// <summary>Gets the annotated client build.</summary>
    public string ClientBuild { get; }

    /// <summary>Gets the annotated map identifier.</summary>
    public long MapId { get; }

    /// <summary>Gets the annotated image width.</summary>
    public int ImageWidth { get; }

    /// <summary>Gets the annotated image height.</summary>
    public int ImageHeight { get; }

    /// <summary>Gets the fit and validation annotations.</summary>
    public ImmutableArray<CalibrationAnchor> Anchors { get; }

    /// <summary>Gets whether this document is explicitly labelled synthetic.</summary>
    public bool Synthetic { get; }

    /// <summary>Creates a validated version 1 annotation document.</summary>
    public CalibrationAnchorDocument(
        string clientBuild,
        long mapId,
        int imageWidth,
        int imageHeight,
        ImmutableArray<CalibrationAnchor> anchors,
        bool synthetic = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientBuild);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(imageWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(imageHeight);

        var normalizedAnchors = anchors.IsDefault ? ImmutableArray<CalibrationAnchor>.Empty : anchors;
        foreach (var anchor in normalizedAnchors)
        {
            if (anchor.MeasuredPixelPoint.X < 0 || anchor.MeasuredPixelPoint.X >= imageWidth
                || anchor.MeasuredPixelPoint.Y < 0 || anchor.MeasuredPixelPoint.Y >= imageHeight)
            {
                throw new ArgumentOutOfRangeException(nameof(anchors), "An annotation lies outside the image bounds.");
            }
        }

        ClientBuild = clientBuild;
        FormatVersion = CurrentFormatVersion;
        MapId = mapId;
        ImageWidth = imageWidth;
        ImageHeight = imageHeight;
        Anchors = normalizedAnchors;
        Synthetic = synthetic;
    }

    /// <summary>Loads and validates a versioned annotation document from a stream.</summary>
    /// <param name="stream">The JSON stream; it remains open.</param>
    /// <returns>The validated annotation document.</returns>
    /// <exception cref="InvalidDataException">The document version, dimensions, role, or image bounds are invalid.</exception>
    public static CalibrationAnchorDocument Load(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        using var json = JsonDocument.Parse(stream);
        var root = json.RootElement;

        if (root.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("The calibration document root must be a JSON object.");

        var formatVersion = ReadInt32(root, "formatVersion");
        if (formatVersion != CurrentFormatVersion)
            throw new InvalidDataException($"Unsupported calibration document version {formatVersion}.");

        var clientBuild = ReadString(root, "clientBuild");
        var mapId = ReadInt64(root, "mapId");
        var imageWidth = ReadInt32(root, "imageWidth");
        var imageHeight = ReadInt32(root, "imageHeight");

        if (imageWidth <= 0 || imageHeight <= 0)
            throw new InvalidDataException("Image dimensions must be positive.");

        if (!root.TryGetProperty("anchors", out var anchorsElement) || anchorsElement.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("The anchors property must be an array.");

        var anchors = ImmutableArray.CreateBuilder<CalibrationAnchor>();
        foreach (var element in anchorsElement.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException("Each annotation must be a JSON object.");

            var cellId = ReadInt32(element, "cellId");
            var pixelX = ReadInt32(element, "pixelX");
            var pixelY = ReadInt32(element, "pixelY");
            var roleText = ReadString(element, "role");
            var role = roleText switch
            {
                "fit" => CalibrationAnchorRole.Fit,
                "validation" => CalibrationAnchorRole.Validation,
                _ => throw new InvalidDataException($"Unknown calibration anchor role '{roleText}'."),
            };

            LogicalCell cell;
            try
            {
                cell = new LogicalCell(cellId);
            }
            catch (ArgumentOutOfRangeException exception)
            {
                throw new InvalidDataException($"Cell identifier {cellId} is outside the map.", exception);
            }

            if (pixelX < 0 || pixelX >= imageWidth || pixelY < 0 || pixelY >= imageHeight)
                throw new InvalidDataException($"Annotation for cell {cellId} lies outside the image bounds.");

            anchors.Add(new CalibrationAnchor(
                cell,
                MapGeometry.PointOf(cell.Value),
                new WindowLocalPixels(pixelX, pixelY),
                role,
                CalibrationAnchorSource.ManualAnnotation));
        }

        var synthetic = false;
        if (root.TryGetProperty("synthetic", out var syntheticElement))
        {
            if (syntheticElement.ValueKind is not JsonValueKind.True and not JsonValueKind.False)
                throw new InvalidDataException("The synthetic property must be a boolean when present.");

            synthetic = syntheticElement.GetBoolean();
        }

        try
        {
            return new CalibrationAnchorDocument(clientBuild, mapId, imageWidth, imageHeight, anchors.ToImmutable(), synthetic);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException("The calibration document contains invalid metadata.", exception);
        }
    }

    /// <summary>Loads and validates a document from a file path.</summary>
    /// <param name="path">The JSON file path.</param>
    /// <returns>The validated annotation document.</returns>
    public static CalibrationAnchorDocument Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        using var stream = File.OpenRead(path);
        return Load(stream);
    }

    /// <summary>Writes the version 1 document as camel-case JSON to a stream.</summary>
    /// <param name="stream">The destination stream; it remains open.</param>
    public void Save(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var wireDocument = new
        {
            formatVersion = CurrentFormatVersion,
            clientBuild = ClientBuild,
            mapId = MapId,
            imageWidth = ImageWidth,
            imageHeight = ImageHeight,
            anchors = Anchors.Select(anchor => new
            {
                cellId = anchor.Cell.Value,
                pixelX = anchor.MeasuredPixelPoint.X,
                pixelY = anchor.MeasuredPixelPoint.Y,
                role = anchor.Role switch
                {
                    CalibrationAnchorRole.Fit => "fit",
                    CalibrationAnchorRole.Validation => "validation",
                    _ => throw new InvalidOperationException("The anchor role is not supported by format version 1."),
                },
            }),
            synthetic = Synthetic,
        };

        JsonSerializer.Serialize(stream, wireDocument, _jsonOptions);
    }

    /// <summary>Writes the document to a file path.</summary>
    /// <param name="path">The destination JSON file path.</param>
    public void Save(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        using var stream = File.Create(path);
        Save(stream);
    }

    /// <summary>Serializes the document to a camel-case JSON string.</summary>
    /// <returns>The JSON document.</returns>
    public string ToJson()
    {
        using var stream = new MemoryStream();
        Save(stream);
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    private static int ReadInt32(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var result))
            throw new InvalidDataException($"The {name} property must be a 32-bit integer.");

        return result;
    }

    private static long ReadInt64(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Number || !value.TryGetInt64(out var result))
            throw new InvalidDataException($"The {name} property must be a 64-bit integer.");

        return result;
    }

    private static string ReadString(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String)
            throw new InvalidDataException($"The {name} property must be a string.");

        var result = value.GetString();
        if (string.IsNullOrWhiteSpace(result))
            throw new InvalidDataException($"The {name} property must not be empty.");

        return result;
    }
}
