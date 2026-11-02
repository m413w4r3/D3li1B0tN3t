using System.Resources;
using D3B.Bot.Localization;

namespace D3B.Bot.Fighting;

/// <summary>The fighting module's texts, in English and French (<c>FightTexts.resx</c>, <c>FightTexts.fr.resx</c>), which the app shows in its language (D96).</summary>
internal static class FightTexts
{
    /// <summary>Gets the module's resources.</summary>
    public static ResourceManager Resources { get; } = new("D3B.Bot.Fighting.FightTexts", typeof(FightTexts).Assembly);

    /// <summary>Gets one of the module's texts.</summary>
    /// <param name="key">The text's key in the resources.</param>
    /// <returns>The text, in the language the app asks for.</returns>
    public static LocalizedText Text(string key)
    {
        return LocalizedText.From(Resources, key);
    }
}
