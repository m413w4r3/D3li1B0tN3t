using Microsoft.Extensions.Logging;

namespace D3B.App.Pages;

/// <summary>The log messages of the modules' pages.</summary>
internal static partial class PageLog
{
    [LoggerMessage(Level = LogLevel.Error, Message = "The page {Id} failed, and shows what it threw instead")]
    public static partial void Failed(ILogger logger, string id, Exception exception);
}
