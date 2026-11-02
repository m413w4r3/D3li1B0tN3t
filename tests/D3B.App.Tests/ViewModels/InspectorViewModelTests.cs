using System.Buffers;
using D3B.App.Controls;
using D3B.App.Localization;
using D3B.App.ViewModels;
using D3B.App.ViewModels.Inspector;
using D3B.Bot.Inspection;
using D3B.Bot.Movement;
using D3B.Bot.Tests.Movement;
using D3B.Bot.Tests.Network;
using D3B.Bot.Tests.Runtime;
using D3B.Data.Tests;
using D3B.Network.Captures;
using D3B.Network.Framing;
using D3B.Network.Relay;
using D3B.Protocol.Game;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace D3B.App.Tests.ViewModels;

public sealed class InspectorViewModelTests : IAsyncDisposable
{
    private readonly FrameRecorder _recorder = new(TimeProvider.System);
    private readonly TestFrameObserver _sentFrames = new();
    private readonly TestPlay _play;
    private readonly TemporaryDirectory _captures = new();
    private readonly IHost _host;
    private readonly Localizer _english = new(Localizer.English);

    public InspectorViewModelTests()
    {
        _play = new TestPlay([_recorder, _sentFrames]);

        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });

        builder.Services.AddSingleton(_play.Bots);
        builder.Services.AddSingleton(_recorder);
        builder.Services.AddSingleton(_play.Translation);
        builder.Services.AddSingleton(new RelaySettings
        {
            Address = RelaySettings.DefaultAddress,
            Ports = RelaySettings.DefaultPorts,
            GamePorts = RelaySettings.DefaultGamePorts,
            ClientDirectory = "no-client",
            RedirectDirectory = "no-redirect",
            CapturesDirectory = _captures.Path,
        });
        _host = builder.Build();
    }

    public async ValueTask DisposeAsync()
    {
        _host.Dispose();
        _captures.Dispose();
        await _play.DisposeAsync();
    }

    private static byte[] Framed(byte[] frame)
    {
        var writer = new ArrayBufferWriter<byte>();

        FrameWriter.Write(writer, frame);

        return writer.WrittenSpan.ToArray();
    }

    private void Capture(string version)
    {
        var header = new CaptureHeader { StartedAt = DateTimeOffset.UnixEpoch, ClientVersion = version, Listener = string.Empty, ServerHost = string.Empty, Server = string.Empty };
        var (writer, _) = CaptureWriter.Create(_captures.Combine("game"), "20261029-031619-0HNPLDBVDE8EE", header, TimeProvider.System);

        using (writer)
        {
            writer.Write(CaptureRecordKind.FromClient, new ReadOnlySequence<byte>(Framed(TestGame.Request(new AuthenticationRequest { Ticket = "0123456789abcdef0123456789abcdef", Language = "fr" }))));
            writer.Write(CaptureRecordKind.FromServer, new ReadOnlySequence<byte>(Framed(TestGame.Event(new MapCurrentEvent { MapId = TestPlay.MapId }))));
        }
    }

    private async Task<InspectorViewModel> WalkedAsync()
    {
        var (connection, bot) = await _play.EnterAsync(433);
        var walk = bot.WalkAsync(388, TestContext.Current.CancellationToken);

        await TestWalks.WalkedAsync(_play, connection, 1, afterRequestRecorded: () => _sentFrames.SentAsync(nameof(MapMovementRequest)));
        await walk;

        var page = new InspectorViewModel(_english, new BotChoice());

        page.Refresh(_host.Services);

        return page;
    }

    [Fact]
    public async Task ABotsFramesShowAsTheyWentWhoSentThemWhatTheyCarryAndHowFarTheyWereRead()
    {
        var page = await WalkedAsync();

        Assert.Equal("Character", Assert.Single(page.Bots).Name);
        Assert.True(page.IsLive);
        Assert.Equal(8, page.Frames.Count);
        Assert.Equal(("Client", Tone.Neutral, nameof(AuthenticationRequest)), (page.Frames[0].Sender, page.Frames[0].SenderTone, page.Frames[0].Name));

        var sent = page.Frames[4];

        Assert.Equal(("D3B", Tone.Success, nameof(MapMovementRequest), "understood"), (sent.Sender, sent.SenderTone, sent.Name, sent.Reading));
        Assert.EndsWith(" B", sent.Size, StringComparison.Ordinal);
        Assert.Equal("8 frames", page.Summary);
    }

    [Fact]
    public async Task UnfoldedTheTicketsRequestShowsTheTicketHidden()
    {
        var page = await WalkedAsync();
        var authentication = page.Frames[0];

        authentication.ToggleCommand.Execute(null);

        Assert.True(authentication.IsExpanded);
        Assert.False(page.Follow);
        Assert.Equal(("ticket", "•••••••• hidden", true), (authentication.Fields[0].Name, authentication.Fields[0].Value, authentication.Fields[0].IsHidden));
        Assert.DoesNotContain(authentication.Fields, field => field.Value?.Contains("ticket-of", StringComparison.Ordinal) ?? false);
    }

    [Fact]
    public async Task TheSendersAndTheNameSoughtFilterTheFrames()
    {
        var page = await WalkedAsync();

        page.ShowsServer = false;

        Assert.Equal([nameof(AuthenticationRequest), nameof(MapMovementRequest), nameof(MapMovementConfirmRequest)], page.Frames.Select(frame => frame.Name));

        page.ShowsServer = true;
        page.Search = "movement";

        Assert.Equal(4, page.Frames.Count);
        Assert.Equal("4 frames shown of 8", page.Summary);
    }

    [Fact]
    public async Task FrozenNewFramesWaitUntilItThaws()
    {
        var page = await WalkedAsync();

        page.IsFrozen = true;
        _play.Connect(TestPlay.Account).Server(TestPlay.Chosen());
        page.Refresh(_host.Services);

        Assert.Equal(8, page.Frames.Count);

        page.IsFrozen = false;
        page.Refresh(_host.Services);

        Assert.Equal(10, page.Frames.Count);
    }

    [Fact]
    public async Task ACaptureOfTheInstalledBuildReplaysAndOneOfAnotherSaysWhy()
    {
        var page = await WalkedAsync();

        Capture(_play.Translation.ClientVersion);
        page.ReadCaptures();
        page.Capture = Assert.Single(page.Captures);

        Assert.False(page.IsLive);
        Assert.Null(page.CaptureProblem);
        Assert.Equal([nameof(AuthenticationRequest), nameof(MapCurrentEvent)], page.Frames.Select(frame => frame.Name));

        File.Delete(page.Capture.Path);
        Capture("3.0.0.0");
        page.Capture = null;
        page.Capture = new CaptureChoiceViewModel(Directory.GetFiles(_captures.Combine("game"))[0], DateTime.Now, "capture");

        Assert.Empty(page.Frames);
        Assert.Contains("3.0.0.0", page.CaptureProblem, StringComparison.Ordinal);
    }
}
