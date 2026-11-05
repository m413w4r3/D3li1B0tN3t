namespace D3B.Protocol.Translation;

/// <summary>Why a game frame was not read whole, at the level of detail a movement-safety policy may act on.</summary>
public enum TranslationProblemKind
{
    /// <summary>The frame was read, or carried an understood envelope with no content.</summary>
    None,

    /// <summary>The bytes are not a game envelope at all.</summary>
    MalformedEnvelope,

    /// <summary>The envelope parses but does not encode back to the frame's length.</summary>
    EnvelopeLengthMismatch,

    /// <summary>The envelope parses, but its carried type URL does not begin with the game's prefix.</summary>
    UnexpectedTypeUrl,

    /// <summary>The envelope is understood but its carried type is absent from the readable protocol.</summary>
    UnknownCarriedType,

    /// <summary>The carried readable type exists, but its payload could not be parsed.</summary>
    UnreadablePayload,

    /// <summary>The carried readable type parsed, but its payload holds fields the readable protocol leaves out.</summary>
    PartialPayload,
}
