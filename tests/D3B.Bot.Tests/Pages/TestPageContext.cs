using System.Globalization;
using D3B.Bot.Pages;

namespace D3B.Bot.Tests.Pages;

/// <summary>A page's context as the app gives it, its language changed and its refresh raised by the test.</summary>
internal sealed class TestPageContext : PageContext
{
    private CultureInfo _culture;

    public override IServiceProvider Services { get; }

    public override CultureInfo Culture => _culture;

    public TestPageContext(IServiceProvider services, CultureInfo culture)
    {
        Services = services;
        _culture = culture;
    }

    public void Use(CultureInfo culture)
    {
        _culture = culture;
        OnCultureChanged();
    }

    public void Refresh()
    {
        OnRefreshing();
    }
}
