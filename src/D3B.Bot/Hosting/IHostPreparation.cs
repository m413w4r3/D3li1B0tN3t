namespace D3B.Bot.Hosting;

/// <summary>Gets something a module needs ready before the bots' host starts, such as its database, or says why the host cannot start (D100).</summary>
/// <remarks>
/// A module registers one as a service. <see cref="BotHost.StartAsync"/> runs every preparation, in the order they were
/// registered, once the game data and the protocol are ready and before the host starts: the first that fails stops the
/// host, which says why, as when the pack is missing.
/// </remarks>
public interface IHostPreparation
{
    /// <summary>Gets ready.</summary>
    /// <param name="cancellationToken">Stops getting ready, as the app closing does.</param>
    /// <returns>Why the host cannot start, and what to do about it, or <see langword="null"/> once ready.</returns>
    Task<string?> PrepareAsync(CancellationToken cancellationToken);
}
