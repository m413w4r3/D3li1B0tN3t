namespace D3B.Core.Modules;

/// <summary>An ordered set of modules added together, such as the default modules.</summary>
/// <remarks>
/// Whoever adds a group may disable some of its modules first, through <see cref="ModuleGroupBuilder.Disable{T}"/>. A
/// disabled module is still built when another module requires it.
/// </remarks>
public interface IModuleGroup
{
    /// <summary>Lists the modules of the group, in the order they are added.</summary>
    /// <param name="group">The group being listed.</param>
    void Build(ModuleGroupBuilder group);
}
