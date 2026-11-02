using System.Collections.Immutable;
using D3B.Protocol.Game;

namespace D3B.Bot.Characters;

/// <summary>The dialog of a zaap the character used, from the server's listing its destinations to the dialog's end (D83).</summary>
/// <param name="Type">The zaap used: a map's, or the haven bag's.</param>
/// <param name="Costs">What travelling to each zaap the character knows costs, in kamas, by map: nothing for the one it stands at.</param>
public sealed record ZaapDialog(TeleporterType Type, ImmutableSortedDictionary<long, int> Costs);
