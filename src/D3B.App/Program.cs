using Avalonia;
using D3B.App.Hosting;
using D3B.App.Updates;
using Microsoft.Extensions.Configuration;
using Velopack;

namespace D3B.App;

/// <summary>Starts the bots' host, then the window; stops the host once the window is gone (D91).</summary>
internal static class Program
{
    /// <summary>Runs the app.</summary>
    /// <param name="args">The command-line arguments, read as the host's configuration, as <c>D3B.Host</c> reads them.</param>
    /// <returns>The app's exit code.</returns>
    [STAThread]
    public static int Main(string[] args)
    {
        // First of all: run by its installer, an update or its uninstaller, the app does what they ask and ends here (D171).
        VelopackApp.Build().Run();

        // Before the host: it starts with the settings the app changed (D151).
        var preferences = AppPreferences.Load(AppPreferences.DefaultPath);
        var configuration = new ConfigurationBuilder().AddEnvironmentVariables().AddCommandLine(args).Build();
        var updates = new AppUpdates(new VelopackUpdateClient(UpdateSettings.SourceIn(configuration)));
        var host = new HostRunner(args, preferences.Host);

        host.Start();
        _ = updates.CheckAsync();

        try
        {
            return BuildAvaloniaApp(host, preferences, updates).StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            // Off the UI's thread, which no longer runs: the bots stop, and the clients get their configuration back (D35).
            Task.Run(host.StopAsync).GetAwaiter().GetResult();

            // Last: installing the version downloaded may end the process at once (D173).
            updates.Finish();
        }
    }

    private static AppBuilder BuildAvaloniaApp(HostRunner host, AppPreferences preferences, AppUpdates updates)
    {
        return App.Configure(AppBuilder.Configure(() => new App { Host = host, Preferences = preferences, Updates = updates }))
            .UsePlatformDetect()
            .LogToTrace();
    }
}
