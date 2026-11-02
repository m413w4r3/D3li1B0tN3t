using System.CommandLine;

namespace D3B.Scripts.Inspect;

/// <summary>The arguments and options several commands share, and how a command runs against the client.</summary>
internal static class CommonOptions
{
    public static Argument<string?> Pattern(bool required)
    {
        return new Argument<string?>("pattern")
        {
            Description = "The bundles to read: a path under StreamingAssets where * matches anything, or a part of one.",
            Arity = required ? ArgumentArity.ExactlyOne : ArgumentArity.ZeroOrOne,
        };
    }

    public static Option<int?> Sample()
    {
        return new Option<int?>("--sample")
        {
            Description = "Reads at most this many bundles per area, spread evenly over the area.",
        };
    }

    /// <summary>Rejects values of <paramref name="option"/> below <paramref name="minimum"/>.</summary>
    public static Option<int> AtLeast(Option<int> option, int minimum)
    {
        option.Validators.Add(result =>
        {
            if (result.GetValueOrDefault<int>() < minimum)
                result.AddError($"{option.Name} is at least {minimum}.");
        });

        return option;
    }

    /// <summary>The bundles a pattern matches, at most <paramref name="sample"/> of them per area.</summary>
    public static IReadOnlyList<BundlePath> Select(Client client, string? pattern, int? sample)
    {
        var bundles = client.Bundles(pattern);

        if (sample is not { } count || count <= 0)
            return bundles;

        return
        [
            .. bundles.GroupBy(bundle => bundle.Area)
                .SelectMany(area =>
                {
                    var members = area.ToList();

                    return members.Count <= count
                        ? members
                        : Enumerable.Range(0, count).Select(i => members[(int)((long)i * members.Count / count)]);
                }),
        ];
    }

    /// <summary>Locates the client, then runs a command on it.</summary>
    /// <returns>The command's exit code, or 2 when the client cannot be found.</returns>
    public static int Run(ParseResult result, Option<DirectoryInfo?> client, Func<Client, int> command)
    {
        Client located;

        try
        {
            located = Client.Locate(result.GetValue(client));
        }
        catch (DirectoryNotFoundException exception)
        {
            Console.Error.WriteLine(exception.Message);

            return 2;
        }

        return command(located);
    }
}
