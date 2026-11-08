namespace D3B.Gui.Vision;

/// <summary>Describes whether a target location met the current visual checks.</summary>
public enum TargetStatus
{
    /// <summary>All configured location and ambiguity checks passed.</summary>
    Verified,

    /// <summary>The image or candidate did not satisfy a required verification check.</summary>
    Unverified,

    /// <summary>The candidate conflicts with a competing peak, known element, or distance bound.</summary>
    Ambiguous
}
