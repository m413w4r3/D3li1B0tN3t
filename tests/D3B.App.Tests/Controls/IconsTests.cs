using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace D3B.App.Tests.Controls;

public sealed class IconsTests
{
    private static Task<T> OnTheUIThreadAsync<T>(Func<T> action)
    {
        return HeadlessUnitTestSession.GetOrStartForAssembly(typeof(IconsTests).Assembly).Dispatch(action, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task EveryIconOfTheAppReadsAsAGeometryWithinItsGrid()
    {
        // The build leaves the geometries' text unread: a path Avalonia cannot parse fails only once a view shows it.
        var icons = await OnTheUIThreadAsync(() =>
        {
            var dictionary = (ResourceDictionary)AvaloniaXamlLoader.Load(new Uri("avares://D3B.App/Themes/Icons.axaml"));

            return dictionary.Keys.OfType<string>().Select(key => (Key: key, Bounds: ((Geometry)dictionary[key]!).Bounds)).ToList();
        });

        Assert.True(icons.Count > 70);
        Assert.All(icons, icon => Assert.StartsWith("Icon", icon.Key, StringComparison.Ordinal));
        Assert.All(icons, icon => Assert.True(icon.Bounds is { Left: >= 0, Top: >= 0, Right: <= 24, Bottom: <= 24, Width: > 0 }, icon.Key));
    }
}
