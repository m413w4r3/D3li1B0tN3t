using D3B.Scripts.Schema;
using D3B.Scripts.Unity;

namespace D3B.Scripts.Tests.Schema;

public sealed class CallRegistersTests
{
    [Fact]
    public void WindowsUsesMicrosoftX64Registers()
    {
        Assert.Equal(["rcx", "rdx", "r8", "r9"], CallRegisters.ArgumentRegisters(ClientPlatform.Windows));
        Assert.Equal(["rax", "rcx", "rdx", "r8", "r9", "r10", "r11"], CallRegisters.VolatileRegisters(ClientPlatform.Windows));
    }

    [Fact]
    public void LinuxUsesSysVX64Registers()
    {
        Assert.Equal(["rdi", "rsi", "rdx", "rcx", "r8", "r9"], CallRegisters.ArgumentRegisters(ClientPlatform.Linux));
        Assert.Equal(["rax", "rcx", "rdx", "rsi", "rdi", "r8", "r9", "r10", "r11"], CallRegisters.VolatileRegisters(ClientPlatform.Linux));
    }
}
