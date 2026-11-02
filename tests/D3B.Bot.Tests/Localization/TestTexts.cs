using System.Resources;

namespace D3B.Bot.Tests.Localization;

/// <summary>Texts in English and French, as a module's own resources hold them.</summary>
internal static class TestTexts
{
    public static ResourceManager Resources { get; } = new("D3B.Bot.Tests.Localization.TestTexts", typeof(TestTexts).Assembly);
}
