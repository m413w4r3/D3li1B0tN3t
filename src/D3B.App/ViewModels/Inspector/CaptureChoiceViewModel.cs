namespace D3B.App.ViewModels.Inspector;

/// <summary>A capture of a game connection the inspector may replay: its file, and when it began (D63, D115).</summary>
internal sealed class CaptureChoiceViewModel
{
    /// <summary>Gets the capture's file.</summary>
    public string Path { get; }

    /// <summary>Gets when it was last written, as the list sorts it.</summary>
    public DateTime Written { get; }

    /// <summary>Gets what the list shows: when it was written and its size.</summary>
    public string Label { get; }

    public CaptureChoiceViewModel(string path, DateTime written, string label)
    {
        Path = path;
        Written = written;
        Label = label;
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return Label;
    }
}
