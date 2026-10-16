using Erable.Core;
namespace Erable.Server.Hosting;

/// <summary>Log messages of the server (source-generated: no formatting cost when the level is off).</summary>
internal static partial class ServerLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Created the administrator account '{Login}'.")]
    public static partial void AdminCreated(ILogger logger, string login);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The server has no account: set ERABLE_ADMIN_LOGIN and ERABLE_ADMIN_PASSWORD to create the first administrator.")]
    public static partial void NoAccount(ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "Project {ProjectId} in {Folder} was not opened: {Code} {Detail}")]
    public static partial void ProjectNotOpened(ILogger logger, ElementId projectId, string folder, string code, string detail);

    [LoggerMessage(Level = LogLevel.Error, Message = "Folder {Folder} holds project {Actual}, not the registered {Expected}.")]
    public static partial void ProjectIdMismatch(ILogger logger, string folder, ElementId actual, ElementId expected);

    [LoggerMessage(Level = LogLevel.Error, Message = "Endpoint {Endpoint} declares no access rule.")]
    public static partial void UndeclaredAccess(ILogger logger, string? endpoint);
}
