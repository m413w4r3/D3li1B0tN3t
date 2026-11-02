using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using D3B.App.Controls;
using D3B.App.Localization;
using D3B.App.Updates;

namespace D3B.App.ViewModels;

/// <summary>The app's version and its update, shown in the window's banner and on the settings page (D173).</summary>
internal sealed partial class UpdatesViewModel : ObservableObject
{
    private readonly Localizer _localizer;
    private readonly AppUpdates? _updates;
    private readonly Dialogs _dialogs;
    private readonly Func<bool> _botsPlay;
    private readonly Action _close;

    /// <summary>Gets where the update stands, said.</summary>
    [ObservableProperty]
    public partial string StatusText { get; private set; } = "";

    /// <summary>Gets the tone of where the update stands.</summary>
    [ObservableProperty]
    public partial Tone StatusTone { get; private set; }

    /// <summary>Gets whether a newer version is downloaded, which a restart installs.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RestartCommand))]
    public partial bool IsReady { get; private set; }

    /// <summary>Gets what the window's banner says once a newer version is downloaded.</summary>
    [ObservableProperty]
    public partial string ReadyText { get; private set; } = "";

    /// <summary>Gets whether a check or a download runs.</summary>
    [ObservableProperty]
    public partial bool IsBusy { get; private set; }

    /// <summary>Gets whether the user may check for a newer version: installed, and neither busy nor holding one.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CheckCommand))]
    public partial bool CanCheck { get; private set; }

    /// <summary>Gets the app's version.</summary>
    public string Version { get; } = AppRelease.Version;

    /// <summary>Gets the client build whose files the app ships.</summary>
    public string ClientVersion { get; } = AppRelease.ClientVersion;

    /// <summary>Gets whether the app runs installed, which alone updates itself.</summary>
    public bool IsInstalled => _updates?.IsInstalled ?? false;

    /// <summary>Initializes a new instance of the <see cref="UpdatesViewModel"/> class.</summary>
    /// <param name="localizer">The app's texts.</param>
    /// <param name="updates">The app's updates, or <see langword="null"/> for an app that never updates, such as in tests.</param>
    /// <param name="dialogs">The window's dialogs, which ask before a restart disconnects the clients.</param>
    /// <param name="botsPlay">Tells whether bots play, which a restart stops.</param>
    /// <param name="close">Closes the app, which then installs the version downloaded and starts again.</param>
    public UpdatesViewModel(Localizer localizer, AppUpdates? updates, Dialogs dialogs, Func<bool> botsPlay, Action close)
    {
        _localizer = localizer;
        _updates = updates;
        _dialogs = dialogs;
        _botsPlay = botsPlay;
        _close = close;
        Refresh();
    }

    /// <summary>Reads where the update stands again, in the language chosen.</summary>
    public void Refresh()
    {
        var status = _updates?.Status ?? UpdateStatus.NotInstalled;
        var version = _updates?.NewVersion ?? "";

        IsReady = status is UpdateStatus.Ready;
        IsBusy = status is UpdateStatus.Checking or UpdateStatus.Downloading;
        CanCheck = status is UpdateStatus.Idle or UpdateStatus.UpToDate or UpdateStatus.Failed;
        ReadyText = IsReady ? _localizer.Format("UpdateReady", version) : "";
        (StatusText, StatusTone) = status switch
        {
            UpdateStatus.NotInstalled => (_localizer["UpdateNotInstalled"], Tone.Neutral),
            UpdateStatus.Checking => (_localizer["UpdateChecking"], Tone.Info),
            UpdateStatus.UpToDate => (_localizer["UpdateUpToDate"], Tone.Success),
            UpdateStatus.Downloading => (_localizer.Format("UpdateDownloading", version, _updates?.Progress ?? 0), Tone.Info),
            UpdateStatus.Ready => (_localizer.Format("UpdateReadyDetail", version), Tone.Success),
            UpdateStatus.Failed => (_localizer.Format("UpdateFailed", _updates?.Failure ?? ""), Tone.Warning),
            _ => (_localizer["UpdateNotChecked"], Tone.Neutral),
        };
    }

    [RelayCommand(CanExecute = nameof(CanCheck))]
    private void Check()
    {
        _ = _updates?.CheckAsync();
        Refresh();
    }

    [RelayCommand(CanExecute = nameof(IsReady))]
    private void Restart()
    {
        // Restarting stops the bots and disconnects the clients (D91): asked first while bots play.
        if (!_botsPlay())
        {
            RestartNow();

            return;
        }

        _dialogs.Ask(new QuestionViewModel(
            _dialogs,
            _localizer["UpdateRestartQuestion"],
            _localizer["UpdateRestartMessage"],
            _localizer["UpdateRestart"],
            _localizer["Cancel"],
            RestartNow));
    }

    private void RestartNow()
    {
        if (_updates?.RequestRestart() is true)
            _close();
    }
}
