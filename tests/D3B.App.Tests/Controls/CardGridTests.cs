using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using D3B.App.Controls;

namespace D3B.App.Tests.Controls;

public sealed class CardGridTests
{
    private static Task<T> OnTheUIThreadAsync<T>(Func<T> action)
    {
        return HeadlessUnitTestSession.GetOrStartForAssembly(typeof(CardGridTests).Assembly).Dispatch(action, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task AWideChildTakesAWholeRowAndEndsTheRowBeforeIt()
    {
        var bounds = await OnTheUIThreadAsync(() =>
        {
            var children = Enumerable.Range(0, 4).Select(_ => new Border { Height = 20 }).ToList();
            var grid = new CardGrid { MaxColumns = 2, MinColumnWidth = 100, Spacing = 10 };

            CardGrid.SetIsWide(children[1], true);
            grid.Children.AddRange(children);
            grid.Measure(new Size(410, double.PositiveInfinity));
            grid.Arrange(new Rect(0, 0, 410, grid.DesiredSize.Height));

            return children.Select(child => child.Bounds).ToList();
        });

        Assert.Equal(new Rect(0, 0, 200, 20), bounds[0]);
        Assert.Equal(new Rect(0, 30, 410, 20), bounds[1]);
        Assert.Equal(new Rect(0, 60, 200, 20), bounds[2]);
        Assert.Equal(new Rect(210, 60, 200, 20), bounds[3]);
    }
}
