using D3B.Scripts.Unity;

namespace D3B.Scripts.Schema;

/// <summary>The x64 argument and caller-saved registers used by a client's platform.</summary>
internal static class CallRegisters
{
    private static readonly IReadOnlyList<string> _windowsArguments = Array.AsReadOnly(["rcx", "rdx", "r8", "r9"]);
    private static readonly IReadOnlyList<string> _windowsVolatile = Array.AsReadOnly(["rax", "rcx", "rdx", "r8", "r9", "r10", "r11"]);
    private static readonly IReadOnlyList<string> _linuxArguments = Array.AsReadOnly(["rdi", "rsi", "rdx", "rcx", "r8", "r9"]);
    private static readonly IReadOnlyList<string> _linuxVolatile = Array.AsReadOnly(["rax", "rcx", "rdx", "rsi", "rdi", "r8", "r9", "r10", "r11"]);

    /// <summary>The argument registers in call order.</summary>
    public static IReadOnlyList<string> ArgumentRegisters(ClientPlatform platform)
    {
        return platform switch
        {
            ClientPlatform.Windows => _windowsArguments,
            ClientPlatform.Linux => _linuxArguments,
            _ => throw new ArgumentOutOfRangeException(nameof(platform), platform, null)
        };
    }

    /// <summary>The caller-saved registers a call may change.</summary>
    public static IReadOnlyList<string> VolatileRegisters(ClientPlatform platform)
    {
        return platform switch
        {
            ClientPlatform.Windows => _windowsVolatile,
            ClientPlatform.Linux => _linuxVolatile,
            _ => throw new ArgumentOutOfRangeException(nameof(platform), platform, null)
        };
    }
}
