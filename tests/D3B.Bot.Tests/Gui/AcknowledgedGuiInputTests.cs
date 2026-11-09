using D3B.Bot.Gui;
using D3B.Gui.Input;
using D3B.Gui.Windowing;

namespace D3B.Bot.Tests.Gui;

public sealed class AcknowledgedGuiInputTests
{
    [Fact]
    public async Task DoesNotCallTheInnerInputWhenTheClickGateRefuses()
    {
        using GuiProtocolTrustTestFixture fixture = new();
        RecordingGuiInput inner = new();
        int gateCalls = 0;
        AcknowledgedGuiInput input = CreateInput(
            fixture,
            inner,
            (_, _, _, _) =>
            {
                gateCalls++;
                return Task.FromResult(false);
            },
            TestContext.Current.CancellationToken);

        GuiInputResult result = await input.ClickAsync(new DesktopPixels(100, 200), fixture.Window, CancellationToken.None);

        Assert.Equal(GuiInputStatus.WindowChanged, result.Status);
        Assert.False(result.ClickMayHaveBeenEmitted);
        Assert.Equal(1, gateCalls);
        Assert.Equal(0, inner.ClickCount);
    }

    [Fact]
    public async Task DelegatesExactlyOnceAfterTheClickGateAcknowledges()
    {
        using GuiProtocolTrustTestFixture fixture = new();
        RecordingGuiInput inner = new();
        Guid seenIntent = Guid.Empty;
        AcknowledgedGuiInput input = CreateInput(
            fixture,
            inner,
            (intentId, _, _, _) =>
            {
                seenIntent = intentId;
                return Task.FromResult(true);
            },
            TestContext.Current.CancellationToken);
        DesktopPixels point = new(120, 240);

        GuiInputResult result = await input.ClickAsync(point, fixture.Window, CancellationToken.None);

        Assert.Equal(GuiInputStatus.Sent, result.Status);
        Assert.Equal(fixture.Intent.IntentId, seenIntent);
        Assert.Equal(1, inner.ClickCount);
        Assert.Equal(point, inner.LastPoint);
        Assert.Same(fixture.Window, inner.LastWindow);
    }

    [Fact]
    public async Task RefusesASecondClickAttemptForTheSameIntent()
    {
        using GuiProtocolTrustTestFixture fixture = new();
        RecordingGuiInput inner = new();
        AcknowledgedGuiInput input = CreateInput(fixture, inner, (_, _, _, _) => Task.FromResult(true), TestContext.Current.CancellationToken);

        GuiInputResult first = await input.ClickAsync(new DesktopPixels(120, 240), fixture.Window, CancellationToken.None);
        GuiInputResult second = await input.ClickAsync(new DesktopPixels(120, 240), fixture.Window, CancellationToken.None);

        Assert.Equal(GuiInputStatus.Sent, first.Status);
        Assert.Equal(GuiInputStatus.Failed, second.Status);
        Assert.False(second.ClickMayHaveBeenEmitted);
        Assert.Equal(1, inner.ClickCount);
    }

    [Fact]
    public void DelegatesEnabledAndRealInputProperties()
    {
        using GuiProtocolTrustTestFixture fixture = new();
        RecordingGuiInput inner = new() { IsEnabled = false, EmitsRealInput = true };
        AcknowledgedGuiInput input = CreateInput(fixture, inner, (_, _, _, _) => Task.FromResult(true), TestContext.Current.CancellationToken);

        Assert.False(input.IsEnabled);
        Assert.True(input.EmitsRealInput);
    }

    [Fact]
    public async Task CallerCancellationBeforeTheInnerInputPreventsDelegation()
    {
        using GuiProtocolTrustTestFixture fixture = new();
        RecordingGuiInput inner = new();
        using CancellationTokenSource cancellation = new();
        AcknowledgedGuiInput input = CreateInput(
            fixture,
            inner,
            (_, _, _, _) =>
            {
                cancellation.Cancel();
                return Task.FromResult(true);
            },
            TestContext.Current.CancellationToken);

        GuiInputResult result = await input.ClickAsync(new DesktopPixels(120, 240), fixture.Window, cancellation.Token);

        Assert.Equal(GuiInputStatus.Canceled, result.Status);
        Assert.False(result.ClickMayHaveBeenEmitted);
        Assert.Equal(0, inner.ClickCount);
    }

    [Fact]
    public async Task FailsClosedWhenTheClickGateThrows()
    {
        using GuiProtocolTrustTestFixture fixture = new();
        RecordingGuiInput inner = new();
        AcknowledgedGuiInput input = CreateInput(
            fixture,
            inner,
            (_, _, _, _) => Task.FromException<bool>(new InvalidOperationException("synthetic actor failure")),
            TestContext.Current.CancellationToken);

        GuiInputResult result = await input.ClickAsync(new DesktopPixels(120, 240), fixture.Window, CancellationToken.None);

        Assert.Equal(GuiInputStatus.Failed, result.Status);
        Assert.False(result.ClickMayHaveBeenEmitted);
        Assert.Equal("The actor click acknowledgement failed.", result.Detail);
        Assert.Equal(0, inner.ClickCount);
    }

    private static AcknowledgedGuiInput CreateInput(
        GuiProtocolTrustTestFixture fixture,
        IGuiInput inner,
        Func<Guid, D3B.Gui.Safety.GuiWorldSnapshot, GameWindowSnapshot, CancellationToken, Task<bool>> gate,
        CancellationToken actorRevocationToken)
    {
        return new AcknowledgedGuiInput(
            fixture.Intent.IntentId,
            fixture.World,
            fixture.Window,
            gate,
            inner,
            actorRevocationToken);
    }
}
