using Microsoft.Extensions.Logging;

namespace D3B.Bot.Hosting;

/// <summary>A warning or an error of the host that names no bot, as its log wrote it (D114).</summary>
/// <param name="At">When it was logged.</param>
/// <param name="Level">Its level: a warning, an error or a critical one.</param>
/// <param name="Category">The log it was written to, such as the relay's.</param>
/// <param name="Message">What the log says, in the host's own words, in English.</param>
/// <param name="Exception">The exception it was logged with, whole, if any.</param>
public sealed record HostProblem(DateTimeOffset At, LogLevel Level, string Category, string Message, string? Exception);
