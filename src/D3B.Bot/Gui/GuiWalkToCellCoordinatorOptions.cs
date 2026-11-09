namespace D3B.Bot.Gui;

/// <summary>Bounds one offline WalkToCell GUI attempt.</summary>
public sealed class GuiWalkToCellCoordinatorOptions
{
    private static readonly TimeSpan _defaultMaxSnapshotAge = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan _defaultIntentLifetime = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan _defaultConfirmationTimeout = TimeSpan.FromSeconds(5);

    /// <summary>Gets the maximum age accepted for either protocol snapshot.</summary>
    public TimeSpan MaxSnapshotAge { get; }

    /// <summary>Gets how long the one registered action remains valid.</summary>
    public TimeSpan IntentLifetime { get; }

    /// <summary>Gets the maximum time to await server confirmation.</summary>
    public TimeSpan ConfirmationTimeout { get; }

    internal bool AllowSyntheticCalibration { get; }

    /// <summary>Creates production options with synthetic calibration disabled.</summary>
    public GuiWalkToCellCoordinatorOptions(
        TimeSpan? maxSnapshotAge = null,
        TimeSpan? intentLifetime = null,
        TimeSpan? confirmationTimeout = null)
        : this(
            maxSnapshotAge ?? _defaultMaxSnapshotAge,
            intentLifetime ?? _defaultIntentLifetime,
            confirmationTimeout ?? _defaultConfirmationTimeout,
            false)
    {
    }

    /// <summary>Creates options with synthetic calibration enabled for the friend test assembly only.</summary>
    internal GuiWalkToCellCoordinatorOptions(
        TimeSpan maxSnapshotAge,
        TimeSpan intentLifetime,
        TimeSpan confirmationTimeout,
        bool allowSyntheticCalibration)
    {
        if (maxSnapshotAge < TimeSpan.Zero || maxSnapshotAge == Timeout.InfiniteTimeSpan)
            throw new ArgumentOutOfRangeException(nameof(maxSnapshotAge));

        if (intentLifetime <= TimeSpan.Zero || intentLifetime > TimeSpan.FromSeconds(5))
            throw new ArgumentOutOfRangeException(nameof(intentLifetime), "An intent lifetime must be positive and at most five seconds.");

        if (confirmationTimeout <= TimeSpan.Zero || confirmationTimeout > TimeSpan.FromSeconds(30))
            throw new ArgumentOutOfRangeException(nameof(confirmationTimeout), "A confirmation timeout must be positive and at most thirty seconds.");

        MaxSnapshotAge = maxSnapshotAge;
        IntentLifetime = intentLifetime;
        ConfirmationTimeout = confirmationTimeout;
        AllowSyntheticCalibration = allowSyntheticCalibration;
    }
}
