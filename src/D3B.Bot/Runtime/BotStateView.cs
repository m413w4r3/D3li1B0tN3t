using D3B.Bot.Characters;

namespace D3B.Bot.Runtime;

/// <summary>An atomically published state, revision, session identity, connection, pause, and translation flags.</summary>
internal sealed record BotStateView(
    CharacterState State,
    long Revision,
    long SessionEpoch,
    bool IsConnected,
    bool IsPaused,
    bool IsTranslationBlocked);
