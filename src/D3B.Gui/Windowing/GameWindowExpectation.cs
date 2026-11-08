namespace D3B.Gui.Windowing;

/// <summary>Names the executable that an explicitly selected X11 window is expected to belong to.</summary>
/// <remarks>The window title is never used to match this expectation.</remarks>
public sealed record GameWindowExpectation
{
    /// <summary>The executable basename expected from <c>/proc/&lt;pid&gt;/exe</c>.</summary>
    public string ExpectedExecutableName { get; }

    /// <summary>Whether an unverified process identity should also mark the observation as failed.</summary>
    public bool RequireVerifiedProcess { get; }

    /// <summary>Creates an expectation for one executable basename.</summary>
    /// <param name="expectedExecutableName">The exact executable basename allowed for this observation.</param>
    /// <param name="requireVerifiedProcess">Whether an unverified process identity should also mark the observation as failed.</param>
    public GameWindowExpectation(string expectedExecutableName, bool requireVerifiedProcess = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedExecutableName);

        if (expectedExecutableName.Contains('/') || expectedExecutableName.Contains('\\'))
        {
            throw new ArgumentException("An executable basename is required.", nameof(expectedExecutableName));
        }

        ExpectedExecutableName = expectedExecutableName;
        RequireVerifiedProcess = requireVerifiedProcess;
    }
}
