using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace D3B.Storage.Runs;

/// <summary>Writes a run's items and professions as JSON in their columns, and compares them by what they hold (D146).</summary>
internal static class RunJson
{
    /// <summary>Gets the comparer of a run's items.</summary>
    public static ValueComparer<Dictionary<int, long>> ItemsComparer { get; } = new(
        (left, right) => Write(left) == Write(right),
        items => Write(items).GetHashCode(StringComparison.Ordinal),
        items => ReadItems(Write(items)));

    /// <summary>Gets the comparer of a run's professions.</summary>
    public static ValueComparer<List<RunJob>> JobsComparer { get; } = new(
        (left, right) => Write(left) == Write(right),
        jobs => Write(jobs).GetHashCode(StringComparison.Ordinal),
        jobs => ReadJobs(Write(jobs)));

    public static string Write(Dictionary<int, long>? items)
    {
        return JsonSerializer.Serialize(items ?? []);
    }

    public static string Write(List<RunJob>? jobs)
    {
        return JsonSerializer.Serialize(jobs ?? []);
    }

    public static Dictionary<int, long> ReadItems(string json)
    {
        return JsonSerializer.Deserialize<Dictionary<int, long>>(json) ?? [];
    }

    public static List<RunJob> ReadJobs(string json)
    {
        return JsonSerializer.Deserialize<List<RunJob>>(json) ?? [];
    }
}
