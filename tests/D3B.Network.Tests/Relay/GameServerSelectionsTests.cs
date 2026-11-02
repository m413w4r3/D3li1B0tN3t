using D3B.Network.Relay;
using Microsoft.Extensions.Time.Testing;

namespace D3B.Network.Tests.Relay;

public sealed class GameServerSelectionsTests
{
    private const string Ticket = "0123456789abcdef0123456789abcdef";

    private readonly FakeTimeProvider _time = new();

    private static RelaySettings Settings(params int[] gamePorts)
    {
        return new RelaySettings
        {
            Address = RelaySettings.DefaultAddress,
            Ports = RelaySettings.DefaultPorts,
            GamePorts = gamePorts,
            ClientDirectory = "no-client",
            RedirectDirectory = "no-redirect",
        };
    }

    [Fact]
    public void TheAnswerListsAGamePortForEachRealPortAsFarAsTheListenerHasThem()
    {
        var selections = new GameServerSelections(Settings(5556), _time);

        Assert.Equal([5556], selections.Add(Ticket, "game.test", [5555, 443]));
        Assert.Equal(RelaySettings.DefaultAddress, selections.Address);
    }

    [Fact]
    public void AGameConnectionIsMatchedByTheTicketItsFirstFramePresents()
    {
        var selections = new GameServerSelections(Settings(5556, 5557), _time);

        selections.Add("ffffffffffffffffffffffffffffffff", "other.test", [5555]);
        selections.Add(Ticket, "game.test", [5555, 443]);

        Assert.True(selections.TryTake(TestFrames.Presenting(Ticket), 5557, out var host, out var port));
        Assert.Equal("game.test", host);
        Assert.Equal(443, port);
    }

    [Fact]
    public void ASelectionIsTakenOnce()
    {
        var selections = new GameServerSelections(Settings(5556), _time);

        selections.Add(Ticket, "game.test", [5555]);

        Assert.True(selections.TryTake(TestFrames.Presenting(Ticket), 5556, out _, out _));
        Assert.False(selections.TryTake(TestFrames.Presenting(Ticket), 5556, out _, out _));
    }

    [Fact]
    public void AFrameWithoutAWaitingTicketMatchesNothing()
    {
        var selections = new GameServerSelections(Settings(5556), _time);

        selections.Add(Ticket, "game.test", [5555]);

        Assert.False(selections.TryTake(TestFrames.Presenting("ffffffffffffffffffffffffffffffff"), 5556, out var host, out _));
        Assert.Null(host);
        Assert.False(selections.TryTake([0x0A, 0x7F], 5556, out _, out _));
    }

    [Fact]
    public void ASelectionNoConnectionCameForIsForgotten()
    {
        var selections = new GameServerSelections(Settings(5556), _time);

        selections.Add(Ticket, "game.test", [5555]);
        _time.Advance(TimeSpan.FromSeconds(61));

        Assert.False(selections.TryTake(TestFrames.Presenting(Ticket), 5556, out _, out _));
    }

    [Fact]
    public void ASelectionWithoutATicketIsNotRewritten()
    {
        var selections = new GameServerSelections(Settings(5556), _time);

        Assert.Empty(selections.Add(string.Empty, "game.test", [5555]));
    }

    [Fact]
    public void ThePortsTheListenerTookAreTheOnesListed()
    {
        var selections = new GameServerSelections(Settings(0), _time);

        selections.OnListening([61000]);

        Assert.Equal([61000], selections.Add(Ticket, "game.test", [5555, 443]));
    }
}
