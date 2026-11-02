namespace D3B.App.ViewModels.Statistics;

/// <summary>Which runs the statistics page counts, by when they began (D146).</summary>
internal enum StatisticsPeriod
{
    /// <summary>Those begun today, by the machine's clock.</summary>
    Today,

    /// <summary>Those begun in the last 7 days.</summary>
    Week,

    /// <summary>Every run kept.</summary>
    All,
}
