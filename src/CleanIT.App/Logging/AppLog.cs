using Microsoft.Extensions.Logging;

namespace CleanIT.App.Logging;

internal static partial class AppLog
{
    [LoggerMessage(5000, LogLevel.Information, "CleanIT {Version} started (elevated: {Elevated})")]
    public static partial void Started(this ILogger logger, string version, bool elevated);

    [LoggerMessage(5001, LogLevel.Information, "Relaunching as administrator")]
    public static partial void ElevatedRestart(this ILogger logger);

    [LoggerMessage(5002, LogLevel.Information, "Elevation prompt declined by the user")]
    public static partial void ElevationDeclined(this ILogger logger);

    [LoggerMessage(5003, LogLevel.Error, "Unhandled exception")]
    public static partial void Unhandled(this ILogger logger, Exception exception);

    [LoggerMessage(5004, LogLevel.Error, "Operation {Operation} failed")]
    public static partial void OperationFailed(this ILogger logger, Exception exception, string operation);

    [LoggerMessage(5005, LogLevel.Information, "User confirmed cleanup: mode {Mode}, {ItemCount} items, {Bytes} bytes")]
    public static partial void CleanupConfirmed(this ILogger logger, CleanIT.Core.Models.CleanupMode mode, long itemCount, long bytes);
}
