using D3B.App.Localization;
using D3B.Bot.Settings;
using D3B.Data.Texts;

namespace D3B.App.ViewModels.Settings;

/// <summary>Names the game's values the settings name, in the app's language, from the pack's texts.</summary>
internal sealed class GameNames
{
    private readonly GameTexts? _texts;
    private readonly Localizer _localizer;

    /// <summary>Initializes a new instance of the <see cref="GameNames"/> class.</summary>
    /// <param name="texts">The pack's texts, or <see langword="null"/> when the host has none.</param>
    /// <param name="localizer">The app's texts, which name a value the pack's do not.</param>
    public GameNames(GameTexts? texts, Localizer localizer)
    {
        _texts = texts;
        _localizer = localizer;
    }

    /// <summary>Names a value of the game.</summary>
    /// <param name="kind">Its kind.</param>
    /// <param name="reference">The value.</param>
    /// <returns>Its name in the pack's texts, else its kind and id.</returns>
    public string Name(SettingKind kind, GameReference reference)
    {
        if (_texts is not null && reference.NameId != 0 && _texts.TryGet(_localizer.GameLanguage, reference.NameId, out var name))
            return name;

        var key = kind switch
        {
            SettingKind.Job => "JobNamed",
            SettingKind.Area => "AreaNamed",
            SettingKind.SubArea => "SubArea",
            SettingKind.Item => "ItemNamed",
            SettingKind.Monster => "MonsterNamed",
            SettingKind.MonsterFamily => "MonsterFamilyNamed",
            _ => "ResourceNamed",
        };

        return _localizer.Format(key, reference.Id);
    }

    /// <summary>Says a value's level, beside its name.</summary>
    /// <param name="reference">The value.</param>
    /// <returns>Its level, or <see langword="null"/> when it has none.</returns>
    public string? Detail(GameReference reference)
    {
        return reference.Level is { } level ? _localizer.Format("Level", level) : null;
    }
}
