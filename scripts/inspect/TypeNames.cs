using AssetsTools.NET;
using AssetsTools.NET.Extra;

namespace D3B.Scripts.Inspect;

/// <summary>Names Unity types: the class of an object, or its script for a MonoBehaviour.</summary>
internal static class TypeNames
{
    public static string OfClass(int typeId)
    {
        var id = (AssetClassID)typeId;

        return Enum.IsDefined(id) ? id.ToString() : $"Class{typeId}";
    }

    /// <summary>The class of an object, with its script for a MonoBehaviour: <c>MonoBehaviour Namespace.Class</c>.</summary>
    public static string Of(OpenedBundle opened, AssetsFileInstance file, AssetFileInfo info, ScriptResolver scripts)
    {
        var typeId = info.GetTypeId(file.file);

        return typeId == (int)AssetClassID.MonoBehaviour
            ? $"MonoBehaviour {scripts.Resolve(opened, file, info)}"
            : OfClass(typeId);
    }
}
