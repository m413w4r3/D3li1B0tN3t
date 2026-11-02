using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D3B.App.Controls;
using D3B.App.Localization;
using D3B.App.ViewModels;
using D3B.App.ViewModels.Maps;
using D3B.App.Views;
using D3B.Bot.Tests.Runtime;
using D3B.Protocol.Game;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace D3B.App.Tests.Views;

public sealed class MapViewTests : IAsyncDisposable
{
    private const int Wall = 320;
    private const int Ground = 450;

    private readonly TestPlay _play = new(Wall);
    private readonly TestGameData _game = new();
    private readonly IHost _host;

    public MapViewTests()
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });

        builder.Services.AddSingleton(_play.Bots);
        builder.Services.AddSingleton(_play.Maps);
        builder.Services.AddSingleton(_play.World);
        builder.Services.AddSingleton(_game.Texts);
        builder.Services.AddSingleton(_game.Tables);
        _host = builder.Build();
    }

    public async ValueTask DisposeAsync()
    {
        _host.Dispose();
        await _play.DisposeAsync();
        _game.Dispose();
    }

    private static Task<T> OnTheUIThreadAsync<T>(Func<T> action)
    {
        return HeadlessUnitTestSession.GetOrStartForAssembly(typeof(MapViewTests).Assembly).Dispatch(action, TestContext.Current.CancellationToken);
    }

    private static Color Pixel(WriteableBitmap frame, Point point)
    {
        using var buffer = frame.Lock();

        var offset = ((int)point.Y * buffer.RowBytes) + ((int)point.X * 4);
        var pixel = new byte[4];

        Marshal.Copy(buffer.Address + offset, pixel, 0, 4);

        return buffer.Format == PixelFormat.Rgba8888
            ? Color.FromArgb(pixel[3], pixel[0], pixel[1], pixel[2])
            : Color.FromArgb(pixel[3], pixel[2], pixel[1], pixel[0]);
    }

    private static Color ColourOf(IBrush brush)
    {
        return ((ISolidColorBrush)brush).Color;
    }

    [Fact]
    public async Task AFightIsDrawnInEachThemesPalette()
    {
        const int Enemy = Ground;
        const int Red = 452;
        const int Blue = 454;

        var (connection, bot) = await _play.EnterAsync(300);

        connection.Server(new ContextCreationEvent { Context = ContextCreationEvent.Types.GameContext.Fight });
        connection.Server(new MapCurrentEvent { MapId = TestPlay.MapId });
        connection.Server(new FightFighterShowEvent
        {
            Fighter = new Actor
            {
                Id = TestPlay.CharacterId,
                Disposition = new ActorDisposition { CellId = 300 },
                Information = new ActorInformation { Fighter = new FightFighter { Spawn = new FighterSpawn { Alive = true, Team = FightTeam.Challenger } } },
            },
        });
        connection.Server(new FightFighterShowEvent
        {
            Fighter = new Actor
            {
                Id = -1,
                Disposition = new ActorDisposition { CellId = Enemy },
                Information = new ActorInformation { Fighter = new FightFighter { Spawn = new FighterSpawn { Alive = true, Team = FightTeam.Defender } } },
            },
        });
        connection.Server(new FightPlacementPossiblePositionsEvent { Cells = new PlacementCells { ChallengerCells = { 300, Red }, DefenderCells = { Enemy, Blue } } });
        await bot.ProcessedAsync();

        var drawn = await OnTheUIThreadAsync(() =>
        {
            var page = new MapViewModel(new Localizer(Localizer.English), new BotChoice());
            var window = new Window { Width = 1100, Height = 820, Content = new MapView { DataContext = page }, RequestedThemeVariant = ThemeVariant.Dark };
            var colours = new List<(Color Character, Color Enemy, Color Red, Color Blue, Color Walkable, Color DrawnCharacter, Color DrawnEnemy, Color DrawnRed, Color DrawnBlue)>();

            page.Refresh(_host.Services);
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var canvas = window.GetVisualDescendants().OfType<MapCanvas>().Single();

            foreach (var theme in new[] { ThemeVariant.Dark, ThemeVariant.Light })
            {
                window.RequestedThemeVariant = theme;
                Dispatcher.UIThread.RunJobs();

                var frame = window.CaptureRenderedFrame()!;
                var palette = canvas.Palette!;
                var layout = MapLayout.Fit(canvas.Bounds.Size);

                Point At(int cell)
                {
                    return canvas.TranslatePoint(layout.Center(cell), window)!.Value;
                }

                colours.Add((
                    ColourOf(palette.Character),
                    ColourOf(palette.Enemy),
                    ColourOf(palette.ChallengerCell),
                    ColourOf(palette.DefenderCell),
                    ColourOf(palette.Walkable),
                    Pixel(frame, At(300)),
                    Pixel(frame, At(Enemy)),
                    Pixel(frame, At(Red)),
                    Pixel(frame, At(Blue))));
            }

            window.Close();

            return colours;
        });

        Assert.All(drawn, colour =>
        {
            Assert.Equal(colour.Character, colour.DrawnCharacter);
            Assert.Equal(colour.Enemy, colour.DrawnEnemy);

            // The placement cells are tinted over the ground, red and blue apart.
            Assert.NotEqual(colour.Walkable, colour.DrawnRed);
            Assert.NotEqual(colour.Walkable, colour.DrawnBlue);
            Assert.NotEqual(colour.DrawnRed, colour.DrawnBlue);
            Assert.True(colour.DrawnRed.R > colour.DrawnRed.B && colour.DrawnBlue.B > colour.DrawnBlue.R);
        });
    }

    [Fact]
    public async Task TheMapIsDrawnInEachThemesPaletteAndTheCellUnderThePointerIsDescribed()
    {
        await _play.EnterAsync(300);

        var drawn = await OnTheUIThreadAsync(() =>
        {
            var page = new MapViewModel(new Localizer(Localizer.English), new BotChoice());
            var window = new Window { Width = 1100, Height = 820, Content = new MapView { DataContext = page }, RequestedThemeVariant = ThemeVariant.Dark };
            var colours = new List<(Color Expected, Color Drawn)>();

            page.Refresh(_host.Services);
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var canvas = window.GetVisualDescendants().OfType<MapCanvas>().Single();

            foreach (var theme in new[] { ThemeVariant.Dark, ThemeVariant.Light })
            {
                window.RequestedThemeVariant = theme;
                Dispatcher.UIThread.RunJobs();

                var frame = window.CaptureRenderedFrame()!;
                var palette = canvas.Palette!;
                var layout = MapLayout.Fit(canvas.Bounds.Size);

                Point At(int cell)
                {
                    return canvas.TranslatePoint(layout.Center(cell), window)!.Value;
                }

                colours.Add((ColourOf(palette.Character), Pixel(frame, At(300))));
                colours.Add((ColourOf(palette.Obstacle), Pixel(frame, At(Wall))));
                colours.Add((ColourOf(palette.Walkable), Pixel(frame, At(Ground))));
                colours.Add((ColourOf(palette.Resource), Pixel(frame, At(TestPlay.TreeCell))));
                colours.Add((ColourOf(palette.Door), Pixel(frame, At(TestPlay.DoorCell))));
            }

            var tree = MapLayout.Fit(canvas.Bounds.Size).Center(TestPlay.TreeCell);

            window.MouseMove(canvas.TranslatePoint(tree, window)!.Value);
            Dispatcher.UIThread.RunJobs();

            var card = window.GetVisualDescendants().OfType<Border>().Single(border => border.Name == "HoverCard");
            var said = card.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text).ToList();
            var hovered = (page.HoveredCell, card.IsVisible);

            // Closing the window takes the pointer off the map, which says so.
            window.Close();

            return (Colours: colours, Hovered: hovered.HoveredCell, Shown: hovered.IsVisible, Said: said, Left: page.HoveredCell);
        });

        Assert.All(drawn.Colours, colour => Assert.Equal(colour.Expected, colour.Drawn));
        Assert.NotEqual(drawn.Colours[0].Drawn, drawn.Colours[5].Drawn);
        Assert.Equal((TestPlay.TreeCell, true, null), (drawn.Hovered, drawn.Shown, drawn.Left));
        Assert.Contains("Ash tree: Cut", drawn.Said);
    }
}
