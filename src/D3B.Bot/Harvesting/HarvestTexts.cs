using System.Resources;
using D3B.Bot.Localization;

namespace D3B.Bot.Harvesting;

/// <summary>The harvesting module's texts, in English and French (<c>HarvestTexts.resx</c>, <c>HarvestTexts.fr.resx</c>), which the app shows in its language (D96).</summary>
internal static class HarvestTexts
{
    /// <summary>Gets the module's resources.</summary>
    public static ResourceManager Resources { get; } = new("D3B.Bot.Harvesting.HarvestTexts", typeof(HarvestTexts).Assembly);

    /// <summary>Gets one of the module's texts.</summary>
    /// <param name="key">The text's key in the resources.</param>
    /// <returns>The text, in the language the app asks for.</returns>
    public static LocalizedText Text(string key)
    {
        return LocalizedText.From(Resources, key);
    }
}
