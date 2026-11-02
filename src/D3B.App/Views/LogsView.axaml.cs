using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Threading;
using D3B.App.ViewModels.Logs;

namespace D3B.App.Views;

/// <summary>The logs page: the lines of a bot, of the host or of all, which it follows as they come (D111).</summary>
internal sealed partial class LogsView : UserControl
{
    private LogsViewModel? _model;
    private bool _scrolling;

    /// <summary>Initializes a new instance of the <see cref="LogsView"/> class.</summary>
    public LogsView()
    {
        InitializeComponent();
        Scroller.ScrollChanged += OnScrollChanged;
    }

    /// <inheritdoc/>
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_model is not null)
        {
            _model.Lines.CollectionChanged -= OnLinesChanged;
            _model.PropertyChanged -= OnModelChanged;
        }

        _model = DataContext as LogsViewModel;

        if (_model is not null)
        {
            _model.Lines.CollectionChanged += OnLinesChanged;
            _model.PropertyChanged += OnModelChanged;
        }
    }

    private void OnLinesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_model is { Follow: true })
            ScrollToEnd();
    }

    private void OnModelChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LogsViewModel.Follow) && _model is { Follow: true })
            ScrollToEnd();
    }

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        // Scrolling up to read stops following: the lines that come no longer move what is read.
        if (!_scrolling && _model is { Follow: true } && e.OffsetDelta.Y < 0 && e.ExtentDelta.Y == 0)
            _model.Follow = false;
    }

    private void ScrollToEnd()
    {
        Dispatcher.UIThread.Post(() =>
        {
            _scrolling = true;
            Scroller.ScrollToEnd();
            _scrolling = false;
        }, DispatcherPriority.Background);
    }

    private async void OnCopyClick(object? sender, RoutedEventArgs e)
    {
        if (_model is not null && TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(_model.Text());
            _model.Copied();
        }
    }
}
