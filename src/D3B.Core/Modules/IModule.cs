namespace D3B.Core.Modules;

/// <summary>A feature that registers itself into a bot application: services, handlers, activities, settings.</summary>
/// <remarks>
/// <para>
/// A module is built once per application, when it is added. Adding a module type twice builds it once.
/// </para>
/// <para>
/// A module requires another by adding it at the start of <see cref="Build"/>, with
/// <see cref="BotBuilder.AddModule{T}()"/>: the required module is then built before the rest of the module. A module
/// counts as added before its own <see cref="Build"/> runs, so a requirement that leads back to it does nothing.
/// </para>
/// </remarks>
public interface IModule
{
    /// <summary>Registers the module into the application.</summary>
    /// <param name="bot">The application being built.</param>
    void Build(BotBuilder bot);
}
