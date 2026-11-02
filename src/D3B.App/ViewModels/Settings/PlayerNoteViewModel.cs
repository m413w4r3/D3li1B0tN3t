namespace D3B.App.ViewModels.Settings;

/// <summary>A character that plays the profile being edited, and what its settings lead to for it, such as the place chosen for its levels (D166).</summary>
/// <param name="Name">The character's name.</param>
/// <param name="Note">What the settings lead to for it, in the app's language, if the activity says.</param>
internal sealed record PlayerNoteViewModel(string Name, string? Note)
{
    /// <summary>Gets whether the activity says something of the character.</summary>
    public bool HasNote => Note is not null;
}
