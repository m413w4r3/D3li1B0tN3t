using CommunityToolkit.Mvvm.Input;

namespace D3B.App.ViewModels;

/// <summary>A question the app asks before doing what cannot be undone, or loses what was not saved: its words and its answers.</summary>
internal sealed partial class QuestionViewModel
{
    private readonly Dialogs _dialogs;
    private readonly Action _confirm;
    private readonly Action? _other;

    /// <summary>Gets the question.</summary>
    public string Title { get; }

    /// <summary>Gets what it is about, if it says more than its title.</summary>
    public string? Message { get; }

    /// <summary>Gets the answer that does it.</summary>
    public string ConfirmText { get; }

    /// <summary>Gets a second answer, such as leaving without saving, if it has one.</summary>
    public string? OtherText { get; }

    /// <summary>Gets the answer that does nothing.</summary>
    public string CancelText { get; }

    /// <summary>Gets whether confirming destroys something, which its button says in the danger tone.</summary>
    public bool IsDestructive { get; }

    /// <summary>Gets whether it has a second answer.</summary>
    public bool HasOther => OtherText is not null;

    /// <summary>Gets whether it says more than its title.</summary>
    public bool HasMessage => Message is not null;

    /// <summary>Initializes a new instance of the <see cref="QuestionViewModel"/> class.</summary>
    /// <param name="dialogs">The window's dialogs, which it closes once answered.</param>
    /// <param name="title">The question.</param>
    /// <param name="message">What it is about, or <see langword="null"/>.</param>
    /// <param name="confirmText">The answer that does it.</param>
    /// <param name="cancelText">The answer that does nothing.</param>
    /// <param name="confirm">What confirming does.</param>
    /// <param name="destructive">Whether confirming destroys something.</param>
    /// <param name="otherText">A second answer, or <see langword="null"/>.</param>
    /// <param name="other">What the second answer does.</param>
    public QuestionViewModel(Dialogs dialogs, string title, string? message, string confirmText, string cancelText, Action confirm, bool destructive = false, string? otherText = null, Action? other = null)
    {
        _dialogs = dialogs;
        Title = title;
        Message = message;
        ConfirmText = confirmText;
        CancelText = cancelText;
        _confirm = confirm;
        IsDestructive = destructive;
        OtherText = otherText;
        _other = other;
    }

    [RelayCommand]
    private void Confirm()
    {
        _dialogs.Close(this);
        _confirm();
    }

    [RelayCommand]
    private void Other()
    {
        _dialogs.Close(this);
        _other?.Invoke();
    }

    [RelayCommand]
    private void Cancel()
    {
        _dialogs.Close(this);
    }
}
