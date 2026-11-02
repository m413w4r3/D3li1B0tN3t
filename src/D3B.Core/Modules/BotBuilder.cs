using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace D3B.Core.Modules;

/// <summary>Assembles a bot application from modules, on the .NET Generic Host, then builds it.</summary>
/// <remarks>
/// <para>
/// Modules register what they bring through <see cref="Services"/>, <see cref="Configuration"/> and
/// <see cref="Logging"/>, which are the Generic Host's own. The builder is an <see cref="IHostApplicationBuilder"/>,
/// so the extension methods written for the host apply to it.
/// </para>
/// <para>
/// Modules are added before <see cref="Build"/>, never after: the application's services are fixed once it exists.
/// </para>
/// </remarks>
public sealed class BotBuilder : IHostApplicationBuilder
{
    private readonly HostApplicationBuilder _host;
    private readonly HashSet<Type> _moduleTypes = [];
    private readonly List<IModule> _modules = [];
    private bool _built;

    /// <summary>Gets the modules built so far, each after the modules it requires.</summary>
    public IReadOnlyList<IModule> Modules => _modules;

    /// <inheritdoc/>
    public IServiceCollection Services => _host.Services;

    /// <inheritdoc/>
    public IConfigurationManager Configuration => _host.Configuration;

    /// <inheritdoc/>
    public IHostEnvironment Environment => _host.Environment;

    /// <inheritdoc/>
    public ILoggingBuilder Logging => _host.Logging;

    /// <inheritdoc/>
    public IMetricsBuilder Metrics => _host.Metrics;

    /// <inheritdoc/>
    public IDictionary<object, object> Properties => ((IHostApplicationBuilder)_host).Properties;

    /// <summary>Initializes a new instance of the <see cref="BotBuilder"/> class, with the Generic Host's defaults.</summary>
    /// <param name="args">The command-line arguments, read as configuration.</param>
    public BotBuilder(string[]? args)
        : this(new HostApplicationBuilderSettings { Args = args })
    {
    }

    /// <summary>Initializes a new instance of the <see cref="BotBuilder"/> class.</summary>
    /// <param name="settings">The settings of the underlying Generic Host.</param>
    public BotBuilder(HostApplicationBuilderSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        _host = Host.CreateApplicationBuilder(settings);
    }

    /// <summary>Adds a module and builds it, unless a module of the same type was already added.</summary>
    /// <typeparam name="T">The module type.</typeparam>
    /// <returns>This builder.</returns>
    /// <exception cref="InvalidOperationException">The application is already built.</exception>
    public BotBuilder AddModule<T>()
        where T : IModule, new()
    {
        return HasModule<T>() ? this : AddModule(new T());
    }

    /// <summary>Adds a module and builds it, unless a module of the same type was already added.</summary>
    /// <param name="module">The module.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="InvalidOperationException">The application is already built.</exception>
    public BotBuilder AddModule(IModule module)
    {
        ArgumentNullException.ThrowIfNull(module);
        EnsureBuilding();

        if (!_moduleTypes.Add(module.GetType()))
            return this;

        module.Build(this);
        _modules.Add(module);

        return this;
    }

    /// <summary>Adds the modules of a group, in order, skipping those of a type already added.</summary>
    /// <param name="group">The group.</param>
    /// <param name="configure">Changes the group before its modules are added, such as disabling some.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="InvalidOperationException">The application is already built.</exception>
    public BotBuilder AddModules(IModuleGroup group, Action<ModuleGroupBuilder>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(group);
        EnsureBuilding();

        var builder = new ModuleGroupBuilder();

        group.Build(builder);
        configure?.Invoke(builder);

        foreach (var module in builder.Modules)
            AddModule(module);

        return this;
    }

    /// <summary>Tells whether a module type was added.</summary>
    /// <typeparam name="T">The module type.</typeparam>
    /// <returns><see langword="true"/> when a module of the type was added.</returns>
    public bool HasModule<T>()
        where T : IModule
    {
        return _moduleTypes.Contains(typeof(T));
    }

    /// <inheritdoc/>
    public void ConfigureContainer<TContainerBuilder>(IServiceProviderFactory<TContainerBuilder> factory, Action<TContainerBuilder>? configure = null)
        where TContainerBuilder : notnull
    {
        _host.ConfigureContainer(factory, configure);
    }

    /// <summary>Builds the application.</summary>
    /// <returns>The application's host, not started yet.</returns>
    /// <exception cref="InvalidOperationException">The application is already built.</exception>
    public IHost Build()
    {
        EnsureBuilding();
        _built = true;

        return _host.Build();
    }

    private void EnsureBuilding()
    {
        if (_built)
            throw new InvalidOperationException("The application is already built.");
    }
}
