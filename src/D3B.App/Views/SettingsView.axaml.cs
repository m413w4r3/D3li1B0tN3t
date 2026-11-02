using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using D3B.App.ViewModels;

namespace D3B.App.Views;

/// <summary>The settings page: the machine's settings, in cards of rows (D151, D159).</summary>
internal sealed partial class SettingsView : UserControl
{
    /// <summary>Initializes a new instance of the <see cref="SettingsView"/> class.</summary>
    public SettingsView()
    {
        InitializeComponent();
    }

    private async void OnCopyPathClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { Tag: string { Length: > 0 } path } && TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(path);
            (DataContext as SettingsViewModel)?.Copied();
        }
    }
}
