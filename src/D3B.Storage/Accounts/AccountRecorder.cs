using System.Threading.Channels;
using D3B.Bot.Network;
using D3B.Network.Relay;
using D3B.Protocol.Game;
using D3B.Protocol.Translation;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace D3B.Storage.Accounts;

/// <summary>Records the accounts and characters the game connections show: the characters their server lists, the one their client chooses (D99).</summary>
/// <remarks>
/// It sees each frame on a relay's read loop, which must not wait: what it records is queued, and written one at a time
/// by its own loop. A write that fails is logged, and the relay goes on.
/// </remarks>
internal sealed class AccountRecorder : BackgroundService, IGameObserver
{
    private readonly AccountBook _accounts;
    private readonly TimeProvider _time;
    private readonly ILogger<AccountRecorder> _log;
    private readonly Channel<Func<CancellationToken, Task>> _writes = Channel.CreateUnbounded<Func<CancellationToken, Task>>(new UnboundedChannelOptions { SingleReader = true });

    public AccountRecorder(AccountBook accounts, TimeProvider time, ILogger<AccountRecorder> log)
    {
        _accounts = accounts;
        _time = time;
        _log = log;
    }

    private static int? LevelOf(Character character)
    {
        return character.Basics is { HasLevel: true } basics ? basics.Level : null;
    }

    public void OnSessionStarted(GameSession session)
    {
    }

    public void OnFrame(GameSession session, RelayDirection direction, DecodedGameFrame frame)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(frame);

        // The account is the one the connection phase identified by the ticket (D65): without it, nothing is recorded.
        if (direction is not RelayDirection.FromServer || !frame.IsRead || session.Account is not { } account)
            return;

        // Dated as seen, not as written: the queue may write it later.
        var seen = _time.GetUtcNow();

        switch (frame.Content)
        {
            case CharacterListEvent list:
                {
                    List<(long Id, string Name, int? Level)> characters = [.. list.Characters.Select(character => (character.Id, character.Basics?.Name ?? "", LevelOf(character)))];

                    _writes.Writer.TryWrite(cancellationToken => _accounts.SeeAsync(account, characters, seen, cancellationToken));

                    break;
                }

            case CharacterSelectionEvent { Success.Character: { } character }:
                _writes.Writer.TryWrite(cancellationToken => _accounts.PlayAsync(account, character.Id, character.Basics?.Name ?? "", LevelOf(character), seen, cancellationToken));

                break;
        }
    }

    /// <summary>Waits until everything queued so far is written.</summary>
    /// <returns>A task that completes once written, or failed and logged.</returns>
    internal async Task WrittenAsync()
    {
        var written = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        _writes.Writer.TryWrite(_ =>
        {
            written.SetResult();

            return Task.CompletedTask;
        });

        await written.Task.ConfigureAwait(false);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var write in _writes.Reader.ReadAllAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                await write(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                StorageLog.RecordFailed(_log, exception);
            }
        }
    }
}
