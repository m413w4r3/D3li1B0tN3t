namespace D3B.Scripts.Preflight;

internal sealed class PreflightBlockedException : Exception
{
    public string Code { get; }

    public PreflightBlockedDetail? Detail { get; }

    public PreflightBlockedException(string code)
        : base(code)
    {
        Code = code;
    }

    public PreflightBlockedException(string code, Exception innerException)
        : base(code, innerException)
    {
        Code = code;
    }

    public PreflightBlockedException(string code, PreflightBlockedDetail detail)
        : base(code)
    {
        Code = code;
        Detail = detail;
    }
}
