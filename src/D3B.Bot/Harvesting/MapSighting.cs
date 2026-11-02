using System.Collections.Frozen;
using System.Collections.Immutable;

namespace D3B.Bot.Harvesting;

/// <summary>What a bot saw of a map: the resources its elements gather, harvested or not, each element as it was, and when.</summary>
/// <param name="Resources">The resources, by item.</param>
/// <param name="At">When, by the bot's clock.</param>
/// <param name="Elements">Its elements that gather a resource, as they were; none when the bot told only the resources.</param>
public sealed record MapSighting(FrozenSet<int> Resources, DateTimeOffset At, ImmutableArray<SightedElement> Elements = default);
