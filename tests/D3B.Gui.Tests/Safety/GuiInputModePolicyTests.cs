using D3B.Gui.Safety;

namespace D3B.Gui.Tests.Safety;

public sealed class GuiInputModePolicyTests
{
    private readonly FakeRequestClassifier _classifier = new();

    [Fact]
    public void GuiClientInputRefusesD3BGameplayRequests()
    {
        var allowed = GuiInputModePolicy.IsAllowed(GameInputMode.GuiClientInput, "d3b-gameplay", _classifier);

        Assert.False(allowed);
    }

    [Fact]
    public void GuiClientInputAllowsClientGameplayTraffic()
    {
        var allowed = GuiInputModePolicy.IsAllowed(GameInputMode.GuiClientInput, "client-gameplay", _classifier);

        Assert.True(allowed);
    }

    [Fact]
    public void GuiClientInputAllowsNonGameplayD3BRequests()
    {
        var allowed = GuiInputModePolicy.IsAllowed(GameInputMode.GuiClientInput, "d3b-control", _classifier);

        Assert.True(allowed);
    }

    [Fact]
    public void ProtocolInjectionKeepsItsExistingAdmissionBehavior()
    {
        var allowed = GuiInputModePolicy.IsAllowed(GameInputMode.ProtocolInjection, "d3b-gameplay", _classifier);

        Assert.True(allowed);
    }

    [Fact]
    public void UndefinedModeIsRejected()
    {
        var allowed = GuiInputModePolicy.IsAllowed((GameInputMode)99, "client-gameplay", _classifier);

        Assert.False(allowed);
    }

    private sealed class FakeRequestClassifier : IGameInputRequestClassifier<string>
    {
        public GameInputRequestClassification Classify(string request)
        {
            return request switch
            {
                "d3b-gameplay" => new GameInputRequestClassification(GameInputRequestSource.D3B, true),
                "d3b-control" => new GameInputRequestClassification(GameInputRequestSource.D3B, false),
                "client-gameplay" => new GameInputRequestClassification(GameInputRequestSource.Client, true),
                _ => new GameInputRequestClassification((GameInputRequestSource)99, true),
            };
        }
    }
}
