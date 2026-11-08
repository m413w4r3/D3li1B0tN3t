using D3B.App.Hosting;
using D3B.Bot.Hosting;
using D3B.Gui.Actions;
using D3B.Gui.Input;
using D3B.Gui.Safety;
using Microsoft.Extensions.Configuration;

namespace D3B.App.Tests.Hosting;

public sealed class GuiHostStartupTests
{
    [Fact]
    public void MissingGuiSectionUsesDisabledPreviewDefaults()
    {
        IConfiguration configuration = new ConfigurationBuilder().Build();

        GuiHostCapability capability = GuiHostStartup.Validate(configuration);

        AssertDisabledPreviewCapability(capability);
    }

    [Fact]
    public void EmptyGuiSectionUsesDisabledPreviewDefaults()
    {
        IConfiguration configuration = CreateConfiguration(new Dictionary<string, string?>());

        GuiHostCapability capability = GuiHostStartup.Validate(configuration);

        AssertDisabledPreviewCapability(capability);
    }

    [Theory]
    [InlineData("Gui:AllowRealInput", "true")]
    [InlineData("Gui:CaptureTimeoutMs", "99")]
    [InlineData("Gui:MaxCaptureBytes", "1048575")]
    [InlineData("Gui:MaxWidth", "0")]
    [InlineData("Gui:MaxHeight", "2161")]
    [InlineData("Gui:CalibrationMaxAgeSeconds", "31")]
    [InlineData("Gui:Mode", "999")]
    [InlineData("Gui:Backend", "999")]
    public void InvalidGuiOptionsAreRejected(string key, string value)
    {
        IConfiguration configuration = CreateConfiguration(new Dictionary<string, string?> { [key] = value });

        Assert.Throws<ArgumentException>(() => GuiHostStartup.Validate(configuration));
    }

    [Fact]
    public void ExplicitPreviewConfigurationDoesNotEnableGuiActivityOrInput()
    {
        IConfiguration configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["Gui:Enabled"] = "true",
            ["Gui:Mode"] = "Preview",
        });

        GuiHostCapability capability = GuiHostStartup.Validate(configuration);

        Assert.True(capability.Enabled);
        Assert.Equal(GuiMode.Preview, capability.Mode);
        Assert.False(capability.AllowRealInput);
        Assert.False(capability.AutomaticActivity);
        Assert.False(capability.SupervisedOneShotAvailable);
        Assert.False(capability.RealInputAvailable);
    }

    [Theory]
    [InlineData("Gui:AllowRealInput", "true")]
    [InlineData("Gui:CaptureTimeoutMs", "99")]
    [InlineData("Gui:MaxCaptureBytes", "1048575")]
    [InlineData("Gui:MaxWidth", "0")]
    [InlineData("Gui:MaxHeight", "2161")]
    [InlineData("Gui:CalibrationMaxAgeSeconds", "31")]
    [InlineData("Gui:Mode", "999")]
    [InlineData("Gui:Backend", "999")]
    public async Task InvalidGuiOptionsFailTheAppRunnerBeforeTheStartDelegateRuns(string key, string value)
    {
        var startCount = 0;
        var runner = new HostRunner(
            [$"--{key}={value}"],
            new Dictionary<string, string?>(),
            (_, _) =>
            {
                startCount++;

                return Task.FromResult(new BotHostStart(null, null));
            });

        runner.Start();
        await Waiting.UntilAsync(() => runner.Status is not HostStatus.Starting, TimeSpan.FromSeconds(30));

        Assert.Equal(HostStatus.Failed, runner.Status);
        Assert.NotNull(runner.Failure);
        Assert.Equal(0, startCount);

        await runner.StopAsync();
    }

    [Fact]
    public async Task DefaultAppCompositionRegistersOnlyTheDisabledCapabilityWithoutStartingAHost()
    {
        GuiHostCapability? capability = null;
        Type[] serviceTypes = [];
        var startCount = 0;
        var runner = new HostRunner(
            [],
            new Dictionary<string, string?>(),
            (builder, _) =>
            {
                startCount++;
                capability = builder.Services
                    .SingleOrDefault(service => service.ServiceType == typeof(GuiHostCapability))
                    ?.ImplementationInstance as GuiHostCapability;
                serviceTypes = builder.Services.Select(service => service.ServiceType).ToArray();

                return Task.FromResult(new BotHostStart(null, "The test does not start the host."));
            });

        runner.Start();
        await Waiting.UntilAsync(() => runner.Status is not HostStatus.Starting, TimeSpan.FromSeconds(30));

        Assert.Equal(HostStatus.Failed, runner.Status);
        Assert.Equal(1, startCount);
        Assert.NotNull(capability);
        AssertDisabledPreviewCapability(capability);
        Assert.DoesNotContain(typeof(GuiActionExecutor), serviceTypes);
        Assert.DoesNotContain(typeof(GuiActionGate), serviceTypes);
        Assert.DoesNotContain(typeof(IGuiInput), serviceTypes);
        Assert.DoesNotContain(typeof(XdotoolGuiInput), serviceTypes);
        Assert.DoesNotContain(serviceTypes, type => type.Name.Contains("GuiCoordinator", StringComparison.Ordinal));

        await runner.StopAsync();
    }

    private static IConfiguration CreateConfiguration(IDictionary<string, string?> settings)
    {
        return new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
    }

    private static void AssertDisabledPreviewCapability(GuiHostCapability capability)
    {
        Assert.False(capability.Enabled);
        Assert.Equal(GuiMode.Preview, capability.Mode);
        Assert.False(capability.AllowRealInput);
        Assert.False(capability.AutomaticActivity);
        Assert.False(capability.SupervisedOneShotAvailable);
        Assert.False(capability.RealInputAvailable);
    }
}
