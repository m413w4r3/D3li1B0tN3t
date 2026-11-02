using D3B.App.Hosting;
using D3B.Bot.Hosting;
using D3B.Bot.Runtime;
using D3B.Bot.Tests;
using D3B.Network.Relay;
using Microsoft.Extensions.DependencyInjection;

namespace D3B.App.Tests.Hosting;

public sealed class HostRunnerTests : IDisposable
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

    private readonly TestInstall _install = new();

    public void Dispose()
    {
        _install.Dispose();
    }

    [Fact]
    public async Task TheHostRunsInTheAppsProcessUntilItStopsThenGivesTheClientItsConfigurationBack()
    {
        _install.WritePack();
        _install.WriteProtocol();

        // The host the app runs adds the storage, whose database goes to the test's own folder, never the user's.
        var runner = new HostRunner([.. _install.Arguments, "--Storage:Path", Path.Combine(_install.Root, "d3b.db")], new Dictionary<string, string?>());

        Assert.Equal(HostStatus.Starting, runner.Status);

        runner.Start();
        await Waiting.UntilAsync(() => runner.Status is not HostStatus.Starting, _timeout);

        Assert.Equal(HostStatus.Running, runner.Status);
        Assert.NotNull(runner.Services?.GetService<Bots>());
        Assert.DoesNotContain($"ConfigUrl={_install.ConfigUrl}\n", File.ReadAllText(_install.VersionFile), StringComparison.Ordinal);

        await runner.StopAsync().WaitAsync(_timeout, TestContext.Current.CancellationToken);

        Assert.Equal(HostStatus.Stopped, runner.Status);
        Assert.Null(runner.Services);
        Assert.EndsWith($"ConfigUrl={_install.ConfigUrl}\n", File.ReadAllText(_install.VersionFile), StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheHostStartsWithTheSettingsTheAppChangedWhichItsCommandLineWinsOver()
    {
        _install.WritePack();
        _install.WriteProtocol();

        var settings = new Dictionary<string, string?>
        {
            [BotDelays.DisplayKey] = "1.5",
            [BotDelays.AnswerKey] = "6",
            [BotDelays.PlacedKey] = null,
            [RelayTimeouts.HoldKey] = "10",
        };
        var runner = new HostRunner([.. _install.Arguments, "--Storage:Path", Path.Combine(_install.Root, "d3b.db"), $"--{BotDelays.AnswerKey}", "9"], settings);

        runner.Start();
        await Waiting.UntilAsync(() => runner.Status is not HostStatus.Starting, _timeout);

        try
        {
            Assert.Equal(HostStatus.Running, runner.Status);

            var delays = runner.Services?.GetService<Bots>()?.Delays;

            Assert.Equal(BotDelays.Measured with { Display = TimeSpan.FromSeconds(1.5), Answer = TimeSpan.FromSeconds(9) }, delays);
            Assert.Equal(TimeSpan.FromSeconds(10), runner.Services?.GetService<RelaySettings>()?.HoldTimeout);
        }
        finally
        {
            await runner.StopAsync().WaitAsync(_timeout, TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task ADelayBelowItsMeasuredValueStopsTheHostAndSaysWhich()
    {
        var runner = new HostRunner([.. _install.Arguments, "--Storage:Path", Path.Combine(_install.Root, "d3b.db")], new Dictionary<string, string?> { [BotDelays.DisplayKey] = "0.2" });

        runner.Start();
        await Waiting.UntilAsync(() => runner.Status is not HostStatus.Starting, _timeout);

        Assert.Equal(HostStatus.Failed, runner.Status);
        Assert.Contains(BotDelays.DisplayKey, runner.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AHostThatDidNotStartSaysWhy()
    {
        var runner = new HostRunner(_ => Task.FromResult(new BotHostStart(null, "No pack for client 3.7.12.12.")));

        runner.Start();
        await Waiting.UntilAsync(() => runner.Status is not HostStatus.Starting, _timeout);

        Assert.Equal(HostStatus.Failed, runner.Status);
        Assert.Equal("No pack for client 3.7.12.12.", runner.Failure);
        Assert.Null(runner.Services);

        await runner.StopAsync();

        Assert.Equal(HostStatus.Failed, runner.Status);
    }

    [Fact]
    public async Task AHostThatCouldNotBeBuiltSaysWhy()
    {
        var runner = new HostRunner(_ => throw new InvalidOperationException("An activity with the id patrol is already registered."));

        runner.Start();
        await Waiting.UntilAsync(() => runner.Status is not HostStatus.Starting, _timeout);

        Assert.Equal(HostStatus.Failed, runner.Status);
        Assert.Equal("An activity with the id patrol is already registered.", runner.Failure);
    }

    [Fact]
    public async Task StoppingTheAppWhileTheHostGetsReadyCancelsIt()
    {
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var runner = new HostRunner(async cancellationToken =>
        {
            // As a pack's download would, until the app stops.
            using var registration = cancellationToken.Register(cancelled.SetResult);

            await Task.Delay(Timeout.Infinite, cancellationToken);

            return new BotHostStart(null, null);
        });

        runner.Start();
        await runner.StopAsync().WaitAsync(_timeout, TestContext.Current.CancellationToken);

        Assert.True(cancelled.Task.IsCompleted);
        Assert.Equal(HostStatus.Stopped, runner.Status);
    }

    [Fact]
    public void TheHostStopsOnceHoweverOftenTheAppStopsIt()
    {
        var runner = new HostRunner(_ => Task.FromResult(new BotHostStart(null, "No pack.")));

        Assert.Same(runner.StopAsync(), runner.StopAsync());
    }
}
