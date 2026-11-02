using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using D3B.App.ViewModels.Inspector;

namespace D3B.App.Views;

/// <summary>The protocol inspector: a bot's game frames as they go, or a capture's, which it follows as they come (D111, D115).</summary>
internal sealed partial class InspectorView : UserControl
{
    private InspectorViewModel? _model;
    private bool _scrolling;

    /// <summary>Initializes a new instance of the <see cref="InspectorView"/> class.</summary>
    public InspectorView()
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
            _model.Frames.CollectionChanged -= OnFramesChanged;
            _model.PropertyChanged -= OnModelChanged;
        }

        _model = DataContext as InspectorViewModel;

        if (_model is not null)
        {
            _model.Frames.CollectionChanged += OnFramesChanged;
            _model.PropertyChanged += OnModelChanged;
        }
    }

    private void OnFramesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_model is { Follow: true })
            ScrollToEnd();
    }

    private void OnModelChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(InspectorViewModel.Follow) && _model is { Follow: true })
            ScrollToEnd();
    }

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        // Scrolling up to read stops following: the frames that come no longer move what is read.
        if (!_scrolling && _model is { Follow: true } && e.OffsetDelta.Y < 0 && e.ExtentDelta.Y == 0)
            _model.Follow = false;
    }

    private void OnFrameTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as Control)?.DataContext is FrameRowViewModel row)
            row.ToggleCommand.Execute(null);
    }

    private void OnCapturesOpened(object? sender, EventArgs e)
    {
        _model?.ReadCaptures();
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
}
