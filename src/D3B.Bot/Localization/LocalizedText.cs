using System.Globalization;
using System.Resources;

namespace D3B.Bot.Localization;

/// <summary>A text the app shows in its language, French or English (D14): the same in both, or one of a module's resources (D96).</summary>
/// <remarks>
/// A module's resources are its own <c>.resx</c> files, the neutral one in English and a <c>.fr.resx</c> in French, read
/// through a <see cref="ResourceManager"/> of its assembly.
/// </remarks>
public sealed class LocalizedText
{
    private readonly string? _text;
    private readonly ResourceManager? _resources;
    private readonly string _key;

    private LocalizedText(string? text, ResourceManager? resources, string key)
    {
        _text = text;
        _resources = resources;
        _key = key;
    }

    /// <summary>Gets a text that is the same in every language.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The text.</returns>
    public static LocalizedText Invariant(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        return new LocalizedText(text, null, text);
    }

    /// <summary>Gets a text from resources, in the language the app asks for.</summary>
    /// <param name="resources">The resources, such as a module's own.</param>
    /// <param name="key">The text's key in the resources.</param>
    /// <returns>The text.</returns>
    public static LocalizedText From(ResourceManager resources, string key)
    {
        ArgumentNullException.ThrowIfNull(resources);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return new LocalizedText(null, resources, key);
    }

    /// <summary>Gets a text of resources in a language, or its key between brackets when the resources lack it.</summary>
    /// <param name="resources">The resources.</param>
    /// <param name="key">The text's key.</param>
    /// <param name="culture">The language.</param>
    /// <returns>The text, or its key between brackets, as the app's own texts show a missing one.</returns>
    /// <remarks>
    /// Resources a module named wrong, such as under a folder its <c>.resx</c> moved to, throw for every text: the app reads
    /// them on its UI's thread, in the process that holds every client's connection (D91), where a text must not throw.
    /// </remarks>
    public static string Lookup(ResourceManager resources, string key, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(resources);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(culture);

        try
        {
            return resources.GetString(key, culture) ?? $"[{key}]";
        }
        catch (Exception exception) when (exception is MissingManifestResourceException or MissingSatelliteAssemblyException)
        {
            return $"[{key}]";
        }
    }

    /// <summary>Gets the text in a language.</summary>
    /// <param name="culture">The language.</param>
    /// <returns>The text, or its key between brackets when the resources lack it, or cannot be found.</returns>
    public string In(CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);

        return _text ?? Lookup(_resources!, _key, culture);
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return In(CultureInfo.CurrentUICulture);
    }
}
