using System.Collections.Concurrent;
using Erable.Core;
using Erable.Storage;

namespace Erable.Server.Projects;

/// <summary>
/// The projects open in this backend (desktop: the ones the user opened; server mode M1: same model, M4 brings
/// accounts and a project registry). A project stays open, and locked, until the host stops.
/// </summary>
public sealed class ProjectWorkspace(TimeProvider clock, UserProfile profile) : IDisposable
{
    private readonly ConcurrentDictionary<ElementId, ProjectService> _open = new();
    private readonly Lock _gate = new();

    public IEnumerable<ProjectService> OpenProjects => _open.Values;

    public ProjectService? Find(ElementId projectId) => _open.GetValueOrDefault(projectId);

    /// <summary>Creates a project and opens it.</summary>
    public Result<ProjectService> Create(string folder, string name, string contentLanguage)
    {
        lock (_gate)
        {
            var created = ProjectStore.Create(folder, name, contentLanguage, clock);
            return created.IsSuccess ? Register(created.Value) : created.Error;
        }
    }

    /// <summary>Opens a project, or returns it when this backend already has it open.</summary>
    public Result<ProjectService> Open(string folder)
    {
        lock (_gate)
        {
            var full = Path.GetFullPath(folder);
            var already = _open.Values.FirstOrDefault(p => string.Equals(p.Project.Folder, full, StringComparison.OrdinalIgnoreCase));
            if (already is not null)
            {
                profile.RecordOpened(already.Project, clock.GetUtcNow());
                return already;
            }

            var opened = ProjectStore.Open(full, clock);
            return opened.IsSuccess ? Register(opened.Value) : opened.Error;
        }
    }

    /// <summary>Closes a project (tests reset it this way); returns false when it was not open.</summary>
    public bool Close(ElementId projectId)
    {
        lock (_gate)
        {
            if (!_open.TryRemove(projectId, out var service))
            {
                return false;
            }

            service.Project.Dispose();
            return true;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            foreach (var service in _open.Values)
            {
                service.Project.Dispose();
            }

            _open.Clear();
        }
    }

    private Result<ProjectService> Register(OpenedProject project)
    {
        if (_open.TryGetValue(project.Manifest.ProjectId, out var other))
        {
            // Two folders with the same projectId (a copied folder): refuse rather than mix them up.
            project.Dispose();
            return new DomainError(
                "project.duplicateId",
                $"Another open project ({other.Project.Folder}) has the same identifier.");
        }

        var service = new ProjectService(project, clock);
        // No upload can be running yet: leftovers of an interrupted write and unreferenced blobs go now.
        project.Blobs.DeleteTemporaryFiles();
        service.CollectGarbageAsync().GetAwaiter().GetResult();
        _open[project.Manifest.ProjectId] = service;
        profile.RecordOpened(project, clock.GetUtcNow());
        return service;
    }
}
