using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D3B.App.Hosting;
using D3B.App.Localization;
using D3B.App.ViewModels;
using D3B.App.Views;
using D3B.Bot.Activities;
using D3B.Bot.Pages;
using D3B.Bot.Runtime;
using D3B.Bot.Tests;
using D3B.Bot.Tests.Runtime;
using D3B.Data;
using D3B.Data.Tests;
using D3B.Network.Relay;
using D3B.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace D3B.App.Tests.Views;

public sealed class SettingsViewTests : IAsyncDisposable
{
    private readonly TestPlay _play = new() { Bots = { Delays = BotDelays.Measured } };
    private readonly TemporaryDirectory _directory = new();

    private static List<string> Texts(Visual root)
    {
        return [.. root.GetVisualDescendants().OfType<TextBlock>().Where(block => block.IsEffectivelyVisible).Select(block => block.Text).OfType<string>().Where(text => text.Length > 0)];
    }

    private static List<string?> Boxes(Visual root)
    {
        return [.. root.GetVisualDescendants().OfType<NumericUpDown>().Select(box => box.Text)];
    }

    public async ValueTask DisposeAsync()
    {
        await _play.DisposeAsync();
        _directory.Dispose();
    }

    private IHost TestHost()
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });

        builder.Services.AddSingleton(_play.Bots);
        builder.Services.AddSingleton(_play.World);
        builder.Services.AddSingleton(new ClientBuild(TestInstall.Version, null));
        builder.Services.AddSingleton(new ActivityCatalog([]));
        builder.Services.AddSingleton(new PageCatalog([]));
        builder.Services.AddSingleton(new StorageSettings { Path = @"C:\Users\Player\AppData\Local\D3B\d3b.db" });
        builder.Services.AddSingleton(new RelaySettings
        {
            Address = RelaySettings.DefaultAddress,
            Ports = RelaySettings.DefaultPorts,
            GamePorts = RelaySettings.DefaultGamePorts,
            ClientDirectory = "no-client",
            RedirectDirectory = "no-redirect",
            CapturesDirectory = @"C:\Users\Player\AppData\Local\D3B\captures",
        });

        return builder.Build();
    }

    [Fact]
    public async Task TheSettingsPageIsDrawnInBothLanguagesAndThemesAtTheRailsFoot()
    {
        using var host = TestHost();
        var runner = await TestHosts.RunningAsync(host);
        var preferences = AppPreferences.Load(_directory.Combine("app.json"));

        await _play.EnterAsync(300);

        var (english, french, raised, boxes) = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(SettingsViewTests).Assembly).Dispatch(
            () =>
            {
                // The views bind to the app's own texts, which only the window's tests change, and give back.
                var localizer = Localizer.Instance;

                localizer.Use(Localizer.English);

                var main = new MainViewModel(runner, localizer, preferences);
                var window = new MainWindow { DataContext = main, Height = 2240 };

                main.Refresh();
                main.ShowSettingsCommand.Execute(null);
                main.Settings.Display.Value = 1.2m;
                main.Settings.RelayWaits[0].Value = 20m;
                window.Show();
                Dispatcher.UIThread.RunJobs();

                var english = Texts(window);
                var boxes = new List<List<string?>> { Boxes(window) };

                Drawings.Draw(window, "settings-en");
                main.Settings.UseFrenchCommand.Execute(null);
                Dispatcher.UIThread.RunJobs();

                var french = Texts(window);

                boxes.Add(Boxes(window));
                Drawings.Draw(window, "settings-fr");

                var raised = window.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Ellipse>().Count(dot => dot.Classes.Contains("Raised") && dot.IsEffectivelyVisible);

                localizer.Use(Localizer.English);
                window.Close();

                return (english, french, raised, boxes);
            },
            TestContext.Current.CancellationToken);

        Assert.Contains("Settings", english);
        Assert.Contains("Display time", english);
        Assert.Contains("1.2 s", english);
        Assert.Contains("Server's answer", english);
        Assert.Contains("Restore", english);
        Assert.Contains("Record the connections", english);
        Assert.Contains(@"Recording now, in C:\Users\Player\AppData\Local\D3B\captures", english);
        Assert.Contains("Appearance", english);
        Assert.Contains("English", english);
        Assert.Contains("Database", english);
        Assert.DoesNotContain(english, text => text.StartsWith('[') && text.EndsWith(']'));
        Assert.Contains("Paramètres", french);
        Assert.Contains("Temps d'affichage", french);
        Assert.Contains("1,2 s", french);
        Assert.Contains("Réponse du serveur", french);
        Assert.Contains("Rétablir", french);
        Assert.Contains("Au prochain démarrage", french);
        Assert.Contains("Sombre", french);
        Assert.Contains("Base de données", french);
        Assert.DoesNotContain("Display time", french);
        Assert.Equal(2, raised);

        // In the app's language, not the system's.
        Assert.Equal(["3.0", "1.0", "0.5", "10.0", "2.0", "1.0", "30.0", "0.5", "0.9", "20.0", "5.0", "5.0", "60.0"], boxes[0]);
        Assert.Equal("3,0", boxes[1][0]);
    }
}
