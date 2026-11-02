namespace D3B.Data.Texts;

/// <summary>The languages packs hold texts in, by the code of the client's text files.</summary>
public static class TextLanguages
{
    /// <summary>French, the language the client's texts are written in.</summary>
    public const string French = "fr";

    /// <summary>English.</summary>
    public const string English = "en";

    /// <summary>Gets every language a pack holds.</summary>
    public static IReadOnlyList<string> All { get; } = [French, English];
}
