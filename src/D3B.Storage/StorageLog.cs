using D3B.Storage.Profiles;
using Microsoft.Extensions.Logging;

namespace D3B.Storage;

/// <summary>The storage's log messages.</summary>
internal static partial class StorageLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Opened the database {Path}: {Accounts} accounts, {Characters} characters, {Profiles} profiles, {Graphics} graphics learned, {Regrowths} resources' growing back, {Placements} fights' placements, {Runs} runs")]
    public static partial void Opened(ILogger logger, string path, int accounts, int characters, int profiles, int graphics, int regrowths, int placements, int runs);

    [LoggerMessage(Level = LogLevel.Information, Message = "Stored {Count} profiles the activities give")]
    public static partial void PresetsStored(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Error, Message = "What the relay saw of an account could not be recorded")]
    public static partial void RecordFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "What the bots learned of the maps' graphics could not be recorded")]
    public static partial void GraphicsFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "How long the resources take to grow back could not be recorded")]
    public static partial void RegrowthsFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "The fights' maps and placement cells could not be recorded")]
    public static partial void PlacementsFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "The activities' runs could not be recorded for the statistics")]
    public static partial void RunsFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bot {Character}: started profile {Profile} on its connection")]
    public static partial void StartedOnConnection(ILogger logger, long character, string profile);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Bot {Character}: profile {Profile} did not start on its connection, {Failure} {Error}")]
    public static partial void NotStartedOnConnection(ILogger logger, long character, string profile, ProfileStartFailure? failure, string? error);
}
