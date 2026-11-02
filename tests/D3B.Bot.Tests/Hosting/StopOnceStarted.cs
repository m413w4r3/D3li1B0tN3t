using Microsoft.Extensions.Hosting;

namespace D3B.Bot.Tests.Hosting;

/// <summary>Stops the host as soon as it has started, so that a test can run it to its end.</summary>
internal sealed class StopOnceStarted : IHostedService
{
    private readonly IHostApplicationLifetime _lifetime;

    public StopOnceStarted(IHostApplicationLifetime lifetime)
    {
        _lifetime = lifetime;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _lifetime.ApplicationStarted.Register(_lifetime.StopApplication);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
