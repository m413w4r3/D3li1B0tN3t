using System.Buffers;
using System.Globalization;
using D3B.Network.Relay;
using D3B.Protocol.Connection;
using D3B.Protocol.Translation;
using Google.Protobuf;
using Microsoft.Extensions.Logging;

namespace D3B.Bot.Network;

/// <summary>One connection phase: each frame decoded and logged by what it is, and the server selection's answer rewritten (D49, 3f).</summary>
/// <remarks>
/// <para>
/// A frame is logged by the readable fields its oneofs hold, such as <c>request.identification</c>, and its length: never
/// by what it holds, since the connection phase carries the account's login token and ticket. A frame not understood
/// whole is a warning, and the relay forwards it as it is.
/// </para>
/// <para>
/// Once the client has asked for a server, the relay holds the server's frames whole, until its answer. A success
/// understood whole (D47) is rewritten: its host becomes D3B's address, and its ports the game listener's, the real game
/// server being kept in <see cref="GameServerSelections"/> and the account the identification named in
/// <see cref="GameAccounts"/>, both by the answer's ticket. Any other answer, or one the relay could not hold whole, goes on
/// as it came: the game connection then goes straight to the game server, as before D3B took it.
/// </para>
/// </remarks>
internal sealed class ConnectionPhaseSession : IConnectionInterceptor
{
    private readonly string _connectionId;
    private readonly ProtocolTranslation _translation;
    private readonly GameServerSelections _selections;
    private readonly GameAccounts _accounts;
    private readonly ILogger _log;

    // Set from the client's read loop, read from the server's: the relay shows the request before sending it on.
    private volatile bool _selecting;

    // Read and written from the server's read loop only: the identification's answer comes before the selection's.
    private GameAccount? _account;

    public ConnectionPhaseSession(string connectionId, ProtocolTranslation translation, GameServerSelections selections, GameAccounts accounts, ILogger log)
    {
        _connectionId = connectionId;
        _translation = translation;
        _selections = selections;
        _accounts = accounts;
        _log = log;
    }

    private static string SideOf(RelayDirection direction)
    {
        return direction is RelayDirection.FromClient ? "client" : "server";
    }

    private static DateTimeOffset? SubscriptionEnd(string text)
    {
        // An ISO 8601 date with its offset on every identification captured, of two subscribed accounts (D72): any other
        // text is no date, and the account counts as not subscribed.
        return DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var end) ? end : null;
    }

    public bool Holds(RelayDirection direction)
    {
        return direction is RelayDirection.FromServer && _selecting;
    }

    public byte[]? OnFrame(RelayDirection direction, ReadOnlySequence<byte> frame, bool held)
    {
        var decoded = _translation.DecodeConnection(frame);

        Log(direction, frame.Length, decoded);

        // Only a frame understood whole is rewritten: one that is not would lose what D3B does not read (D47).
        if (!decoded.IsUnderstood)
            return null;

        if (direction is RelayDirection.FromClient)
        {
            if (decoded.Message.Request?.ContentCase is ConnectionRequest.ContentOneofCase.ServerSelection)
                _selecting = true;

            return null;
        }

        if (decoded.Message.Response?.Identification?.Success is { } identified)
            _account = new GameAccount(identified.AccountId, identified.AccountNickname, identified.AccountTag, SubscriptionEnd(identified.SubscriptionEnd));

        if (decoded.Message.Response?.ServerSelection is not { } answer)
            return null;

        _selecting = false;

        if (answer.Success is not { } success)
            return null;

        if (!held)
        {
            NetworkLog.SelectionNotRewritten(_log, _connectionId, "the relay could not hold it whole");

            return null;
        }

        if (success.Ports.Count is 0)
        {
            NetworkLog.SelectionNotRewritten(_log, _connectionId, "it names no port");

            return null;
        }

        var ports = _selections.Add(success.Ticket, success.Host, [.. success.Ports]);

        if (ports.Count is 0)
        {
            NetworkLog.SelectionNotRewritten(_log, _connectionId, "D3B has no game listener");

            return null;
        }

        if (_account is not null)
            _accounts.Remember(success.Ticket, _account);

        var listened = string.Join(", ", ports);

        success.Host = _selections.Address.ToString();
        success.Ports.Clear();
        success.Ports.Add(ports);
        NetworkLog.SelectionRewritten(_log, _connectionId, success.Host, listened);

        return decoded.Message.ToByteArray();
    }

    public void OnHoldAbandoned(RelayDirection direction, string reason)
    {
        _selecting = false;
        NetworkLog.SelectionNotRewritten(_log, _connectionId, $"the relay held the {SideOf(direction)}'s frame, but {reason}");
    }

    private void Log(RelayDirection direction, long length, DecodedFrame<ConnectionMessage> decoded)
    {
        if (!_log.IsEnabled(decoded.IsUnderstood ? LogLevel.Information : LogLevel.Warning))
            return;

        var side = SideOf(direction);
        var message = decoded.Message is null ? "no message" : RelayDecoder.Describe(decoded.Message);

        if (decoded.IsUnderstood)
            NetworkLog.Decoded(_log, _connectionId, side, message, length);
        else
            NetworkLog.NotUnderstood(_log, _connectionId, side, message, length, decoded.Problem);
    }
}
