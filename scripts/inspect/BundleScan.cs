using System.Diagnostics;

namespace D3B.Scripts.Inspect;

/// <summary>Reads many bundles in parallel, keeps their order, and reports the ones that fail without stopping.</summary>
internal static class BundleScan
{
    private static readonly TimeSpan _progressInterval = TimeSpan.FromSeconds(2);

    /// <summary>Runs <paramref name="read"/> on every bundle and returns the results of those that succeeded, in order.</summary>
    /// <param name="bundles">The bundles to read.</param>
    /// <param name="read">Reads one bundle. It runs on worker threads, one bundle per call.</param>
    /// <param name="failures">How many bundles failed; each failure is written to the standard error.</param>
    public static List<(BundlePath Bundle, T Result)> Run<T>(IReadOnlyList<BundlePath> bundles, Func<BundlePath, T> read, out int failures)
    {
        var results = new (bool Done, T Result)[bundles.Count];
        var errors = 0;
        var done = 0;
        var clock = Stopwatch.StartNew();
        var lastReport = TimeSpan.Zero;
        var progress = new Lock();

        Parallel.For(0, bundles.Count, i =>
        {
            try
            {
                results[i] = (true, read(bundles[i]));
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                // AssetsTools.NET reports unreadable data through many exception types, its LZMA decoder's own among
                // them, so any failure is the bundle's: one bad bundle must not hide the rest.
                Interlocked.Increment(ref errors);
                Console.Error.WriteLine($"{bundles[i].RelativePath}: {exception.GetType().Name}: {exception.Message}");
            }

            var count = Interlocked.Increment(ref done);

            lock (progress)
            {
                if (clock.Elapsed - lastReport < _progressInterval)
                    return;

                lastReport = clock.Elapsed;
                Console.Error.WriteLine($"{count}/{bundles.Count} bundles read in {clock.Elapsed.TotalSeconds:0} s");
            }
        });

        failures = errors;

        var succeeded = new List<(BundlePath Bundle, T Result)>(bundles.Count - errors);

        for (var i = 0; i < bundles.Count; i++)
        {
            if (results[i].Done)
                succeeded.Add((bundles[i], results[i].Result));
        }

        return succeeded;
    }
}
