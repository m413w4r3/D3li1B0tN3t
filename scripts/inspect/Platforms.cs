namespace D3B.Scripts.Inspect;

/// <summary>Names the values of Unity's <c>BuildTarget</c> that a serialized file records as its platform.</summary>
internal static class Platforms
{
    public static string Name(uint platform)
    {
        var name = platform switch
        {
            2 => "StandaloneOSX",
            5 => "StandaloneWindows",
            9 => "iOS",
            13 => "Android",
            19 => "StandaloneWindows64",
            20 => "WebGL",
            24 => "StandaloneLinux64",
            _ => null,
        };

        return name is null ? $"{platform}" : $"{platform} ({name})";
    }
}
