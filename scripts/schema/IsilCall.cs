using Cpp2IL.Core.Model.Contexts;

namespace D3B.Scripts.Schema;

/// <summary>A call in a method's lifted code, with the constants its argument registers held.</summary>
internal sealed class IsilCall
{
    /// <summary>The method called, when Cpp2IL resolved it; a call to shared generic code stays unresolved.</summary>
    public required MethodAnalysisContext? Target { get; init; }

    /// <summary>The constants held in the platform's argument registers in call order, or null.</summary>
    public required IReadOnlyList<long?> Arguments { get; init; }

    /// <summary>Tells whether the call is to a method of a type, by the type's full name.</summary>
    public bool Calls(string typeName)
    {
        return Target?.DeclaringType?.FullName == typeName;
    }
}
