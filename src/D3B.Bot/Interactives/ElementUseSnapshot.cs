using D3B.Bot.Characters;
using D3B.Data.Maps;
using D3B.Gui.Safety;

namespace D3B.Bot.Interactives;

/// <summary>One coherent bot view used by passive element diagnostics.</summary>
internal sealed record ElementUseSnapshot(
    CharacterState State,
    MapData? Map,
    long Revision,
    long SessionEpoch,
    bool Connected,
    bool SessionAttached,
    bool TranslationBlocked,
    bool Paused,
    bool Acting,
    GameInputMode InputMode);
