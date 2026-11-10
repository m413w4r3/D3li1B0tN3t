namespace D3B.Bot.Interactives;

/// <summary>How strongly the available protocol and pack evidence identifies a resource.</summary>
internal enum ElementCandidateIdentity
{
    /// <summary>No reliable item identity is known.</summary>
    Unknown,

    /// <summary>A pack graphic hint suggests one item without server confirmation.</summary>
    Inferred,

    /// <summary>Server skill evidence and the catalog identify one item.</summary>
    Verified,

    /// <summary>Evidence conflicts or names more than one possible item.</summary>
    Ambiguous,
}
