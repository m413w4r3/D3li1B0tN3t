using Microsoft.Extensions.Logging;

namespace D3B.Bot.Hosting;

/// <summary>The host's log messages.</summary>
internal static partial class HostLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Built {Count} modules: {Modules}")]
    public static partial void ModulesBuilt(ILogger logger, int count, string modules);

    [LoggerMessage(Level = LogLevel.Information, Message = "Opened the pack of client {Version}, format {Format}: {Path}")]
    public static partial void PackOpened(ILogger logger, string version, int format, string path);

    [LoggerMessage(Level = LogLevel.Information, Message = "Loaded the mapping of client {Version}, format {Format}: {Messages} messages, {Fields} fields, {Enums} enums and {Values} values audited, {Path}")]
    public static partial void MappingLoaded(ILogger logger, string version, int format, int messages, int fields, int enums, int values, string path);

    [LoggerMessage(Level = LogLevel.Critical, Message = "The host does not start. {Reason}")]
    public static partial void DoesNotStart(ILogger logger, string reason);
}
