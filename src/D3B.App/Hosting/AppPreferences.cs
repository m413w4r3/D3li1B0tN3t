using System.Text.Json;

namespace D3B.App.Hosting;

/// <summary>What the app remembers of its own: its language and theme, its sidebar folded or not, and the host's settings it changed, in a file of its own beside the database (D101, D151, D159).</summary>
/// <remarks>
/// <para>
/// Read before the window shows, while the host, and its database, still get ready: the window opens in the language and
/// theme it was left in. A file that does not read, or cannot be written, leaves the app on its defaults, the system's
/// language and dark, and the host on its own.
/// </para>
/// <para>
/// The host's settings are its configuration's own keys and values, such as <c>Delays:Display</c> in seconds or
/// <c>Network:Captures:Enabled</c>, which <c>D3B.Host</c> reads the same way: the app gives them to the host as it
/// starts, before the host's own configuration and command line, which win over them. Only what the settings page
/// changed is kept; a setting given back its default is forgotten, so that it follows D3B's.
/// </para>
/// </remarks>
internal sealed class AppPreferences
{
    /// <summary>The theme that is not the default.</summary>
    public const string Light = "Light";

    /// <summary>The default theme.</summary>
    public const string Dark = "Dark";

    private static readonly JsonSerializerOptions _json = new() { WriteIndented = true };

    private readonly SortedDictionary<string, string> _host;

    /// <summary>Gets the file by default: <c>%LOCALAPPDATA%\D3B\app.json</c>.</summary>
    public static string DefaultPath => System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "D3B", "app.json");

    /// <summary>Gets the file.</summary>
    public string Path { get; }

    /// <summary>Gets the language the app was left in, <c>fr</c> or <c>en</c>, if it was changed.</summary>
    public string? Language { get; private set; }

    /// <summary>Gets the theme the app was left in, if it was changed.</summary>
    public string? Theme { get; private set; }

    /// <summary>Gets whether the sidebar was left folded to its icons.</summary>
    public bool IsSidebarCollapsed { get; private set; }

    /// <summary>Gets the host's settings the app changed, by their configuration's keys, which it gives the host as it starts.</summary>
    public IReadOnlyDictionary<string, string?> Host => _host.ToDictionary(setting => setting.Key, string? (setting) => setting.Value, StringComparer.OrdinalIgnoreCase);

    private AppPreferences(string path, string? language, string? theme, bool sidebarCollapsed, IReadOnlyDictionary<string, string>? host)
    {
        Path = path;
        Language = language;
        Theme = theme;
        IsSidebarCollapsed = sidebarCollapsed;
        _host = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (key, value) in host ?? new Dictionary<string, string>())
        {
            if (!string.IsNullOrWhiteSpace(key) && value is not null)
                _host[key] = value;
        }
    }

    /// <summary>Reads what the app remembers.</summary>
    /// <param name="path">The file.</param>
    /// <returns>What it remembers, or nothing when the file is missing or does not read.</returns>
    public static AppPreferences Load(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        try
        {
            var stored = JsonSerializer.Deserialize<Stored>(File.ReadAllText(path));

            return new AppPreferences(path, stored?.Language, stored?.Theme, stored?.SidebarCollapsed ?? false, stored?.Host);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return new AppPreferences(path, null, null, false, null);
        }
    }

    /// <summary>Remembers the language the app is in.</summary>
    /// <param name="language"><c>fr</c> or <c>en</c>.</param>
    public void RememberLanguage(string language)
    {
        Language = language;
        Save();
    }

    /// <summary>Remembers the theme the app is in.</summary>
    /// <param name="theme"><see cref="Dark"/> or <see cref="Light"/>.</param>
    public void RememberTheme(string theme)
    {
        Theme = theme;
        Save();
    }

    /// <summary>Remembers whether the sidebar is folded to its icons.</summary>
    /// <param name="collapsed">Whether it is.</param>
    public void RememberSidebar(bool collapsed)
    {
        IsSidebarCollapsed = collapsed;
        Save();
    }

    /// <summary>Remembers the host's settings, for its next start.</summary>
    /// <param name="settings">The settings by their configuration's keys; one with no value is forgotten, the host's default back.</param>
    public void RememberHost(IReadOnlyDictionary<string, string?> settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        foreach (var (key, value) in settings)
        {
            if (value is null)
                _host.Remove(key);
            else
                _host[key] = value;
        }

        Save();
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            File.WriteAllText(Path, JsonSerializer.Serialize(new Stored(Language, Theme, IsSidebarCollapsed ? true : null, _host.Count > 0 ? _host : null), _json));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Forgetting a preference does no harm: the app goes on, and the host keeps what it runs with.
        }
    }

    private sealed record Stored(string? Language, string? Theme, bool? SidebarCollapsed, IReadOnlyDictionary<string, string>? Host);
}
