using Microsoft.Extensions.Hosting;

namespace D3B.Bot.Hosting;

/// <summary>What starting the bots' host gave: the host, or why it did not start.</summary>
/// <param name="Host">The host, started, which the caller stops and disposes; <see langword="null"/> when it did not start.</param>
/// <param name="Failure">Why the host did not start, and what to do about it; <see langword="null"/> when it started.</param>
/// <param name="MissingBuild">
/// The installed client's build when the host did not start for lack of its pack, schema or mapping, which a newer version
/// of the app ships (D172); <see langword="null"/> otherwise.
/// </param>
public sealed record BotHostStart(IHost? Host, string? Failure, string? MissingBuild = null);
