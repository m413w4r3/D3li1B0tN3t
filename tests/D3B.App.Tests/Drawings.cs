using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;

namespace D3B.App.Tests;

/// <summary>Draws a window without a screen in both themes, kept as images to look at when <c>D3B_DRAWINGS</c> names a folder.</summary>
internal static class Drawings
{
    public static void Draw(Window window, string name)
    {
        // Both themes drawn, as the app has them; the app's own is given back.
        foreach (var theme in new[] { ThemeVariant.Dark, ThemeVariant.Light })
        {
            Application.Current!.RequestedThemeVariant = theme;
            Dispatcher.UIThread.RunJobs();
            Assert.NotNull(window.CaptureRenderedFrame());
            Save(window, $"{name}-{theme}");
        }

        Application.Current!.RequestedThemeVariant = ThemeVariant.Dark;
        Dispatcher.UIThread.RunJobs();
    }

    private static void Save(Window window, string name)
    {
        if (Environment.GetEnvironmentVariable("D3B_DRAWINGS") is not { Length: > 0 } folder)
            return;

        Directory.CreateDirectory(folder);
        window.CaptureRenderedFrame()?.Save(Path.Combine(folder, name + ".png"), PngBitmapEncoderOptions.Default);
    }
}
