using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using D3B.App.Controls;
using D3B.App.Localization;
using D3B.Bot.Inspection;

namespace D3B.App.ViewModels.Inspector;

/// <summary>A frame in the protocol inspector's list: when, who sent it, what it carries, and its fields once unfolded in place (D115).</summary>
/// <remarks>Its fields are read the first time it unfolds: most frames are never looked at.</remarks>
internal sealed partial class FrameRowViewModel : ObservableObject
{
    private readonly Localizer _localizer;
    private readonly Action? _unfolded;
    private IReadOnlyList<FrameFieldViewModel>? _fields;

    /// <summary>Gets the frame.</summary>
    public InspectedFrame Frame { get; }

    /// <summary>Gets when it went, to the millisecond.</summary>
    public string Time { get; }

    /// <summary>Gets who sent it, said.</summary>
    public string Sender { get; }

    /// <summary>Gets the tone of who sent it: the client neutral, the server informative, D3B in success's.</summary>
    public Tone SenderTone { get; }

    /// <summary>Gets the message it carries: its readable name, the build's obfuscated one after a dot, or what it is not.</summary>
    public string Name { get; }

    /// <summary>Gets its length.</summary>
    public string Size { get; }

    /// <summary>Gets how far the readable protocol read it: understood whole, read, or not read.</summary>
    public string Reading { get; }

    /// <summary>Gets the tone of how far it was read.</summary>
    public Tone ReadingTone { get; }

    /// <summary>Gets whether the readable protocol read it.</summary>
    public bool IsRead => Frame.Frame.IsRead;

    /// <summary>Gets or sets whether it is unfolded, showing its fields.</summary>
    [ObservableProperty]
    public partial bool IsExpanded { get; set; }

    /// <summary>Gets its fields, once unfolded: a readable message's, the ticket hidden, or the shape of one the readable protocol lacks.</summary>
    public IReadOnlyList<FrameFieldViewModel> Fields => _fields ??= FieldsOf();

    /// <summary>Initializes a new instance of the <see cref="FrameRowViewModel"/> class.</summary>
    /// <param name="frame">The frame.</param>
    /// <param name="localizer">The app's texts, in the language the row is made in.</param>
    /// <param name="unfolded">Told when the row unfolds, so that the list stops following the frames that come.</param>
    public FrameRowViewModel(InspectedFrame frame, Localizer localizer, Action? unfolded = null)
    {
        Frame = frame;
        _localizer = localizer;
        _unfolded = unfolded;

        var decoded = frame.Frame;

        Time = frame.At.ToLocalTime().ToString("HH:mm:ss.fff", localizer.Culture);
        (Sender, SenderTone) = frame.Source switch
        {
            FrameSource.Client => (localizer["FrameClient"], Tone.Neutral),
            FrameSource.Server => (localizer["FrameServer"], Tone.Info),
            _ => (localizer["FrameD3B"], Tone.Success),
        };
        Name = decoded.ReadableType?.Name
            ?? (decoded.CarriedType is { } carried ? "." + carried : decoded.Envelope is null ? localizer["FrameNoMessage"] : decoded.Envelope.ContentCase.ToString());
        Size = localizer.Format("Bytes", decoded.Length);
        (Reading, ReadingTone) = decoded.IsUnderstood && decoded.IsRead ? (localizer["FrameUnderstood"], Tone.Success)
            : decoded.IsRead ? (localizer["FrameRead"], Tone.Info)
            : (localizer["FrameNotRead"], Tone.Neutral);
    }

    /// <summary>Tells whether the frame carries a message whose name holds a text, whatever its case.</summary>
    /// <param name="search">The text sought, or an empty one, which every frame holds.</param>
    /// <returns>Whether its readable or obfuscated name holds it.</returns>
    public bool Names(string search)
    {
        return search.Length is 0 || Name.Contains(search, StringComparison.OrdinalIgnoreCase)
            || (Frame.Frame.CarriedType?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    [RelayCommand]
    private void Toggle()
    {
        if (!IsExpanded)
        {
            OnPropertyChanged(nameof(Fields));
            _unfolded?.Invoke();
        }

        IsExpanded = !IsExpanded;
    }

    private List<FrameFieldViewModel> FieldsOf()
    {
        var decoded = Frame.Frame;
        var fields = new List<FrameFieldViewModel>();

        if (decoded.Content is { } content)
        {
            foreach (var field in FrameFields.Of(content))
                fields.Add(new FrameFieldViewModel(field.Name, field.IsHidden ? _localizer["FrameHidden"] : field.Value, field.IsHidden, false, field.Depth));

            if (fields.Count is 0)
                fields.Add(new FrameFieldViewModel(_localizer["FrameNoField"], null, false, true, 0));
        }
        else if (FrameFields.TryShape(decoded, out var shape) || shape.Count > 0)
        {
            fields.Add(new FrameFieldViewModel(_localizer["FrameShape"], null, false, true, 0));

            foreach (var field in shape)
                fields.Add(new FrameFieldViewModel(field.Number.ToString(CultureInfo.InvariantCulture), _localizer.Format("FrameWire", WireType(field.WireType), _localizer.Format("Bytes", field.Length)), false, false, 0));
        }

        if (decoded.Problem is { } problem)
            fields.Add(new FrameFieldViewModel(_localizer["FrameWhy"], problem, false, true, 0));

        return fields;
    }

    private string WireType(int wireType)
    {
        return wireType switch
        {
            0 => "varint",
            1 => "64 bits",
            2 => _localizer["WireLength"],
            5 => "32 bits",
            _ => wireType.ToString(CultureInfo.InvariantCulture),
        };
    }
}
