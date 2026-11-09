using D3B.Gui.Capture;
using D3B.Gui.Windowing;
using OpenCvSharp;

namespace D3B.Bot.Tests.Gui;

internal sealed class GuiWalkToCellTestDesktop : IGameWindowLocator, IWindowCapture
{
    public GameWindowSnapshot Window { get; set; }

    public int LocateCount { get; private set; }

    public int CaptureCount { get; private set; }

    public Func<Task>? AfterCapture { get; set; }

    public Func<int, GameWindowSnapshot>? WindowForLocate { get; set; }

    public CaptureStatus CaptureStatus { get; set; } = CaptureStatus.Captured;

    public bool ContentUsable { get; set; } = true;

    public GuiWalkToCellTestDesktop(GameWindowSnapshot window)
    {
        ArgumentNullException.ThrowIfNull(window);
        Window = window;
    }

    public Task<GameWindowSnapshot> LocateAsync(long xid, GameWindowExpectation expectation, CancellationToken cancellationToken)
    {
        LocateCount++;
        return Task.FromResult(WindowForLocate?.Invoke(LocateCount) ?? Window);
    }

    public async Task<CapturedFrame> CaptureAsync(long xid, GameWindowExpectation expectation, CancellationToken cancellationToken)
    {
        CaptureCount++;
        GameWindowSnapshot before = Window;
        if (AfterCapture is not null)
            await AfterCapture();

        GameWindowSnapshot after = Window;
        Mat image = new(after.WindowHeight, after.WindowWidth, MatType.CV_8UC3, Scalar.All(25));
        return new CapturedFrame(
            Guid.NewGuid(),
            after.Identity,
            before.CapturedAt,
            after.CapturedAt,
            before,
            after,
            image.Cols,
            image.Rows,
            image,
            CaptureStatus,
            CaptureStatus == CaptureStatus.Captured ? null : "Synthetic capture failure.",
            ContentUsable);
    }
}
