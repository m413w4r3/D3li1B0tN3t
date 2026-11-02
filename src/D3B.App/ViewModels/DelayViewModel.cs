using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using D3B.App.Localization;

namespace D3B.App.ViewModels;

/// <summary>A delay of D3B's on the settings page, in seconds, from its default, the least, to its most (D151).</summary>
/// <remarks>
/// Its default is the value D3B measured, or chose for the relay: the box never goes below it, since a shorter delay
/// would have the bot act before the client or the server is done. A fixed value, never drawn at random (D152).
/// </remarks>
internal sealed partial class DelayViewModel : ObservableObject
{
    private readonly Localizer _localizer;
    private readonly string _key;
    private readonly string _defaultKey;
    private readonly Func<TimeSpan> _read;
    private readonly Action<TimeSpan> _write;
    private bool _quiet;

    /// <summary>Gets the default, the least the delay may be.</summary>
    public TimeSpan Default { get; }

    /// <summary>Gets the most the delay may be raised to.</summary>
    public TimeSpan Most { get; }

    /// <summary>Gets the least, in seconds, for the box.</summary>
    public decimal Minimum => (decimal)Default.TotalSeconds;

    /// <summary>Gets the most, in seconds, for the box.</summary>
    public decimal Maximum => (decimal)Most.TotalSeconds;

    /// <summary>Gets how far a step of the box or the bar goes, in seconds: a tenth under a second, a half under ten, else five.</summary>
    public decimal Increment { get; }

    /// <summary>Gets the least, in seconds, for the bar.</summary>
    public double SliderMinimum => Default.TotalSeconds;

    /// <summary>Gets the most, in seconds, for the bar.</summary>
    public double SliderMaximum => Most.TotalSeconds;

    /// <summary>Gets a step of the bar, in seconds.</summary>
    public double SliderStep => (double)Increment;

    /// <summary>Gets or sets the delay in seconds, as the box shows it; emptied, it keeps the delay it had.</summary>
    [ObservableProperty]
    public partial decimal? Value { get; set; }

    /// <summary>Gets what the delay is.</summary>
    [ObservableProperty]
    public partial string Label { get; private set; } = "";

    /// <summary>Gets what it waits for, and when to raise it.</summary>
    [ObservableProperty]
    public partial string Help { get; private set; } = "";

    /// <summary>Gets the default said, measured or D3B's.</summary>
    [ObservableProperty]
    public partial string DefaultText { get; private set; } = "";

    /// <summary>Gets the most it may be raised to, said.</summary>
    [ObservableProperty]
    public partial string MostText { get; private set; } = "";

    /// <summary>Gets the delay said, in seconds.</summary>
    [ObservableProperty]
    public partial string ValueText { get; private set; } = "";

    /// <summary>Gets how the box writes seconds, in the app's language rather than the system's.</summary>
    [ObservableProperty]
    public partial NumberFormatInfo NumberFormat { get; private set; } = NumberFormatInfo.InvariantInfo;

    /// <summary>Gets whether the delay was raised above its default.</summary>
    [ObservableProperty]
    public partial bool IsRaised { get; private set; }

    /// <summary>Gets or sets the delay in seconds, as the bar moves it.</summary>
    public double Slider
    {
        get => (double)(Value ?? Minimum);
        set => Value = Math.Round((decimal)value / Increment) * Increment;
    }

    /// <summary>Initializes a new instance of the <see cref="DelayViewModel"/> class.</summary>
    /// <param name="localizer">The app's texts.</param>
    /// <param name="key">The key of its texts: its label, and its help with <c>Help</c> after it.</param>
    /// <param name="defaultKey">The key of the text saying its default, measured or D3B's.</param>
    /// <param name="range">Its default, the least, and its most.</param>
    /// <param name="read">Reads the delay as it is now.</param>
    /// <param name="write">Changes the delay.</param>
    public DelayViewModel(Localizer localizer, string key, string defaultKey, (TimeSpan Default, TimeSpan Most) range, Func<TimeSpan> read, Action<TimeSpan> write)
    {
        _localizer = localizer;
        _key = key;
        _defaultKey = defaultKey;
        _read = read;
        _write = write;
        Default = range.Default;
        Most = range.Most;
        Increment = Default < TimeSpan.FromSeconds(1) ? 0.1m : Default < TimeSpan.FromSeconds(10) ? 0.5m : 5m;
        Reread();
        Relabel();
    }

    /// <summary>Says the delay again, in the app's language.</summary>
    public void Relabel()
    {
        Label = _localizer[_key];
        Help = _localizer[_key + "Help"];
        DefaultText = _localizer.Format(_defaultKey, Seconds(Default));
        MostText = _localizer.Format("DelayMost", Seconds(Most));
        NumberFormat = _localizer.Culture.NumberFormat;
        ValueText = Value is { } seconds ? Seconds(TimeSpan.FromSeconds((double)seconds)) : "";
    }

    /// <summary>Shows the delay as it is now, changing nothing.</summary>
    public void Reread()
    {
        _quiet = true;

        try
        {
            Value = (decimal)_read().TotalSeconds;
        }
        finally
        {
            _quiet = false;
        }
    }

    private string Seconds(TimeSpan delay)
    {
        return _localizer.Format("DelaySeconds", delay.TotalSeconds);
    }

    partial void OnValueChanged(decimal? value)
    {
        OnPropertyChanged(nameof(Slider));

        if (value is not { } seconds)
            return;

        var delay = TimeSpan.FromSeconds((double)Math.Clamp(seconds, Minimum, Maximum));

        IsRaised = delay > Default;
        ValueText = Seconds(delay);

        if (!_quiet)
            _write(delay);
    }
}
