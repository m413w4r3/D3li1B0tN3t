using D3B.Bot.Characters;

namespace D3B.App.ViewModels;

/// <summary>The bot the map, the logs page and the protocol inspector look at, shared so that each follows the others (D111, D120).</summary>
internal sealed class BotChoice
{
    /// <summary>Gets or sets the bot looked at last, if any.</summary>
    public CharacterKey? Key { get; set; }
}
