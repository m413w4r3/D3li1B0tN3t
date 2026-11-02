namespace D3B.App.ViewModels.Inspector;

/// <summary>A line of a frame unfolded: a field and its value, a field of a message the readable protocol lacks by its shape, or a note (D115).</summary>
internal sealed class FrameFieldViewModel
{
    private const double IndentStep = 18;

    /// <summary>Gets the field's name, or its number for a shape.</summary>
    public string Name { get; }

    /// <summary>Gets its name as shown, followed by a colon when a value follows it.</summary>
    public string Label => Value is null ? Name : Name + ":";

    /// <summary>Gets its value, its wire type and length for a shape, or nothing for a message, whose fields follow.</summary>
    public string? Value { get; }

    /// <summary>Gets whether its value is a secret, hidden.</summary>
    public bool IsHidden { get; }

    /// <summary>Gets whether it is a note on the frame rather than a field.</summary>
    public bool IsNote { get; }

    /// <summary>Gets how far it is set in.</summary>
    public double Indent { get; }

    public FrameFieldViewModel(string name, string? value, bool isHidden, bool isNote, int depth)
    {
        Name = name;
        Value = value;
        IsHidden = isHidden;
        IsNote = isNote;
        Indent = depth * IndentStep;
    }
}
