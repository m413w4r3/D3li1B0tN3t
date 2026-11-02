using System.CommandLine;
using D3B.Scripts.Unity;

namespace D3B.Scripts.Inspect;

/// <summary><c>texts</c>: the client's text files, one per language.</summary>
internal static class TextsCommand
{
    public static Command Create(Option<DirectoryInfo?> client)
    {
        var language = new Option<string?>("--language") { Description = "Reads only this language, such as fr." };
        var id = new Option<int?>("--id") { Description = "Writes the text of this identifier." };
        var sample = CommonOptions.AtLeast(
            new Option<int>("--sample") { Description = "Writes the first texts and keys of each file.", DefaultValueFactory = _ => 0 },
            0);
        var command = new Command("texts", "Sums up the text files of Content/I18n and checks that every entry points to the start of a text.")
        {
            language,
            id,
            sample,
        };

        command.SetAction(result => CommonOptions.Run(
            result,
            client,
            located => Run(located, result.GetValue(language), result.GetValue(id), result.GetValue(sample))));

        return command;
    }

    private static int Run(Client client, string? language, int? id, int sample)
    {
        var directory = Path.Combine(client.StreamingAssets, "Content", "I18n");
        var paths = Directory.Exists(directory)
            ? Directory.EnumerateFiles(directory, language is null ? "*.bin" : $"{language}.bin").Order(StringComparer.Ordinal).ToList()
            : [];

        if (paths.Count is 0)
        {
            Console.Error.WriteLine(language is null ? $"No text file in {directory}." : $"No text file {language}.bin in {directory}.");

            return 1;
        }

        var table = new TextTable("File", "Header", "#Texts", "Identifiers", "#Keys", "#Distinct", "#Tables size", "#Texts size", "Check");
        var files = new List<(string Language, I18nFile File, HashSet<int> Starts)>();
        var failures = 0;

        foreach (var path in paths)
        {
            I18nFile file;
            HashSet<int> starts;

            try
            {
                file = I18nFile.Read(path);
                starts = file.TextStarts();
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or FormatException)
            {
                failures++;
                table.Add(Path.GetFileName(path), null, null, null, null, null, null, null, $"unreadable: {exception.Message}");

                continue;
            }

            // An offset counts only when it is where a text starts, not merely somewhere a length could be read.
            var offsets = file.Texts.Select(text => text.Offset).Concat(file.Keys.Select(key => key.Offset)).Distinct().ToList();
            var misplaced = offsets.Count(offset => !starts.Contains(offset));

            failures += misplaced;
            files.Add((Path.GetFileNameWithoutExtension(path), file, starts));
            table.Add(
                Path.GetFileName(path),
                file.HeaderLanguage,
                file.Texts.Count,
                file.Texts.Count is 0 ? string.Empty : $"{file.Texts.Min(text => text.Id):N0} to {file.Texts.Max(text => text.Id):N0}",
                file.Keys.Count,
                offsets.Count,
                Sizes.Format(file.TextsOffset),
                Sizes.Format(file.Length - file.TextsOffset),
                misplaced is 0 ? $"every offset starts one of {starts.Count:N0} texts" : $"{misplaced:N0} offset(s) start no text");
        }

        table.Write(Console.Out);

        foreach (var (fileLanguage, file, starts) in files)
        {
            if (id is { } wanted)
            {
                var entry = file.Texts.Where(text => text.Id == wanted).Select(text => (int?)text.Offset).FirstOrDefault();

                Console.WriteLine(entry is { } offset
                    ? $"{fileLanguage} {wanted}: {TextOrProblem(file, starts, offset)}"
                    : $"{fileLanguage}: no text {wanted}");
            }

            if (sample <= 0)
                continue;

            Console.WriteLine();
            Console.WriteLine($"{fileLanguage}: first {sample} texts, then first {sample} keys");

            foreach (var (textId, offset) in file.Texts.Take(sample))
                Console.WriteLine($"  {textId}: {TextOrProblem(file, starts, offset)}");

            foreach (var (hash, offset) in file.Keys.Take(sample))
                Console.WriteLine($"  {hash:X16}: {TextOrProblem(file, starts, offset)}");
        }

        return failures is 0 ? 0 : 1;
    }

    private static string TextOrProblem(I18nFile file, HashSet<int> starts, int offset)
    {
        return starts.Contains(offset) ? file.TextAt(offset) : $"<offset {offset} starts no text>";
    }
}
