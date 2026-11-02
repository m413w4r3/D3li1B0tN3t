using Avalonia;
using Avalonia.Headless;

[assembly: AvaloniaTestApplication(typeof(D3B.App.Tests.HeadlessApp))]

namespace D3B.App.Tests;

/// <summary>The app with no screen, its views drawn by Skia, for the tests that show them.</summary>
public static class HeadlessApp
{
    public static AppBuilder BuildAvaloniaApp()
    {
        return App.Configure(AppBuilder.Configure<App>())
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
    }
}
