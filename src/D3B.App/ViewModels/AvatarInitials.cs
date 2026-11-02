namespace D3B.App.ViewModels;

/// <summary>A character's initials, as its avatar shows them.</summary>
internal static class AvatarInitials
{
    /// <summary>Gets a name's initials: its two first words' first letters, or a word's two first.</summary>
    /// <param name="name">The name.</param>
    /// <returns>The initials, upper case, or a question mark for no name.</returns>
    public static string Of(string name)
    {
        var words = name.Split([' ', '-', '_'], StringSplitOptions.RemoveEmptyEntries);

        return words.Length switch
        {
            0 => "?",
            1 => words[0][..Math.Min(2, words[0].Length)].ToUpperInvariant(),
            _ => string.Concat(words[0][0], words[1][0]).ToUpperInvariant(),
        };
    }
}
