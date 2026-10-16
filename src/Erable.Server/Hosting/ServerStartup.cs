using Erable.Server.Accounts;
using Erable.Server.Projects;

namespace Erable.Server.Hosting;

/// <summary>
/// Server mode startup (M4-01): creates the first administrator from the configuration, opens the hosted projects and
/// purges the expired sessions. Runs before Kestrel starts listening, so no request meets a half-ready server.
/// </summary>
internal sealed class ServerStartup(
    ErableHostOptions options, AccountService accounts, ProjectCatalog catalog, ProjectWorkspace workspace, TimeProvider clock, ILogger<ServerStartup> logger)
    : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!accounts.Enabled)
        {
            return Task.CompletedTask;
        }

        if (accounts.EnsureBootstrapAdmin())
        {
            ServerLog.AdminCreated(logger, options.BootstrapAdminLogin!);
        }
        else if (accounts.Store.CountAccounts() == 0)
        {
            ServerLog.NoAccount(logger);
        }

#if ERABLE_TEST_ENDPOINTS
        if (options.TestMode)
        {
            Testing.TestUsers.Seed(options, accounts);
        }
#endif
        accounts.Store.PurgeExpiredSessions(clock.GetUtcNow());
        catalog.OpenRegistered();
        ShareProject();
        return Task.CompletedTask;
    }

    /// <summary>
    /// <c>erable serve ./MonWiki</c>: the folder is registered as a project of the server, administered by the first
    /// server administrator, unless it already has an administrator.
    /// </summary>
    private void ShareProject()
    {
        if (options.SharedProjectFolder is not { } folder)
        {
            return;
        }

        var opened = workspace.Open(folder, profile: null);
        if (!opened.IsSuccess)
        {
            ServerLog.SharedProjectNotOpened(logger, folder, opened.Error.Code, opened.Error.Message);
            return;
        }

        if (accounts.Store.ListAccounts().FirstOrDefault(a => a.IsServerAdmin && !a.Disabled) is { } admin)
        {
            catalog.Register(opened.Value, admin.Id);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
