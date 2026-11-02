using System.ComponentModel;
using Avalonia.Controls.Primitives;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Styling;
using D3B.App.Controls;
using D3B.App.ViewModels;
using D3B.App.ViewModels.Settings;
using ShadUI;

namespace D3B.App.Views;

/// <summary>The app's window: where the bots' host stands, its pages beside the sidebar, its dialogs and toasts, dark unless the settings say otherwise.</summary>
internal sealed partial class MainWindow : ShadUI.Window
{
    private readonly DialogManager _dialogManager = new DialogManager()
        .Register<QuestionView, QuestionViewModel>()
        .Register<GamePickerView, GamePickerViewModel>()
        .Register<NewProfileView, NewProfileViewModel>();

    private readonly ToastManager _toastManager = new();
    private MainViewModel? _main;
    private object? _dialog;

    /// <summary>Initializes a new instance of the <see cref="MainWindow"/> class.</summary>
    public MainWindow()
    {
        InitializeComponent();
        DialogHost.Manager = _dialogManager;
        ToastHost.Manager = _toastManager;
        Sidebar.AddHandler(ToggleButton.IsCheckedChangedEvent, OnSidebarChecked);
    }

    /// <inheritdoc/>
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_main is not null)
        {
            _main.Settings.PropertyChanged -= OnSettingsChanged;
            _main.Dialogs.PropertyChanged -= OnDialogsChanged;
            _main.Toasts.Shown -= OnToastShown;
        }

        _main = DataContext as MainViewModel;

        if (_main is not null)
        {
            _main.Settings.PropertyChanged += OnSettingsChanged;
            _main.Dialogs.PropertyChanged += OnDialogsChanged;
            _main.Toasts.Shown += OnToastShown;
        }
    }

    /// <inheritdoc/>
    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _dialogManager.Dispose();
    }

    private void OnSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsViewModel.IsDark) && sender is SettingsViewModel settings && Avalonia.Application.Current is { } application)
            application.RequestedThemeVariant = settings.IsDark ? ThemeVariant.Dark : ThemeVariant.Light;
    }

    private void OnSidebarChecked(object? sender, RoutedEventArgs e)
    {
        // ShadUI's sidebar checks the item of its route, and no more: a page checked by a click, a key or a screen reader is
        // the one to show, which then names the route back.
        if (e.Source is SidebarItem { IsChecked: true, Route: { Length: > 0 } route } && _main is not null && route != _main.CurrentRoute)
            _main.Navigate(route);
    }

    private void OnDialogsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ViewModels.Dialogs.Current) || sender is not ViewModels.Dialogs dialogs)
            return;

        // ShadUI finds a dialog's view by its context's own type, so each is shown as what it is.
        if (_dialog is not null)
            _dialogManager.Close(_dialog);

        _dialog = dialogs.Current;

        switch (_dialog)
        {
            case QuestionViewModel question:
                _dialogManager.CreateDialog(question).WithMaxWidth(440).Show();
                break;
            case GamePickerViewModel picker:
                _dialogManager.CreateDialog(picker).WithMinWidth(560).WithMaxWidth(720).Show();
                break;
            case NewProfileViewModel profile:
                _dialogManager.CreateDialog(profile).WithMaxWidth(440).Show();
                break;
        }
    }

    private void OnToastShown(Toast toast)
    {
        var builder = _toastManager.CreateToast(toast.Text).DismissOnClick().WithDelay(5);

        if (toast is { ActionText: { } text, Action: { } action })
            builder = builder.WithAction(text, action);

        switch (toast.Tone)
        {
            case Tone.Success:
                builder.ShowSuccess();
                break;
            case Tone.Warning:
                builder.ShowWarning();
                break;
            case Tone.Danger:
                builder.ShowError();
                break;
            case Tone.Info:
                builder.ShowInfo();
                break;
            default:
                builder.Show();
                break;
        }
    }

    private async void OnCopyFailureClick(object? sender, RoutedEventArgs e)
    {
        // What to run to fix it, often: a command the owner pastes in a terminal.
        if (DataContext is MainViewModel { HostFailure: { } failure } && Clipboard is { } clipboard)
            await clipboard.SetTextAsync(failure);
    }
}
