using D3B.Bot.Localization;
using Microsoft.Extensions.DependencyInjection;

namespace D3B.Bot.Pages;

/// <summary>Registers the pages a module brings, for the app to show (D96).</summary>
public static class PageServiceCollectionExtensions
{
    /// <summary>Registers a page in the host's <see cref="PageCatalog"/>.</summary>
    /// <param name="services">The host's services.</param>
    /// <param name="id">The page's id, unique in the host.</param>
    /// <param name="title">The page's title, in the app's rail and as the page names itself.</param>
    /// <param name="icon">The page's icon in the rail: lines on a grid of 24 units, in the path syntax of SVG, or <see langword="null"/> for the app's own.</param>
    /// <param name="create">Makes the page, an Avalonia control, once, on the UI's thread, the first time it shows.</param>
    /// <returns>The services.</returns>
    /// <exception cref="InvalidOperationException">A page with the id is already registered.</exception>
    /// <remarks>
    /// The page's types are named only in <paramref name="create"/>, never in the module's own code, so that a host without
    /// the app, which lacks the app's UI framework, builds the module and never loads them (D96).
    /// </remarks>
    public static IServiceCollection AddPage(this IServiceCollection services, string id, LocalizedText title, string? icon, Func<PageContext, object> create)
    {
        ArgumentNullException.ThrowIfNull(services);

        var definition = new PageDefinition(id, title, icon, create);

        // While the modules build, so that a module reusing an id stops the host from starting, as for activities (D93).
        if (services.Any(descriptor => descriptor.ImplementationInstance is PageDefinition registered && registered.Id == id))
            throw new InvalidOperationException($"A page with the id {id} is already registered.");

        services.AddSingleton(definition);

        return services;
    }
}
