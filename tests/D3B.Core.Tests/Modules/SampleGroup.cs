using D3B.Core.Modules;

namespace D3B.Core.Tests.Modules;

internal sealed class SampleGroup : IModuleGroup
{
    public void Build(ModuleGroupBuilder group)
    {
        group.Add<RequiredModule>().Add<CountingModule>();
    }
}
