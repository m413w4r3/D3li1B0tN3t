namespace D3B.Scripts.Preflight;

internal sealed class PreflightBlockedException : Exception
{
    public string Code { get; }

    public PreflightBlockedException(string code)
        : base(code)
    {
        Code = code;
    }
}
