using Avalonia.Controls;
using Avalonia.Data;
using D3B.Bot.Localization;
using D3B.Bot.Pages;
using D3B.Bot.Runtime;
using Microsoft.Extensions.DependencyInjection;

namespace D3B.App.Tests.Pages;

/// <summary>A module's page, as a module writes one: a greeting bound to its texts, and the bots it reads as the window refreshes it.</summary>
internal sealed class TestPage : UserControl
{
    public const string Id = "d3b.tests.page";

    private readonly PageContext _context;
    private readonly PageTexts _texts;
    private readonly TextBlock _greeting = new();
    private readonly TextBlock _count = new();

    /// <summary>Gets the greeting, bound to the page's texts as a view binds <c>{Binding Texts[Greeting]}</c>.</summary>
    public string? Greeting => _greeting.Text;

    /// <summary>Gets how many bots the host held the last time the window refreshed the page, in the app's language.</summary>
    public string? Count => _count.Text;

    /// <summary>Gets how many times the window refreshed the page.</summary>
    public int Refreshes { get; private set; }

    public TestPage(PageContext context)
    {
        _context = context;
        _texts = context.Texts(TestPageTexts.Resources);
        _greeting.Bind(TextBlock.TextProperty, new ReflectionBinding("[Greeting]") { Source = _texts, Mode = BindingMode.OneWay });
        Content = new StackPanel { Children = { _greeting, _count } };
        context.Refreshing += OnRefreshing;
    }

    /// <summary>The page as its module registers it, its title one of its texts.</summary>
    /// <param name="made">Told each time the app makes the page.</param>
    public static PageDefinition Definition(Action? made = null)
    {
        return new PageDefinition(Id, LocalizedText.From(TestPageTexts.Resources, "Title"), "M4 4 H20 V20 H4 Z", context =>
        {
            made?.Invoke();

            return new TestPage(context);
        });
    }

    private void OnRefreshing(object? sender, EventArgs e)
    {
        Refreshes++;
        _count.Text = _texts.Plural("Bots", _context.Services.GetRequiredService<Bots>().All.Count);
    }
}
