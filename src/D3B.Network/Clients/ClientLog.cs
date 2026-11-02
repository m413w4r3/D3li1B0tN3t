using System.Net;
using Microsoft.Extensions.Logging;

namespace D3B.Network.Clients;

/// <summary>The log messages of the client's redirection.</summary>
internal static partial class ClientLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Clients reach D3B: their configuration sends {Host} to {Address}, from {Path}")]
    public static partial void Redirected(ILogger logger, string host, IPAddress address, string path);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The client's configuration cannot be downloaded from {Url}; the copy of {Date} is used. {Reason}")]
    public static partial void UsingCopy(ILogger logger, string url, DateTime date, string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The client's configuration lists ports {Ports}, and D3B listens on {Listened}: clients choosing another go nowhere")]
    public static partial void PortsNotListened(ILogger logger, string ports, string listened);

    [LoggerMessage(Level = LogLevel.Information, Message = "The launcher rewrote {Path}: sending clients to D3B again")]
    public static partial void Rewritten(ILogger logger, string path);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The client's configuration could not be sent to D3B again: clients may connect straight to Ankama. {Reason}")]
    public static partial void RedirectFailed(ILogger logger, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Restored the client's own configuration in {Path}: clients connect straight to Ankama")]
    public static partial void Restored(ILogger logger, string path);
}
