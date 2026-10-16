using Erable.Core;
using Erable.Core.Accounts;
using Erable.Server.Accounts;
using Erable.Server.Hosting;
using Erable.Storage;

namespace Erable.Server.Projects;

/// <summary>
/// The projects a server hosts (M4-01): <c>data/projects/&lt;projectId&gt;/</c>, each registered in <c>server.db</c>
/// with its members. Opens them all at startup so that every request finds its project in the
/// <see cref="ProjectWorkspace"/>. In desktop mode there is no registry and every method does nothing.
/// </summary>
public sealed class ProjectCatalog(
    ErableHostOptions options, ProjectWorkspace workspace, AccountService accounts, TimeProvider clock, ILogger<ProjectCatalog> logger)
{
    /// <summary>Folder holding the hosted projects.</summary>
    public string ProjectsRoot => Path.Combine(options.DataDirectory ?? Path.GetTempPath(), "projects");

    public bool Enabled => accounts.Enabled;

    public string FolderOf(ProjectRegistration registration) =>
        Path.IsPathRooted(registration.Folder) ? registration.Folder : Path.Combine(ProjectsRoot, registration.Folder);

    /// <summary>Creates a project in its own folder under <c>data/projects</c>; the creator becomes its administrator.</summary>
    public Result<ProjectService> Create(string name, string contentLanguage, string creatorId, UserProfile? profile)
    {
        var folder = Path.Combine(ProjectsRoot, Guid.NewGuid().ToString("N"));
        var created = workspace.Create(folder, name, contentLanguage, profile, creatorId);
        if (!created.IsSuccess)
        {
            return created;
        }

        Register(created.Value, creatorId);
        return created;
    }

    /// <summary>
    /// Records a project in the registry. The first registration makes <paramref name="creatorId"/> its administrator;
    /// an already known project keeps its members.
    /// </summary>
    public void Register(ProjectService service, string creatorId)
    {
        if (!Enabled)
        {
            return;
        }

        var projectId = service.Project.Manifest.ProjectId;
        var root = Path.GetFullPath(ProjectsRoot);
        var full = Path.GetFullPath(service.Project.Folder);
        var folder = Path.GetDirectoryName(full) is { } parent
            && string.Equals(Path.TrimEndingDirectorySeparator(parent), Path.TrimEndingDirectorySeparator(root), StringComparison.OrdinalIgnoreCase)
            ? Path.GetFileName(full)
            : full;
        var known = accounts.Store.IsRegistered(projectId);
        accounts.Store.RegisterProject(new ProjectRegistration(projectId, folder, clock.GetUtcNow()));
        if (!known && accounts.Store.CountAdmins(projectId) == 0 && accounts.Store.GetAccount(creatorId) is not null)
        {
            accounts.Store.SetMember(projectId, creatorId, ProjectRole.Admin);
        }
    }

    /// <summary>Opens every registered project; a damaged one is logged and skipped, it never stops the server.</summary>
    public void OpenRegistered()
    {
        if (!Enabled)
        {
            return;
        }

        foreach (var registration in accounts.Store.ListProjects())
        {
            var folder = FolderOf(registration);
            var opened = workspace.Open(folder, profile: null);
            if (!opened.IsSuccess)
            {
                ServerLog.ProjectNotOpened(logger, registration.ProjectId, folder, opened.Error.Code, opened.Error.Message);
            }
            else if (opened.Value.Project.Manifest.ProjectId != registration.ProjectId)
            {
                ServerLog.ProjectIdMismatch(logger, folder, opened.Value.Project.Manifest.ProjectId, registration.ProjectId);
            }
        }
    }
}
