using D3B.Gui.Windowing;

namespace D3B.Gui.Tests.Capture;

internal sealed class SequenceGameWindowLocator : IGameWindowLocator
{
    private readonly GameWindowSnapshot[] _snapshots;
    private int _index;

    public SequenceGameWindowLocator(params GameWindowSnapshot[] snapshots)
    {
        ArgumentNullException.ThrowIfNull(snapshots);

        if (snapshots.Length == 0)
        {
            throw new ArgumentException("At least one window snapshot is required.", nameof(snapshots));
        }

        _snapshots = snapshots;
    }

    public Task<GameWindowSnapshot> LocateAsync(long xid, GameWindowExpectation expectation, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        int index = Interlocked.Increment(ref _index) - 1;
        return Task.FromResult(_snapshots[Math.Min(index, _snapshots.Length - 1)]);
    }
}
