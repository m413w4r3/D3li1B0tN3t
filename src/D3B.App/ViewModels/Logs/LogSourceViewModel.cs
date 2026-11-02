using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using D3B.App.Controls;
using D3B.Bot.Runtime;

namespace D3B.App.ViewModels.Logs;

/// <summary>A source the logs page or the inspector shows: every one, the host, or a bot, a chip at the page's top (D111).</summary>
internal sealed partial class LogSourceViewModel : ObservableObject
{
    private readonly Action<LogSourceViewModel> _select;

    /// <summary>Gets whose lines it is.</summary>
    public LogSourceKind Kind { get; }

    /// <summary>Gets the bot, for a bot's source.</summary>
    public BotActor? Bot { get; }

    /// <summary>Gets whether it is a bot's, whose chip shows a dot of its connection.</summary>
    public bool IsBot => Kind is LogSourceKind.Bot;

    /// <summary>Gets its name: the bot's character's, or the app's word for every source or the host.</summary>
    [ObservableProperty]
    public partial string Name { get; set; } = "";

    /// <summary>Gets the tone of the bot's connection, for its chip's dot.</summary>
    [ObservableProperty]
    public partial Tone Tone { get; set; }

    /// <summary>Gets or sets whether the page shows it.</summary>
    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    /// <summary>Initializes a new instance of the <see cref="LogSourceViewModel"/> class.</summary>
    /// <param name="kind">Whose lines it is.</param>
    /// <param name="bot">The bot, for a bot's source.</param>
    /// <param name="select">Shows it on the page.</param>
    public LogSourceViewModel(LogSourceKind kind, BotActor? bot, Action<LogSourceViewModel> select)
    {
        Kind = kind;
        Bot = bot;
        _select = select;
    }

    [RelayCommand]
    private void Select()
    {
        _select(this);
    }
}
