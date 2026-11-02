using AssetsTools.NET;
using AssetsTools.NET.Extra;

namespace D3B.Scripts.Inspect;

/// <summary>Reads the <c>m_Name</c> of objects without reading the rest of them.</summary>
/// <remarks>
/// A map is a MonoBehaviour of about 450 KB; its name comes fourth, after three fixed fields. The iterator walks the type
/// tree up to the name and stops, where a full read would build tens of thousands of fields.
/// </remarks>
internal static class ObjectNames
{
    private const string NameField = "m_Name";

    /// <summary>The object's name, or <see langword="null"/> when its type has none.</summary>
    public static string? Read(AssetsManager manager, AssetsFileInstance file, AssetFileInfo info)
    {
        var template = manager.GetTemplateBaseField(file, info);

        if (!template.Children.Exists(child => child.Name is NameField))
            return null;

        // The name precedes any managed reference, so no reference type manager is needed to reach it.
        var iterator = new AssetTypeValueIterator(template, file.file.Reader, info.GetAbsoluteByteOffset(file.file), null);

        while (iterator.ReadNext())
        {
            if (iterator.Depth is 1 && iterator.TempField.Name is NameField)
                return iterator.ReadValueField().AsString;
        }

        return null;
    }
}
