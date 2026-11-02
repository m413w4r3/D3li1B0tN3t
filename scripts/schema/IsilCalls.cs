using Cpp2IL.Core.ISIL;
using Cpp2IL.Core.Model.Contexts;
using D3B.Scripts.Unity;

namespace D3B.Scripts.Schema;

/// <summary>Reads the calls of a method's machine code, lifted by Cpp2IL to its instruction-set-independent language.</summary>
/// <remarks>
/// Only constants are followed: an immediate moved into a register, a register copied, an address computed from a known
/// register, a register cleared by xor. Anything else forgets the register, a call forgets those it may change, and an
/// instruction a jump lands on forgets them all, since another path may reach it with other values. What is read is
/// therefore what the register holds on every path, or nothing.
/// </remarks>
internal static class IsilCalls
{
    /// <summary>Lifts a method and lists its calls in the order of its code.</summary>
    /// <exception cref="InvalidDataException">The method could not be lifted, or a jump's target could not be read.</exception>
    public static List<IsilCall> Read(MethodAnalysisContext method, ClientPlatform platform)
    {
        method.Analyze();

        try
        {
            var instructions = method.ConvertedIsil ?? throw new InvalidDataException($"{method.FullName} could not be lifted.");
            var targets = JumpTargets(method, instructions);
            var arguments = CallRegisters.ArgumentRegisters(platform);
            var volatileRegisters = CallRegisters.VolatileRegisters(platform);
            var known = new Dictionary<string, long>(StringComparer.Ordinal);
            var calls = new List<IsilCall>();

            foreach (var instruction in instructions)
            {
                if (targets.Contains(instruction))
                    known.Clear();

                if (instruction.OpCode.Mnemonic is IsilMnemonic.Call or IsilMnemonic.CallNoReturn)
                {
                    calls.Add(new IsilCall
                    {
                        Target = instruction.Operands.Length > 0 && instruction.Operands[0].Data is IsilMethodOperand target ? target.Method : null,
                        Arguments = [.. arguments.Select(register => known.TryGetValue(register, out var value) ? value : (long?)null)],
                    });

                    foreach (var register in volatileRegisters)
                        known.Remove(register);

                    continue;
                }

                Follow(instruction, known);
            }

            return calls;
        }
        finally
        {
            method.ReleaseAnalysisData();
        }
    }

    private static HashSet<InstructionSetIndependentInstruction> JumpTargets(MethodAnalysisContext method, List<InstructionSetIndependentInstruction> instructions)
    {
        var targets = new HashSet<InstructionSetIndependentInstruction>(ReferenceEqualityComparer.Instance);

        foreach (var instruction in instructions)
        {
            if (instruction.FlowControl is not (IsilFlowControl.ConditionalJump or IsilFlowControl.UnconditionalJump or IsilFlowControl.IndexedJump))
                continue;

            if (instruction.Operands.Length is 0 || instruction.Operands[0].Data is not InstructionSetIndependentInstruction target)
                throw new InvalidDataException($"{method.FullName}: the target of jump {instruction} cannot be read.");

            targets.Add(target);
        }

        return targets;
    }

    private static void Follow(InstructionSetIndependentInstruction instruction, Dictionary<string, long> known)
    {
        if (instruction.Operands.Length is 0 || instruction.Operands[0].Data is not IsilRegisterOperand destination)
            return;

        var name = Normalize(destination.RegisterName);
        var source = instruction.Operands.Length > 1 ? instruction.Operands[1].Data : null;
        long? value = (instruction.OpCode.Mnemonic, source) switch
        {
            (IsilMnemonic.Move, IsilImmediateOperand immediate) => Convert.ToInt64(immediate.Value, System.Globalization.CultureInfo.InvariantCulture),
            (IsilMnemonic.Move, IsilRegisterOperand register) => Known(known, register),
            (IsilMnemonic.LoadAddress, IsilMemoryOperand { Index: null, Base: { Data: IsilRegisterOperand register } } memory) => Known(known, register) + memory.Addend,
            (IsilMnemonic.Xor, IsilRegisterOperand register) when Normalize(register.RegisterName) == name => 0,
            _ => null,
        };

        if (value is long constant)
            known[name] = constant;
        else
            known.Remove(name);
    }

    private static long? Known(Dictionary<string, long> known, IsilRegisterOperand register)
    {
        return known.TryGetValue(Normalize(register.RegisterName), out var value) ? value : null;
    }

    private static string Normalize(string register)
    {
        // A write to a 32-bit register clears the upper half; the arguments read here are bytes and 32-bit tags, so a
        // narrower name stands for its 64-bit register.
        return register switch
        {
            "eax" or "ax" or "al" => "rax",
            "ecx" or "cx" or "cl" => "rcx",
            "edx" or "dx" or "dl" => "rdx",
            "ebx" or "bx" or "bl" => "rbx",
            ['r', >= '0' and <= '9', .., 'd' or 'w' or 'b'] => register[..^1],
            _ => register,
        };
    }
}
