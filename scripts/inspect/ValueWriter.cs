using System.Text;
using System.Text.Unicode;
using AssetsTools.NET;

namespace D3B.Scripts.Inspect;

/// <summary>Writes the fields of an object as an indented tree, with arrays and long values cut short.</summary>
internal sealed class ValueWriter
{
    private const int MaxInlineFields = 6;
    private const int MaxStringLength = 160;
    private const int MaxBytes = 16;

    private readonly TextWriter _writer;
    private readonly int _maxDepth;
    private readonly int _maxItems;

    /// <param name="writer">Where the tree goes.</param>
    /// <param name="maxDepth">How deep to go below the object; deeper fields are counted, not written.</param>
    /// <param name="maxItems">How many items of an array to write; the others are counted.</param>
    public ValueWriter(TextWriter writer, int maxDepth, int maxItems)
    {
        _writer = writer;
        _maxDepth = maxDepth;
        _maxItems = maxItems;
    }

    private static bool IsLeaf(AssetTypeValueField field)
    {
        return field.Value is { ValueType: not AssetValueType.None and not AssetValueType.Array };
    }

    private static string Describe(AssetTypeValue value)
    {
        return value.ValueType switch
        {
            AssetValueType.Bool => value.AsBool ? "true" : "false",
            AssetValueType.String => DescribeString(value.AsByteArray),
            AssetValueType.Float => $"{value.AsFloat:R}",
            AssetValueType.Double => $"{value.AsDouble:R}",
            AssetValueType.ByteArray => DescribeBytes(value.AsByteArray),
            _ => $"{value.AsObject}",
        };
    }

    private static string DescribeString(byte[] bytes)
    {
        // TextAsset scripts and some other strings hold binary data, which reads better as bytes than as text.
        var binary = !Utf8.IsValid(bytes) || Array.Exists(bytes, b => b < 0x20 && b is not (byte)'\t' and not (byte)'\n' and not (byte)'\r');

        return binary ? DescribeBytes(bytes) : Quote(Encoding.UTF8.GetString(bytes));
    }

    private static string Quote(string text)
    {
        var shown = text.Length > MaxStringLength ? $"{text[..MaxStringLength]}…" : text;
        var escaped = shown.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal);

        return text.Length > MaxStringLength ? $"\"{escaped}\" ({text.Length} chars)" : $"\"{escaped}\"";
    }

    private static string DescribeBytes(byte[] bytes)
    {
        if (bytes.Length is 0)
            return "<0 bytes>";

        var shown = Convert.ToHexString(bytes, 0, Math.Min(bytes.Length, MaxBytes));
        var spaced = new StringBuilder(shown.Length * 3 / 2);

        for (var i = 0; i < shown.Length; i += 2)
        {
            if (i > 0)
                spaced.Append(' ');

            spaced.Append(shown, i, 2);
        }

        return bytes.Length > MaxBytes ? $"<{bytes.Length:N0} bytes: {spaced} …>" : $"<{bytes.Length} bytes: {spaced}>";
    }

    private static AssetTypeValueField? ArrayOf(AssetTypeValueField field)
    {
        if (field.TemplateField.IsArray)
            return field;

        // A vector, a map or a set holds its items in a single child named Array.
        return field.Children is [{ TemplateField.IsArray: true } array] ? array : null;
    }

    private static bool TryInline(AssetTypeValueField field, out string inline)
    {
        inline = string.Empty;

        if (field.Children.Count is 0 or > MaxInlineFields || !field.Children.TrueForAll(IsLeaf))
            return false;

        inline = $"{{{string.Join(", ", field.Children.Select(child => $"{child.FieldName}: {Describe(child.Value)}"))}}}";

        return true;
    }

    public void Write(AssetTypeValueField field)
    {
        Write(field, field.FieldName, 0);
    }

    private void Write(AssetTypeValueField field, string label, int depth)
    {
        var indent = new string(' ', depth * 2);
        var heading = $"{indent}{label} ({field.TypeName})";

        if (ArrayOf(field) is { } array)
        {
            WriteArray(array, heading, depth);

            return;
        }

        if (field.Value?.ValueType is AssetValueType.ManagedReferencesRegistry)
        {
            WriteRegistry(field.Value.AsManagedReferencesRegistry, heading, depth);

            return;
        }

        if (field.Value is { ValueType: not AssetValueType.None and not AssetValueType.Array } leaf)
        {
            _writer.WriteLine($"{heading} = {Describe(leaf)}");

            return;
        }

        if (TryInline(field, out var inline))
        {
            _writer.WriteLine($"{heading} = {inline}");

            return;
        }

        if (depth >= _maxDepth)
        {
            _writer.WriteLine($"{heading} … {field.Children.Count} fields");

            return;
        }

        _writer.WriteLine(heading);

        foreach (var child in field.Children)
            Write(child, child.FieldName, depth + 1);
    }

    private void WriteArray(AssetTypeValueField array, string heading, int depth)
    {
        if (array.Value?.ValueType is AssetValueType.ByteArray)
        {
            _writer.WriteLine($"{heading} = {DescribeBytes(array.Value.AsByteArray)}");

            return;
        }

        var items = array.Children;

        if (items.Count is 0)
        {
            _writer.WriteLine($"{heading} = []");

            return;
        }

        if (items.TrueForAll(IsLeaf))
        {
            var values = string.Join(", ", items.Take(_maxItems).Select(item => Describe(item.Value)));
            var rest = items.Count > _maxItems ? $", … {items.Count - _maxItems:N0} more" : string.Empty;

            _writer.WriteLine($"{heading} = [{values}{rest}] ({items.Count:N0} items)");

            return;
        }

        _writer.WriteLine($"{heading}, {items.Count:N0} items");

        if (depth >= _maxDepth)
            return;

        for (var i = 0; i < Math.Min(items.Count, _maxItems); i++)
            Write(items[i], $"[{i}]", depth + 1);

        if (items.Count > _maxItems)
            _writer.WriteLine($"{new string(' ', (depth + 1) * 2)}… {items.Count - _maxItems:N0} more");
    }

    private void WriteRegistry(ManagedReferencesRegistry registry, string heading, int depth)
    {
        var references = registry.references;

        _writer.WriteLine($"{heading}, version {registry.version}, {references.Count:N0} references");

        if (depth >= _maxDepth)
            return;

        var indent = new string(' ', (depth + 1) * 2);

        for (var i = 0; i < Math.Min(references.Count, _maxItems); i++)
        {
            var reference = references[i];
            var type = reference.type;
            var name = type.Namespace.Length is 0 ? type.ClassName : $"{type.Namespace}.{type.ClassName}";

            _writer.WriteLine($"{indent}[rid {reference.rid}] {name}");

            if (reference.data is null || depth + 1 >= _maxDepth)
                continue;

            foreach (var child in reference.data.Children)
                Write(child, child.FieldName, depth + 2);
        }

        if (references.Count > _maxItems)
            _writer.WriteLine($"{indent}… {references.Count - _maxItems:N0} more");
    }
}
