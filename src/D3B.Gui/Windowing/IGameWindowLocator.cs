namespace D3B.Gui.Windowing;

/// <summary>Inspects an explicitly chosen X11 window without selecting a window automatically.</summary>
public interface IGameWindowLocator
{
    /// <summary>Inspects the requested XID and checks its process against an executable expectation.</summary>
    /// <param name="xid">The numeric X11 window identifier selected by the caller.</param>
    /// <param name="expectation">The expected executable basename and identity policy.</param>
    /// <param name="cancellationToken">Cancels the inspection.</param>
    /// <returns>A snapshot containing measured state or a sanitized failure.</returns>
    Task<GameWindowSnapshot> LocateAsync(
        long xid,
        GameWindowExpectation expectation,
        CancellationToken cancellationToken);
}
