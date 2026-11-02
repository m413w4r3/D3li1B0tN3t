using D3B.App.Localization;

namespace D3B.App.ViewModels;

/// <summary>Says how long ago something happened, as a person would: just now, minutes, hours, yesterday, days, then the date.</summary>
internal static class Ago
{
    /// <summary>Says how long ago something happened.</summary>
    /// <param name="localizer">The app's texts.</param>
    /// <param name="prefix">The texts' prefix, such as <c>Seen</c> for <c>SeenNow</c>, <c>SeenMinutes</c> and the others.</param>
    /// <param name="when">When it happened.</param>
    /// <param name="now">The time now.</param>
    /// <returns>The text.</returns>
    public static string Of(Localizer localizer, string prefix, DateTimeOffset when, DateTimeOffset now)
    {
        var elapsed = now - when;

        if (elapsed < TimeSpan.FromMinutes(1))
            return localizer[prefix + "Now"];

        if (elapsed < TimeSpan.FromHours(1))
            return localizer.Format(prefix + "Minutes", (int)elapsed.TotalMinutes);

        if (elapsed < TimeSpan.FromDays(1))
            return localizer.Format(prefix + "Hours", (int)elapsed.TotalHours);

        if (elapsed < TimeSpan.FromDays(2))
            return localizer[prefix + "Yesterday"];

        if (elapsed < TimeSpan.FromDays(7))
            return localizer.Format(prefix + "Days", (int)elapsed.TotalDays);

        return localizer.Format(prefix + "On", when.ToLocalTime().DateTime);
    }
}
