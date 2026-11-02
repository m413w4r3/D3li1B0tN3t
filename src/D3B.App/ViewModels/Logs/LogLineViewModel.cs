using D3B.App.Controls;

namespace D3B.App.ViewModels.Logs;

/// <summary>A line of the logs page: a bot's journal entry or a problem of the host, said in the app's language (D112, D114).</summary>
/// <remarks>Made once, as it is read, and never changed: the page makes its lines again when the language changes.</remarks>
internal sealed class LogLineViewModel
{
    private const double IndentStep = 20;

    /// <summary>Gets when it happened, by the bot's clock: an action's end.</summary>
    public DateTimeOffset At { get; }

    /// <summary>Gets when it happened, as the page shows it.</summary>
    public string Time { get; }

    /// <summary>Gets whom it is about, a bot's name or the host, shown when the page shows every source; else <see langword="null"/>.</summary>
    public string? Source { get; }

    /// <summary>Gets what happened.</summary>
    public string Text { get; }

    /// <summary>Gets how it ended and how long it took, beside it, if anything.</summary>
    public string? Detail { get; }

    /// <summary>Gets what more there is to read, such as an exception, in its tooltip.</summary>
    public string? More { get; }

    /// <summary>Gets its tone: success, warning, danger, or neutral.</summary>
    public Tone Tone { get; }

    /// <summary>Gets what it is about, at a glance.</summary>
    public LogMark Mark { get; }

    /// <summary>Gets how far it is set in: a travel's map changes lie 1 deeper than the travel.</summary>
    public double Indent { get; }

    /// <summary>Gets whether it is shown when the page shows warnings and errors.</summary>
    public bool IsProblem => Tone is Tone.Warning or Tone.Danger;

    /// <summary>Gets whether it is shown when the page shows errors alone.</summary>
    public bool IsError => Tone is Tone.Danger;

    public LogLineViewModel(DateTimeOffset at, string time, string? source, string text, string? detail, string? more, Tone tone, LogMark mark, int depth)
    {
        At = at;
        Time = time;
        Source = source;
        Text = text;
        Detail = detail;
        More = more;
        Tone = tone;
        Mark = mark;
        Indent = depth * IndentStep;
    }

    /// <summary>Tells whether the line says a text, whatever its case.</summary>
    /// <param name="search">The text sought, or an empty one, which every line says.</param>
    /// <returns>Whether its text, detail or source holds it.</returns>
    public bool Says(string search)
    {
        return search.Length is 0
            || Text.Contains(search, StringComparison.CurrentCultureIgnoreCase)
            || (Detail?.Contains(search, StringComparison.CurrentCultureIgnoreCase) ?? false)
            || (Source?.Contains(search, StringComparison.CurrentCultureIgnoreCase) ?? false);
    }

    /// <summary>Gets the line as text, as the page copies it.</summary>
    /// <returns>Its time, source, text and detail, separated by tabs.</returns>
    public override string ToString()
    {
        return string.Join('\t', new[] { Time, Source, new string(' ', (int)(Indent / IndentStep) * 2) + Text, Detail }.Where(part => !string.IsNullOrEmpty(part)));
    }
}
