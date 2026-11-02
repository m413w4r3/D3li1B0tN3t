using D3B.Bot.Tests.Runtime;
using D3B.Engine.Maps;
using D3B.Protocol.Game;
using Google.Protobuf;
using Microsoft.Extensions.Time.Testing;

namespace D3B.Bot.Tests.Movement;

/// <summary>What the server and the client answer a walk D3B sent, as a test plays them.</summary>
internal static class TestWalks
{
    /// <summary>Waits for D3B's n-th request, a walk, then shows it walked and confirmed; gives the cell it ends on.</summary>
    /// <param name="play">The test's play.</param>
    /// <param name="connection">The connection D3B sends through.</param>
    /// <param name="count">The walk's number among D3B's requests.</param>
    /// <param name="beforeResponse">What the server sends between the client's confirmation and its response, such as the map an action cell leads to.</param>
    /// <param name="afterRequestRecorded">A test signal to await before replying to D3B's request.</param>
    public static async Task<int> WalkedAsync(TestPlay play, TestConnection connection, int count, IMessage? beforeResponse = null, Func<Task>? afterRequestRecorded = null)
    {
        var request = Assert.IsType<MapMovementRequest>(await SentAsync(play, connection, count));

        if (afterRequestRecorded is not null)
            await afterRequestRecorded();

        var start = request.KeyMovements[0] & 0xFFF;
        var end = request.KeyMovements[^1] & 0xFFF;

        Assert.True(CellPathfinder.TryFind(play.Maps.Get(request.MapId).Cells.Span, start, end, out var path));

        connection.Server(new MapMovementEvent { ActorId = TestPlay.CharacterId, Cells = { path.Cells.ToArray() } });
        connection.Client(new MapMovementConfirmRequest());

        if (beforeResponse is not null)
            connection.Server(beforeResponse);

        connection.Response(new MapMovementConfirmResponse());

        return end;
    }

    /// <summary>Waits until D3B sent a number of requests, the clock moving meanwhile, as after a map change; gives the last.</summary>
    public static async Task<IMessage> SentAsync(TestPlay play, TestConnection connection, int count)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);

        while (connection.Sent.Count < count)
        {
            Assert.True(DateTime.UtcNow < deadline, $"D3B sent {connection.Sent.Count} requests, not {count}.");
            play.Time.Advance(TimeSpan.FromMilliseconds(100));
            await Task.Delay(5, TestContext.Current.CancellationToken);
        }

        return connection.Sent[count - 1];
    }

    /// <summary>The server names a map, the client asks what is on it, and the server places the character on a cell.</summary>
    public static void Arrive(TestConnection connection, long mapId, int cell)
    {
        connection.Server(new MapCurrentEvent { MapId = mapId });
        connection.Client(new MapInformationRequest { MapId = mapId });
        connection.Server(TestPlay.Information(cell, mapId));
    }

    /// <summary>Moves the clock until a behavior is over, whenever each of its delays started.</summary>
    public static async Task<T> AdvanceUntilDoneAsync<T>(FakeTimeProvider time, Task<T> task)
    {
        while (!task.IsCompleted)
        {
            time.Advance(TimeSpan.FromMilliseconds(250));
            await Task.Delay(5, TestContext.Current.CancellationToken);
        }

        return await task;
    }
}
