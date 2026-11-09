using System.Collections.Immutable;
using D3B.Bot.Gui;
using D3B.Bot.Movement;
using D3B.Bot.Runtime;
using D3B.Bot.Tests.Runtime;
using D3B.Data.Maps;
using D3B.Gui.Actions;
using D3B.Gui.Calibration;
using D3B.Gui.Input;
using D3B.Gui.Safety;
using D3B.Gui.Vision;
using D3B.Gui.Windowing;
using D3B.Gui.Tests.Calibration;

namespace D3B.Bot.Tests.Gui;

internal sealed class GuiWalkToCellTestContext : IAsyncDisposable
{
    public TestPlay Play { get; }

    public TestConnection Connection { get; }

    public BotActor Bot { get; }

    public GuiWorldSnapshotAdapter Adapter { get; }

    public GameWindowSnapshot Window { get; }

    public CalibrationResult Calibration { get; }

    public GuiOptions GuiOptions { get; }

    public GuiActionGate Gate { get; }

    public GuiActionExecutor Executor { get; }

    private GuiWalkToCellTestContext(
        TestPlay play,
        TestConnection connection,
        BotActor bot,
        GuiWorldSnapshotAdapter adapter,
        GameWindowSnapshot window,
        CalibrationResult calibration,
        GuiOptions guiOptions,
        GuiActionGate gate,
        GuiActionExecutor executor)
    {
        Play = play;
        Connection = connection;
        Bot = bot;
        Adapter = adapter;
        Window = window;
        Calibration = calibration;
        GuiOptions = guiOptions;
        Gate = gate;
        Executor = executor;
    }

    public static async Task<GuiWalkToCellTestContext> CreateAsync(int startingCell = 300)
    {
        TestPlay play = new();
        (TestConnection connection, BotActor bot) = await play.EnterAsync(startingCell, TestPlay.EastMapId);
        GuiWorldSnapshotAdapter adapter = new(play.Time);
        GameWindowIdentity identity = SyntheticCalibrationFixtures.CreateWindowIdentity();
        DateTimeOffset now = play.Time.GetUtcNow();
        GameWindowSnapshot window = CreateWindow(identity, now);
        ClientAreaGeometry geometry = CreateGeometry();
        CalibrationAnchorDocument document = SyntheticCalibrationFixtures.CreateDocument();
        CalibrationRequest calibrationRequest = SyntheticCalibrationFixtures.CreateRequest(
            play.Time,
            true,
            1,
            identity,
            800,
            600,
            geometry,
            1,
            TimeSpan.FromSeconds(20),
            TestPlay.EastMapId);
        CalibrationResult calibration = CalibrationValidator.Validate(document, calibrationRequest);
        GuiOptions guiOptions = new() { Enabled = true };
        GuiActionGate gate = new(guiOptions, play.Time);
        GuiActionExecutor executor = new(gate, guiOptions, play.Time);

        return new GuiWalkToCellTestContext(play, connection, bot, adapter, window, calibration, guiOptions, gate, executor);
    }

    public WalkToCellPlan CreatePlan(int targetCell)
    {
        GuiWorldSnapshot world = Adapter.Capture(Bot);
        HashSet<int> interactive = world.KnownElements
            .Where(element => element.Cell is not null)
            .Select(element => element.Cell!.Value.Value)
            .ToHashSet();
        MapData map = Play.Maps.Get(TestPlay.EastMapId);
        return WalkToCellPlanner.Plan(map.Cells.Span, world.CellId!.Value.Value, [targetCell], interactive);
    }

    public WalkToCellPlan CreatePlanIgnoringInteractiveCells(int targetCell)
    {
        MapData map = Play.Maps.Get(TestPlay.EastMapId);
        return WalkToCellPlanner.Plan(map.Cells.Span, Bot.State.Cell!.Value, [targetCell], new HashSet<int>());
    }

    public async Task<GuiActionIntent> RegisterActiveIntentAsync(int targetCell)
    {
        GuiWorldSnapshot world = Adapter.Capture(Bot);
        DateTimeOffset now = Play.Time.GetUtcNow();
        GuiActionIntent intent = new(
            Guid.NewGuid(),
            GuiActionKind.WalkToCell,
            world.SessionEpoch,
            world.StateRevision,
            TestPlay.EastMapId,
            new LogicalCell(targetCell),
            null,
            Window.Identity,
            Calibration.Profile!.CalibrationId,
            GuiClientAction.Walk,
            GuiServerOutcome.MovementConfirmed,
            now.AddSeconds(-1),
            now.AddSeconds(1));
        GuiIntentRegistration registration = await Bot.RegisterGuiIntentAsync(
            intent,
            world,
            Window,
            TestContext.Current.CancellationToken);
        Assert.Equal(GuiIntentRegistrationStatus.Registered, registration.Status);
        return intent;
    }

    public GuiWalkToCellTestDesktop CreateDesktop()
    {
        return new GuiWalkToCellTestDesktop(Window);
    }

    public GuiWalkToCellRequest CreateRequest(
        WalkToCellPlan plan,
        IGuiInput input,
        GuiWalkToCellTestDesktop? desktop = null,
        CalibrationResult? calibration = null,
        ImmutableArray<WindowLocalRect> uiExclusions = default,
        TimeProvider? timeProvider = null,
        GuiWorldSnapshotAdapter? adapter = null)
    {
        GuiWalkToCellTestDesktop actualDesktop = desktop ?? CreateDesktop();

        return new GuiWalkToCellRequest(
            Bot,
            adapter ?? Adapter,
            actualDesktop,
            actualDesktop,
            Window.Identity.Xid,
            new GameWindowExpectation("dofus", true),
            calibration ?? Calibration,
            GuiOptions,
            Gate,
            Executor,
            input,
            Play.Maps,
            plan,
            uiExclusions,
            4,
            timeProvider ?? Play.Time);
    }

    public ValueTask DisposeAsync()
    {
        return Play.DisposeAsync();
    }

    private static GameWindowSnapshot CreateWindow(GameWindowIdentity identity, DateTimeOffset now)
    {
        return new GameWindowSnapshot(
            identity,
            true,
            true,
            true,
            false,
            true,
            true,
            true,
            true,
            true,
            true,
            new DesktopPixels(90, 20),
            800,
            600,
            2,
            780,
            560,
            null,
            now);
    }

    private static ClientAreaGeometry CreateGeometry()
    {
        return new ClientAreaGeometry(new DesktopPixels(92, 22), new ClientAreaPixels(2, 2), 780, 560);
    }
}
