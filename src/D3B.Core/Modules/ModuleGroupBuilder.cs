namespace D3B.Core.Modules;

/// <summary>Lists the modules of a group, in order, and lets whoever adds the group disable some of them.</summary>
public sealed class ModuleGroupBuilder
{
    private readonly List<IModule> _modules = [];
    private readonly HashSet<Type> _disabled = [];

    /// <summary>Gets the modules of the group that are not disabled, in the order they were listed.</summary>
    public IReadOnlyList<IModule> Modules => [.. _modules.Where(module => !_disabled.Contains(module.GetType()))];

    /// <summary>Lists a module, unless a module of the same type is already listed.</summary>
    /// <typeparam name="T">The module type.</typeparam>
    /// <returns>This builder.</returns>
    public ModuleGroupBuilder Add<T>()
        where T : IModule, new()
    {
        return Contains(typeof(T)) ? this : Add(new T());
    }

    /// <summary>Lists a module, unless a module of the same type is already listed.</summary>
    /// <param name="module">The module.</param>
    /// <returns>This builder.</returns>
    public ModuleGroupBuilder Add(IModule module)
    {
        ArgumentNullException.ThrowIfNull(module);

        if (!Contains(module.GetType()))
            _modules.Add(module);

        return this;
    }

    /// <summary>Disables a module of the group, so that adding the group does not add it.</summary>
    /// <typeparam name="T">The module type.</typeparam>
    /// <returns>This builder.</returns>
    /// <exception cref="InvalidOperationException">The group does not list a module of this type.</exception>
    public ModuleGroupBuilder Disable<T>()
        where T : IModule
    {
        // Disabling a module the group does not hold is a stale or mistyped configuration, never something to ignore.
        if (!Contains(typeof(T)))
            throw new InvalidOperationException($"The module {typeof(T).Name} is not in this group.");

        _disabled.Add(typeof(T));

        return this;
    }

    private bool Contains(Type type)
    {
        return _modules.Exists(module => module.GetType() == type);
    }
}
