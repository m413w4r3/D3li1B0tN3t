using D3B.Bot.Gui;
using D3B.Gui.Actions;
using D3B.Gui.Windowing;

namespace D3B.Bot.Tests.Gui;

public sealed class GuiIntentCorrelationTests
{
    [Fact]
    public void MatchingClientAndServerEvidenceFollowsTheRequiredSequence()
    {
        GuiIntentCorrelationFixture fixture = new();
        GuiIntentCorrelation correlation = new(fixture.Time);

        GuiIntentRegistration registration = correlation.Register(fixture.Intent, fixture.World, fixture.Window);
        Assert.Equal(GuiIntentRegistrationStatus.Registered, registration.Status);
        Assert.Equal(GuiIntentCorrelationState.Registered, correlation.State);

        Assert.True(correlation.Acknowledge(fixture.World, fixture.Window));
        Assert.Equal(GuiIntentCorrelationState.Acknowledged, correlation.State);

        Assert.True(correlation.MarkClicked(fixture.World, fixture.Window));
        Assert.Equal(GuiIntentCorrelationState.Clicked, correlation.State);
        Assert.Equal(1, correlation.ClickCount);

        Assert.True(correlation.ObserveClientRequest(fixture.MatchingRequest()));
        Assert.Equal(GuiIntentCorrelationState.ClientRequestSeen, correlation.State);

        Assert.True(correlation.ObserveServerOutcome(fixture.MatchingOutcome()));
        Assert.Equal(GuiIntentCorrelationState.ServerConfirmed, correlation.State);

        Assert.True(correlation.Consume());
        Assert.Equal(GuiIntentCorrelationState.Consumed, correlation.State);
        Assert.False(correlation.MarkClicked(fixture.World, fixture.Window));
    }

    [Fact]
    public void UnexpectedClientRequestInterruptsAndGivesThePlayerPriority()
    {
        GuiIntentCorrelationFixture fixture = new();
        GuiIntentCorrelation correlation = StartClick(fixture);
        GuiClientRequestObservation unexpected = new(
            fixture.Intent.SessionEpoch,
            fixture.Intent.MapId,
            GuiClientAction.UseElement,
            null,
            50);

        Assert.False(correlation.ObserveClientRequest(unexpected));
        Assert.Equal(GuiIntentCorrelationState.Interrupted, correlation.State);
        Assert.True(correlation.PlayerHasPriority);
        Assert.False(correlation.ObserveServerOutcome(fixture.MatchingOutcome()));
    }

    [Fact]
    public void SimultaneousMatchingHumanInputIsAmbiguousAndInterrupts()
    {
        GuiIntentCorrelationFixture fixture = new();
        GuiIntentCorrelation correlation = StartClick(fixture);

        Assert.False(correlation.ObserveClientRequest(fixture.MatchingRequest(potentiallyConcurrentHumanInput: true)));
        Assert.Equal(GuiIntentCorrelationState.Interrupted, correlation.State);
        Assert.True(correlation.PlayerHasPriority);
        Assert.Contains("indistinguishable", correlation.LastDetail ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SecondConcurrentIntentionIsPermanentlyRefused()
    {
        GuiIntentCorrelationFixture fixture = new();
        GuiIntentCorrelation correlation = new(fixture.Time);
        GuiActionIntent second = fixture.CreateIntent();

        Assert.Equal(GuiIntentRegistrationStatus.Registered, correlation.Register(fixture.Intent, fixture.World, fixture.Window).Status);
        GuiIntentRegistration refused = correlation.Register(second, fixture.World, fixture.Window);

        Assert.Equal(GuiIntentRegistrationStatus.Busy, refused.Status);
        Assert.Equal(GuiIntentCorrelationState.Registered, correlation.State);
        Assert.Equal(GuiIntentRegistrationStatus.Duplicate, correlation.Register(second, fixture.World, fixture.Window).Status);
    }

    [Fact]
    public void ExpiredIntentionIsCanceledWithoutClick()
    {
        GuiIntentCorrelationFixture fixture = new();
        GuiIntentCorrelation correlation = new(fixture.Time);
        GuiActionIntent expired = fixture.CreateIntent(expiresAt: fixture.Now);

        GuiIntentRegistration registration = correlation.Register(expired, fixture.World, fixture.Window);

        Assert.Equal(GuiIntentRegistrationStatus.Expired, registration.Status);
        Assert.Equal(GuiIntentCorrelationState.Cancelled, correlation.State);
        Assert.Equal(0, correlation.ClickCount);
        Assert.False(correlation.Acknowledge(fixture.World, fixture.Window));
    }

    [Fact]
    public void MissingConfirmationIsTerminalAndCannotRestartTheSameIntention()
    {
        GuiIntentCorrelationFixture fixture = new();
        GuiIntentCorrelation correlation = StartClick(fixture);

        Assert.True(correlation.ObserveClientRequest(fixture.MatchingRequest()));
        Assert.True(correlation.MarkUnconfirmed());
        Assert.Equal(GuiIntentCorrelationState.Unconfirmed, correlation.State);
        Assert.Equal(1, correlation.ClickCount);

        Assert.Equal(GuiIntentRegistrationStatus.Duplicate, correlation.Register(fixture.Intent, fixture.World, fixture.Window).Status);
        Assert.Equal(GuiIntentCorrelationState.Unconfirmed, correlation.State);
    }

    [Fact]
    public void ChangedSessionCannotBeAcknowledged()
    {
        GuiIntentCorrelationFixture fixture = new();
        GuiIntentCorrelation correlation = new(fixture.Time);
        Assert.Equal(GuiIntentRegistrationStatus.Registered, correlation.Register(fixture.Intent, fixture.World, fixture.Window).Status);

        Assert.False(correlation.Acknowledge(fixture.World with { SessionEpoch = fixture.World.SessionEpoch + 1 }, fixture.Window));
        Assert.Equal(GuiIntentCorrelationState.Interrupted, correlation.State);
        Assert.True(correlation.PlayerHasPriority);
        Assert.False(correlation.MarkClicked(fixture.World, fixture.Window));
        Assert.Equal(0, correlation.ClickCount);
    }

    [Fact]
    public void ChangedWindowCannotBeAcknowledged()
    {
        GuiIntentCorrelationFixture fixture = new();
        GuiIntentCorrelation correlation = new(fixture.Time);
        Assert.Equal(GuiIntentRegistrationStatus.Registered, correlation.Register(fixture.Intent, fixture.World, fixture.Window).Status);
        GameWindowSnapshot changedWindow = fixture.Window with
        {
            Identity = fixture.Identity with { Xid = fixture.Identity.Xid + 1 },
        };

        Assert.False(correlation.Acknowledge(fixture.World, changedWindow));
        Assert.Equal(GuiIntentCorrelationState.Interrupted, correlation.State);
        Assert.True(correlation.PlayerHasPriority);
        Assert.Equal(0, correlation.ClickCount);
    }

    [Fact]
    public void SnapshotRevisionMustRemainUnchangedImmediatelyBeforeClick()
    {
        GuiIntentCorrelationFixture fixture = new();
        GuiIntentCorrelation correlation = new(fixture.Time);
        Assert.Equal(GuiIntentRegistrationStatus.Registered, correlation.Register(fixture.Intent, fixture.World, fixture.Window).Status);
        Assert.True(correlation.Acknowledge(fixture.World, fixture.Window));

        Assert.False(correlation.MarkClicked(fixture.World with { StateRevision = fixture.World.StateRevision + 1 }, fixture.Window));
        Assert.Equal(GuiIntentCorrelationState.Interrupted, correlation.State);
        Assert.Equal(0, correlation.ClickCount);
    }

    private static GuiIntentCorrelation StartClick(GuiIntentCorrelationFixture fixture)
    {
        GuiIntentCorrelation correlation = new(fixture.Time);
        Assert.Equal(GuiIntentRegistrationStatus.Registered, correlation.Register(fixture.Intent, fixture.World, fixture.Window).Status);
        Assert.True(correlation.Acknowledge(fixture.World, fixture.Window));
        Assert.True(correlation.MarkClicked(fixture.World, fixture.Window));
        return correlation;
    }
}
