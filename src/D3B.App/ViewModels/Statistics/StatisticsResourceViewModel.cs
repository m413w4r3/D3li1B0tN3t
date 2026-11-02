using Avalonia.Media;

namespace D3B.App.ViewModels.Statistics;

/// <summary>A resource the runs counted brought, with its icon, how many and how many an hour (D146).</summary>
/// <param name="Icon">Its icon, if the pack has one.</param>
/// <param name="Name">Its name, in the app's language.</param>
/// <param name="Count">How many, said.</param>
/// <param name="Rate">How many an hour, said, once the runs ran long enough.</param>
/// <param name="Share">How many, as a share of the resource brought the most, from 0 to 100.</param>
internal sealed record StatisticsResourceViewModel(IImage? Icon, string Name, string Count, string? Rate, double Share)
{
    /// <summary>Gets whether the resource has its icon, else a neutral square stands in its place.</summary>
    public bool HasIcon => Icon is not null;
}
