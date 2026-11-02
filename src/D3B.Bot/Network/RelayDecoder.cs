using D3B.Network.Relay;
using D3B.Protocol.Translation;
using Google.Protobuf;
using Google.Protobuf.Reflection;
using Microsoft.Extensions.Logging;

namespace D3B.Bot.Network;

/// <summary>Watches every connection the relay takes, through the installed build's translation (D49, D51, D62).</summary>
/// <remarks>
/// Each connection phase gets its own <see cref="ConnectionPhaseSession"/>, which logs its frames by what they are and
/// rewrites the server selection's answer, so that the client's game connection comes to D3B. Each game connection gets
/// its own <see cref="GameSession"/>, which reads its frames for the <see cref="IGameObserver"/> instances and sends the
/// server D3B's requests.
/// </remarks>
internal sealed class RelayDecoder : IRelayInterceptor
{
    private const int MaxDepth = 3;

    private readonly ProtocolTranslation _translation;
    private readonly GameServerSelections _selections;
    private readonly GameAccounts _accounts;
    private readonly IReadOnlyList<IGameObserver> _observers;
    private readonly ILoggerFactory _logs;
    private readonly ILogger _log;

    public RelayDecoder(ProtocolTranslation translation, GameServerSelections selections, GameAccounts accounts, IEnumerable<IGameObserver> observers, ILoggerFactory logs)
    {
        _translation = translation;
        _selections = selections;
        _accounts = accounts;
        _observers = [.. observers];
        _logs = logs;
        _log = logs.CreateLogger<ConnectionPhaseSession>();
    }

    /// <summary>Names a message by the fields its oneofs hold, down from the root: what it is, without what it says.</summary>
    internal static string Describe(IMessage message)
    {
        var path = new List<string>();
        var current = message;

        while (current is not null && path.Count < MaxDepth)
        {
            var field = current.Descriptor.Oneofs
                .Where(oneof => !oneof.IsSynthetic)
                .Select(oneof => oneof.Accessor.GetCaseFieldDescriptor(current))
                .FirstOrDefault(field => field is not null);

            if (field is null)
                break;

            path.Add(field.Name);
            current = field.FieldType is FieldType.Message ? (IMessage)field.Accessor.GetValue(current) : null;
        }

        return path.Count is 0 ? message.Descriptor.Name : string.Join('.', path);
    }

    public IConnectionInterceptor? Intercept(IRelayConnection connection)
    {
        return connection.Route is RelayRoute.Connection
            ? new ConnectionPhaseSession(connection.Id, _translation, _selections, _accounts, _log)
            : GameSession.Start(connection, _translation, _accounts, _observers, _logs.CreateLogger<GameSession>());
    }
}
