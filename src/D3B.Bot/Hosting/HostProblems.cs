using D3B.Bot.Journal;
using Microsoft.Extensions.Logging;

namespace D3B.Bot.Hosting;

/// <summary>The host's last warnings and errors that name no bot, kept in memory for the app's logs page (D114).</summary>
/// <remarks>
/// <para>
/// A logging provider beside the others: what the host's log writes at the warning level or above reaches it, unless it
/// names a bot by its character (a <c>Character</c> value, as every bot's message does), whose journal says it already in
/// the app's words (D112). So it holds a connection whose account is unknown, a database that fails to write, a relay's
/// failure: what the host says of itself, as it says it.
/// </para>
/// <para>
/// Messages are written from any thread, the relay's read loops included: a write takes a lock and copies nothing but the
/// message's text.
/// </para>
/// </remarks>
public sealed class HostProblems : ILoggerProvider
{
    /// <summary>The number of problems kept: older ones are dropped (D113).</summary>
    public const int Capacity = 2_000;

    private const string CharacterKey = "Character";

    private readonly TimeProvider _time;

    /// <summary>Gets the problems kept, as they come.</summary>
    public RecentItems<HostProblem> Problems { get; } = new(Capacity);

    /// <summary>Initializes a new instance of the <see cref="HostProblems"/> class.</summary>
    /// <param name="time">The clock problems are dated by.</param>
    public HostProblems(TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(time);

        _time = time;
    }

    /// <inheritdoc/>
    public ILogger CreateLogger(string categoryName)
    {
        return new Logger(this, categoryName);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        // Nothing is held but the problems, which the app may still read.
    }

    private static bool NamesABot<TState>(TState state)
    {
        if (state is not IReadOnlyList<KeyValuePair<string, object?>> values)
            return false;

        for (var index = 0; index < values.Count; index++)
        {
            if (values[index].Key == CharacterKey)
                return true;
        }

        return false;
    }

    private void Write<TState>(string category, LogLevel level, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (NamesABot(state))
            return;

        Problems.Add(new HostProblem(_time.GetUtcNow(), level, category, formatter(state, exception), exception?.ToString()));
    }

    private sealed class Logger : ILogger
    {
        private readonly HostProblems _problems;
        private readonly string _category;

        public Logger(HostProblems problems, string category)
        {
            _problems = problems;
            _category = category;
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel is >= LogLevel.Warning and not LogLevel.None;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(logLevel))
                _problems.Write(_category, logLevel, state, exception, formatter);
        }
    }
}
