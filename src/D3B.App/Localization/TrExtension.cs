using Avalonia.Data;
using Avalonia.Markup.Xaml;

namespace D3B.App.Localization;

/// <summary>A text of the app in a view, which follows a change of language: <c>{l:Tr Key}</c>.</summary>
internal sealed class TrExtension : MarkupExtension
{
    /// <summary>Gets or sets the text's key in the resources.</summary>
    public string Key { get; set; }

    /// <summary>Initializes a new instance of the <see cref="TrExtension"/> class.</summary>
    /// <param name="key">The text's key in the resources.</param>
    public TrExtension(string key)
    {
        Key = key;
    }

    /// <inheritdoc/>
    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        return new ReflectionBinding($"[{Key}]") { Source = Localizer.Instance, Mode = BindingMode.OneWay };
    }
}
