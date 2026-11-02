using D3B.Bot.Hosting;

namespace D3B.Bot.Tests.Hosting;

/// <summary>A module's preparation, which says whether it ran, and fails if it was given why.</summary>
internal sealed class TestPreparation : IHostPreparation
{
    private readonly string? _failure;

    public bool Prepared { get; private set; }

    public TestPreparation(string? failure)
    {
        _failure = failure;
    }

    public Task<string?> PrepareAsync(CancellationToken cancellationToken)
    {
        Prepared = true;

        return Task.FromResult(_failure);
    }
}
