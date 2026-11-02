using D3B.App.Hosting;
using D3B.App.Localization;
using D3B.App.ViewModels;
using D3B.Data.Tests;

namespace D3B.App.Tests.Hosting;

public sealed class AppPreferencesTests
{
    [Fact]
    public void TheAppRemembersItsLanguageAndThemeFromOneRunToTheNext()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.Combine("D3B", "app.json");
        var first = AppPreferences.Load(path);

        Assert.Equal((null, null), (first.Language, first.Theme));

        first.RememberLanguage("fr");
        first.RememberTheme(AppPreferences.Light);

        var next = AppPreferences.Load(path);

        Assert.Equal(("fr", AppPreferences.Light), (next.Language, next.Theme));
    }

    [Fact]
    public void TheHostsSettingsAreRememberedAndForgottenOneByOne()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.Combine("app.json");
        var first = AppPreferences.Load(path);

        first.RememberLanguage("fr");
        first.RememberHost(new Dictionary<string, string?> { ["Delays:Display"] = "1.5", ["Delays:Answer"] = "6" });
        first.RememberHost(new Dictionary<string, string?> { ["Delays:Answer"] = null, ["Network:Captures:Enabled"] = "True" });

        var next = AppPreferences.Load(path);

        Assert.Equal("fr", next.Language);
        Assert.Equal(new Dictionary<string, string?> { ["Delays:Display"] = "1.5", ["Network:Captures:Enabled"] = "True" }, next.Host);
        Assert.Empty(first.Host.Keys.Except(next.Host.Keys));
    }

    [Fact]
    public void AFileThatDoesNotReadLeavesTheDefaults()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.Combine("app.json");

        File.WriteAllText(path, "{ not json");

        var preferences = AppPreferences.Load(path);

        Assert.Equal((null, null), (preferences.Language, preferences.Theme));
        Assert.Empty(preferences.Host);
        Assert.Equal(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "D3B", "app.json"), AppPreferences.DefaultPath);
    }

    [Fact]
    public async Task TheWindowRemembersTheLanguageItSwitchesTo()
    {
        using var directory = new TemporaryDirectory();
        await using var store = new TestStore();
        var path = directory.Combine("app.json");
        var preferences = AppPreferences.Load(path);

        await store.OpenedAsync();

        var main = new MainViewModel(await TestHosts.RunningAsync(store.Host), new Localizer(Localizer.English), preferences);

        main.Settings.UseFrenchCommand.Execute(null);
        main.Settings.UseLightCommand.Execute(null);
        main.ToggleSidebarCommand.Execute(null);

        Assert.Equal(("fr", AppPreferences.Light), (AppPreferences.Load(path).Language, AppPreferences.Load(path).Theme));
        Assert.True(AppPreferences.Load(path).IsSidebarCollapsed);
        Assert.False(new MainViewModel(await TestHosts.RunningAsync(store.Host), new Localizer(Localizer.English), AppPreferences.Load(path)).IsSidebarExpanded);

        main.Settings.UseDarkCommand.Execute(null);

        Assert.Equal(AppPreferences.Dark, AppPreferences.Load(path).Theme);
    }
}
