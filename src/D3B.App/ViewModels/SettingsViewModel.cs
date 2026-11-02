using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using D3B.App.Controls;
using D3B.App.Hosting;
using D3B.App.Localization;
using D3B.Bot.Data;
using D3B.Bot.Runtime;
using D3B.Network.Relay;
using D3B.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace D3B.App.ViewModels;

/// <summary>The settings page: the client's display time, D3B's delays, the captures, the language and theme, and where D3B keeps its data (D151).</summary>
/// <remarks>
/// <para>
/// The settings are the machine's, not a profile's, kept in the app's own file (<see cref="AppPreferences"/>) as the
/// host's configuration names them, which the app gives the host as it starts. The bots' delays apply at once, to every
/// bot's next wait; the relay's and the captures from the host's next start, the relay being built once.
/// </para>
/// <para>
/// The page shows the delays the bots wait once the host runs, which the host's command line may have raised; changed
/// before it ran, the page's are given to the bots instead.
/// </para>
/// </remarks>
internal sealed partial class SettingsViewModel : ObservableObject
{
    private readonly Localizer _localizer;
    private readonly AppPreferences? _preferences;
    private readonly Action<CultureInfo> _useLanguage;
    private readonly Toasts _toasts;
    private BotDelays _delays;
    private RelayTimeouts _timeouts;
    private Bots? _bots;
    private bool _changed;
    private bool _remembers;
    private CultureInfo? _labelled;

    /// <summary>Gets the client's display time, after a map and after a window.</summary>
    public DelayViewModel Display { get; }

    /// <summary>Gets the bots' other delays, which apply at once.</summary>
    public IReadOnlyList<DelayViewModel> BotWaits { get; }

    /// <summary>Gets the relay's timeouts, which apply from the host's next start.</summary>
    public IReadOnlyList<DelayViewModel> RelayWaits { get; }

    /// <summary>Gets or sets whether the relay records captures from the host's next start.</summary>
    [ObservableProperty]
    public partial bool RecordsCaptures { get; set; }

    /// <summary>Gets whether the host records captures now, and where.</summary>
    [ObservableProperty]
    public partial string CapturesNow { get; private set; } = "";

    /// <summary>Gets whether the app is in French.</summary>
    [ObservableProperty]
    public partial bool IsFrench { get; private set; }

    /// <summary>Gets whether the app is in English.</summary>
    [ObservableProperty]
    public partial bool IsEnglish { get; private set; }

    /// <summary>Gets whether the app is dark, which the window follows.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLight))]
    public partial bool IsDark { get; private set; }

    /// <summary>Gets whether a delay was raised above its default, which the page restores.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RestoreDelaysCommand))]
    public partial bool HasRaisedDelays { get; private set; }

    /// <summary>Gets D3B's database file.</summary>
    [ObservableProperty]
    public partial string DatabasePath { get; private set; } = "";

    /// <summary>Gets the installed build's pack, once the host knows it.</summary>
    [ObservableProperty]
    public partial string PackPath { get; private set; } = "";

    /// <summary>Gets the file the app keeps its settings in.</summary>
    public string SettingsPath { get; }

    /// <summary>Gets the app's version and its update (D173).</summary>
    public UpdatesViewModel Updates { get; }

    /// <summary>Gets whether the app is light.</summary>
    public bool IsLight => !IsDark;

    /// <summary>Initializes a new instance of the <see cref="SettingsViewModel"/> class.</summary>
    /// <param name="localizer">The app's texts.</param>
    /// <param name="preferences">What the app remembers, or <see langword="null"/> to remember nothing.</param>
    /// <param name="useLanguage">Puts the app in a language, as its title bar does.</param>
    /// <param name="toasts">What the window says in its toasts, such as the delays restored.</param>
    /// <param name="updates">The app's version and its update, or <see langword="null"/> for an app that never updates.</param>
    public SettingsViewModel(Localizer localizer, AppPreferences? preferences, Action<CultureInfo> useLanguage, Toasts toasts, UpdatesViewModel? updates = null)
    {
        _localizer = localizer;
        _toasts = toasts;
        _preferences = preferences;
        _useLanguage = useLanguage;
        Updates = updates ?? new UpdatesViewModel(localizer, null, new Dialogs(), () => false, () => { });

        var stored = new ConfigurationBuilder().AddInMemoryCollection(preferences?.Host ?? new Dictionary<string, string?>()).Build();

        _delays = Stored(() => BotDelays.Read(stored), BotDelays.Measured);
        _timeouts = Stored(() => RelayTimeouts.Read(stored), RelayTimeouts.Default);
        RecordsCaptures = bool.TryParse(stored[RelaySettings.CapturesEnabledKey], out var records) && records;
        IsDark = preferences?.Theme != AppPreferences.Light;
        SettingsPath = preferences?.Path ?? AppPreferences.DefaultPath;
        Display = BotDelay("DelayDisplay", delays => delays.Display, (delays, delay) => delays with { Display = delay });
        BotWaits =
        [
            BotDelay("DelayAnswer", delays => delays.Answer, (delays, delay) => delays with { Answer = delay }),
            BotDelay("DelayWalkArrival", delays => delays.WalkArrival, (delays, delay) => delays with { WalkArrival = delay }),
            BotDelay("DelayWalkStep", delays => delays.WalkStep, (delays, delay) => delays with { WalkStep = delay }),
            BotDelay("DelayPlaced", delays => delays.Placed, (delays, delay) => delays with { Placed = delay }),
            BotDelay("DelayUseEnd", delays => delays.UseEnd, (delays, delay) => delays with { UseEnd = delay }),
            BotDelay("DelayBankFollow", delays => delays.BankFollow, (delays, delay) => delays with { BankFollow = delay }),
            BotDelay("DelayRecovery", delays => delays.Recovery, (delays, delay) => delays with { Recovery = delay }),
            BotDelay("DelayBetweenSteps", delays => delays.BetweenSteps, (delays, delay) => delays with { BetweenSteps = delay }),
            BotDelay("DelayFightAction", delays => delays.FightAction, (delays, delay) => delays with { FightAction = delay }),
        ];
        RelayWaits =
        [
            RelayDelay("DelayConnect", timeouts => timeouts.Connect, (timeouts, delay) => timeouts with { Connect = delay }),
            RelayDelay("DelayClose", timeouts => timeouts.Close, (timeouts, delay) => timeouts with { Close = delay }),
            RelayDelay("DelayHold", timeouts => timeouts.Hold, (timeouts, delay) => timeouts with { Hold = delay }),
            RelayDelay("DelaySelection", timeouts => timeouts.Selection, (timeouts, delay) => timeouts with { Selection = delay }),
        ];
        HasRaisedDelays = AnyRaised();
        _remembers = true;
    }

    /// <summary>Reads the host again, in the language chosen.</summary>
    /// <param name="services">The host's services while it runs, else <see langword="null"/>.</param>
    public void Refresh(IServiceProvider? services)
    {
        IsFrench = _localizer.Culture.Equals(Localizer.French);
        IsEnglish = !IsFrench;

        if (!_localizer.Culture.Equals(_labelled))
        {
            foreach (var delay in BotWaits.Concat(RelayWaits).Prepend(Display))
                delay.Relabel();

            _labelled = _localizer.Culture;
        }

        if (_bots is null && services?.GetService<Bots>() is { } bots)
            Attach(bots);

        if (services?.GetService<RelaySettings>() is not { } relay)
            CapturesNow = "";
        else if (relay.CapturesDirectory is { } directory)
            CapturesNow = _localizer.Format("CapturesRecording", directory);
        else
            CapturesNow = _localizer["CapturesNotRecording"];

        DatabasePath = services?.GetService<StorageSettings>()?.Path ?? StorageSettings.DefaultPath;
        PackPath = services?.GetService<PackSource>()?.PackPath ?? _localizer["PackPathUnknown"];
    }

    private static T Stored<T>(Func<T> read, T fallback)
    {
        try
        {
            return read();
        }
        catch (InvalidOperationException)
        {
            // A file edited by hand to a delay out of its bounds: the host says so as it starts; the page offers D3B's.
            return fallback;
        }
    }

    private DelayViewModel BotDelay(string key, Func<BotDelays, TimeSpan> read, Func<BotDelays, TimeSpan, BotDelays> write)
    {
        return new DelayViewModel(_localizer, key, "DelayMeasured", (read(BotDelays.Measured), read(BotDelays.Most)), () => read(_delays), delay => UseDelays(write(_delays, delay)));
    }

    private DelayViewModel RelayDelay(string key, Func<RelayTimeouts, TimeSpan> read, Func<RelayTimeouts, TimeSpan, RelayTimeouts> write)
    {
        return new DelayViewModel(_localizer, key, "DelayDefault", (read(RelayTimeouts.Default), read(RelayTimeouts.Most)), () => read(_timeouts), delay => UseTimeouts(write(_timeouts, delay)));
    }

    private void Attach(Bots bots)
    {
        // Changed while the host got ready, the page's delays win; else the bots', which its command line may raise.
        if (_changed)
            bots.Delays = _delays;
        else
            _delays = bots.Delays;

        _bots = bots;

        foreach (var delay in BotWaits.Prepend(Display))
            delay.Reread();

        HasRaisedDelays = AnyRaised();
    }

    private bool AnyRaised()
    {
        return BotWaits.Concat(RelayWaits).Any(delay => delay.IsRaised);
    }

    private void UseDelays(BotDelays delays)
    {
        _delays = delays;
        _changed = true;

        if (_bots is { } bots)
            bots.Delays = delays;

        _preferences?.RememberHost(delays.ToConfiguration());
        HasRaisedDelays = AnyRaised();
    }

    private void UseTimeouts(RelayTimeouts timeouts)
    {
        _timeouts = timeouts;
        _preferences?.RememberHost(timeouts.ToConfiguration());
        HasRaisedDelays = AnyRaised();
    }

    private void UseTheme(bool dark)
    {
        IsDark = dark;
        _preferences?.RememberTheme(dark ? AppPreferences.Dark : AppPreferences.Light);
    }

    partial void OnRecordsCapturesChanged(bool value)
    {
        if (!_remembers)
            return;

        // Off is the relay's default: forgotten, the host's own configuration decides again.
        _preferences?.RememberHost(new Dictionary<string, string?> { [RelaySettings.CapturesEnabledKey] = value ? bool.TrueString : null });
    }

    /// <summary>Says a path was copied.</summary>
    public void Copied()
    {
        _toasts.Show(new Toast(_localizer["Copied"], Tone.Success));
    }

    [RelayCommand(CanExecute = nameof(HasRaisedDelays))]
    private void RestoreDelays()
    {
        UseDelays(BotDelays.Measured with { Display = _delays.Display });
        UseTimeouts(RelayTimeouts.Default);

        foreach (var delay in BotWaits.Concat(RelayWaits))
            delay.Reread();

        HasRaisedDelays = AnyRaised();
        _toasts.Show(new Toast(_localizer["DelaysRestored"], Tone.Success));
    }

    [RelayCommand]
    private void UseFrench()
    {
        _useLanguage(Localizer.French);
    }

    [RelayCommand]
    private void UseEnglish()
    {
        _useLanguage(Localizer.English);
    }

    [RelayCommand]
    private void UseDark()
    {
        UseTheme(dark: true);
    }

    [RelayCommand]
    private void UseLight()
    {
        UseTheme(dark: false);
    }
}
