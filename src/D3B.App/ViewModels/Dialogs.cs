using CommunityToolkit.Mvvm.ComponentModel;

namespace D3B.App.ViewModels;

/// <summary>The dialog the window shows over its pages, one at a time: a question to confirm, or a dialog of its own such as choosing resources (D159).</summary>
/// <remarks>
/// The window draws <see cref="Current"/> in ShadUI's dialog host, through the view its type has; the view models only
/// open and close it, so that they are tested without a window.
/// </remarks>
internal sealed partial class Dialogs : ObservableObject
{
    /// <summary>Gets the dialog shown, if any: a <see cref="QuestionViewModel"/> or a dialog's own view model.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOpen))]
    public partial object? Current { get; private set; }

    /// <summary>Gets whether a dialog shows.</summary>
    public bool IsOpen => Current is not null;

    /// <summary>Shows a dialog, in place of any other.</summary>
    /// <param name="dialog">The dialog's view model.</param>
    public void Show(object dialog)
    {
        ArgumentNullException.ThrowIfNull(dialog);
        Current = dialog;
    }

    /// <summary>Asks a question, answered by its buttons.</summary>
    /// <param name="question">The question.</param>
    public void Ask(QuestionViewModel question)
    {
        Show(question);
    }

    /// <summary>Closes the dialog shown, as cancelling it does.</summary>
    public void Close()
    {
        Current = null;
    }

    /// <summary>Closes a dialog, if it is still the one shown.</summary>
    /// <param name="dialog">The dialog.</param>
    public void Close(object dialog)
    {
        if (ReferenceEquals(Current, dialog))
            Current = null;
    }
}
