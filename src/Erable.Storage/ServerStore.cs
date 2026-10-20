using Dapper;
using Erable.Core;
using Erable.Core.Accounts;
using Microsoft.Data.Sqlite;

namespace Erable.Storage;

/// <summary>A server account. The login is also the author name stored in projects.</summary>
/// <param name="Id">Normalized login, see <see cref="AccountRules.NormalizeLogin"/>.</param>
/// <param name="DisplayName">Name shown to other users.</param>
/// <param name="PasswordHash">ASP.NET Core Identity password hash; never leaves the server.</param>
/// <param name="IsServerAdmin">May create accounts and projects.</param>
/// <param name="Disabled">Cannot log in; the author name stays valid in the history.</param>
/// <param name="CreatedAt">Creation time (UTC).</param>
public sealed record AccountRecord(
    string Id, string DisplayName, string PasswordHash, bool IsServerAdmin, bool Disabled, DateTimeOffset CreatedAt);

/// <summary>A login session: a cookie (browser) or a bearer token (desktop connected). Only the hash of the token is kept.</summary>
public sealed record SessionRecord(string TokenHash, string UserId, string Kind, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt);

/// <summary>A project hosted by the server. <paramref name="Folder"/> is relative to <c>data/projects</c> unless rooted.</summary>
public sealed record ProjectRegistration(ElementId ProjectId, string Folder, DateTimeOffset CreatedAt);

/// <summary>A member of a project, with the name to show.</summary>
public sealed record MemberInfo(string UserId, string DisplayName, ProjectRole Role, bool Disabled);

/// <summary>What a change of the members of a project came to.</summary>
public enum MemberChange
{
    Done,

    /// <summary>The project would be left without an administrator.</summary>
    LastAdministrator,

    /// <summary>No active account has that login.</summary>
    UnknownAccount,

    /// <summary>The user is not a member of the project.</summary>
    NotAMember,
}

/// <summary>
/// <c>data/server.db</c>: accounts, sessions, hosted projects and their members (docs/architecture.md §7.1, §9).
/// Kept apart from the projects, which stay self-contained folders. Every call opens its own connection (WAL: readers
/// never wait); writes are serialized in-process.
/// </summary>
public sealed class ServerStore : IDisposable
{
    public const string FileName = "server.db";

    public const string SessionKindCookie = "cookie";
    public const string SessionKindBearer = "bearer";

    private const int BusyTimeoutMs = 5000;

    private const string AccountColumns =
        "id, display_name AS DisplayName, password_hash AS PasswordHash, is_admin AS IsAdmin, disabled AS Disabled, created_at AS CreatedAt";

    /// <summary>Schema of <c>server.db</c>; one entry per <c>PRAGMA user_version</c>.</summary>
    private static readonly string[] Schema =
    [
        """
        CREATE TABLE accounts (
            id            TEXT    PRIMARY KEY,
            display_name  TEXT    NOT NULL,
            password_hash TEXT    NOT NULL,
            is_admin      INTEGER NOT NULL DEFAULT 0,
            disabled      INTEGER NOT NULL DEFAULT 0,
            created_at    TEXT    NOT NULL
        ) STRICT;

        CREATE TABLE sessions (
            token_hash TEXT    PRIMARY KEY,
            user_id    TEXT    NOT NULL REFERENCES accounts (id) ON DELETE CASCADE,
            kind       TEXT    NOT NULL,
            created_at TEXT    NOT NULL,
            expires_at TEXT    NOT NULL
        ) STRICT;
        CREATE INDEX ix_sessions_user ON sessions (user_id);

        CREATE TABLE projects (
            project_id INTEGER PRIMARY KEY,
            folder     TEXT    NOT NULL,
            created_at TEXT    NOT NULL
        ) STRICT;

        CREATE TABLE memberships (
            project_id INTEGER NOT NULL REFERENCES projects (project_id) ON DELETE CASCADE,
            user_id    TEXT    NOT NULL REFERENCES accounts (id) ON DELETE CASCADE,
            role       TEXT    NOT NULL CHECK (role IN ('reader', 'editor', 'admin')),
            PRIMARY KEY (project_id, user_id)
        ) STRICT;
        CREATE INDEX ix_memberships_user ON memberships (user_id);
        """,
    ];

    private readonly Lock _writeGate = new();
    private readonly string _connectionString;

    private ServerStore(string path)
    {
        Path = path;
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWrite,
            Pooling = true,
            ForeignKeys = true,
            DefaultTimeout = BusyTimeoutMs / 1000,
        }.ToString();
    }

    public string Path { get; }

    /// <summary>Opens <c>server.db</c> in <paramref name="dataDirectory"/>, creating or upgrading it.</summary>
    public static ServerStore OpenOrCreate(string dataDirectory)
    {
        ArgumentNullException.ThrowIfNull(dataDirectory);
        Directory.CreateDirectory(dataDirectory);
        var path = System.IO.Path.Combine(dataDirectory, FileName);
        using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false,
        }.ToString()))
        {
            connection.Open();
            connection.Execute("PRAGMA journal_mode = WAL;");
            var version = connection.ExecuteScalar<int>("PRAGMA user_version;");
            if (version > Schema.Length)
            {
                throw new InvalidOperationException($"{FileName} was written by a newer version (schema {version}).");
            }

            for (var next = version; next < Schema.Length; next++)
            {
                using var transaction = connection.BeginTransaction(deferred: false);
                connection.Execute(Schema[next], transaction: transaction);
                connection.Execute($"PRAGMA user_version = {next + 1};", transaction: transaction);
                transaction.Commit();
            }
        }

        return new ServerStore(path);
    }

    public void Dispose()
    {
        using var probe = new SqliteConnection(_connectionString);
        SqliteConnection.ClearPool(probe);
    }

    // ─── Accounts ───────────────────────────────────────────────────────────

    public Result<AccountRecord> CreateAccount(AccountRecord account)
    {
        ArgumentNullException.ThrowIfNull(account);
        lock (_writeGate)
        {
            using var connection = Open();
            if (connection.ExecuteScalar<int>("SELECT COUNT(*) FROM accounts WHERE id = @Id", new { account.Id }) > 0)
            {
                return new DomainError(ErrorCodes.AccountLoginTaken, $"The login '{account.Id}' is taken.");
            }

            connection.Execute(
                "INSERT INTO accounts (id, display_name, password_hash, is_admin, disabled, created_at) VALUES (@Id, @DisplayName, @PasswordHash, @Admin, @Disabled, @At)",
                new
                {
                    account.Id,
                    account.DisplayName,
                    account.PasswordHash,
                    Admin = account.IsServerAdmin ? 1 : 0,
                    Disabled = account.Disabled ? 1 : 0,
                    At = StorageFormat.Timestamp(account.CreatedAt),
                });
            return account;
        }
    }

    public AccountRecord? GetAccount(string id)
    {
        using var connection = Open();
        return connection.QuerySingleOrDefault<AccountRow>($"SELECT {AccountColumns} FROM accounts WHERE id = @Id", new { Id = id })?.ToRecord();
    }

    public IReadOnlyList<AccountRecord> ListAccounts()
    {
        using var connection = Open();
        return [.. connection.Query<AccountRow>($"SELECT {AccountColumns} FROM accounts ORDER BY id").Select(r => r.ToRecord())];
    }

    public int CountAccounts()
    {
        using var connection = Open();
        return connection.ExecuteScalar<int>("SELECT COUNT(*) FROM accounts");
    }

    /// <summary>Changes the password; every session of the account ends (a stolen cookie must not survive it).</summary>
    public bool SetPassword(string id, string passwordHash)
    {
        lock (_writeGate)
        {
            using var connection = Open();
            using var transaction = connection.BeginTransaction(deferred: false);
            var changed = connection.Execute("UPDATE accounts SET password_hash = @Hash WHERE id = @Id", new { Id = id, Hash = passwordHash }, transaction) > 0;
            connection.Execute("DELETE FROM sessions WHERE user_id = @Id", new { Id = id }, transaction);
            transaction.Commit();
            return changed;
        }
    }

    /// <summary>
    /// Replaces the hash of the same password by a stronger one (algorithm upgrade at sign-in): the sessions stay, since
    /// the password did not change. Only if the hash is still <paramref name="currentHash"/> (no concurrent change).
    /// </summary>
    public bool UpgradePasswordHash(string id, string currentHash, string upgradedHash)
    {
        lock (_writeGate)
        {
            using var connection = Open();
            return connection.Execute(
                "UPDATE accounts SET password_hash = @Upgraded WHERE id = @Id AND password_hash = @Current",
                new { Id = id, Current = currentHash, Upgraded = upgradedHash }) > 0;
        }
    }

    public bool UpdateAccount(string id, string displayName, bool isServerAdmin, bool disabled)
    {
        lock (_writeGate)
        {
            using var connection = Open();
            using var transaction = connection.BeginTransaction(deferred: false);
            var changed = connection.Execute(
                "UPDATE accounts SET display_name = @Name, is_admin = @Admin, disabled = @Disabled WHERE id = @Id",
                new { Id = id, Name = displayName, Admin = isServerAdmin ? 1 : 0, Disabled = disabled ? 1 : 0 }, transaction) > 0;
            if (disabled)
            {
                connection.Execute("DELETE FROM sessions WHERE user_id = @Id", new { Id = id }, transaction);
            }

            transaction.Commit();
            return changed;
        }
    }

    // ─── Sessions ───────────────────────────────────────────────────────────

    public void AddSession(SessionRecord session)
    {
        ArgumentNullException.ThrowIfNull(session);
        lock (_writeGate)
        {
            using var connection = Open();
            connection.Execute(
                "INSERT INTO sessions (token_hash, user_id, kind, created_at, expires_at) VALUES (@TokenHash, @UserId, @Kind, @Created, @Expires)",
                new
                {
                    session.TokenHash,
                    session.UserId,
                    session.Kind,
                    Created = StorageFormat.Timestamp(session.CreatedAt),
                    Expires = StorageFormat.Timestamp(session.ExpiresAt),
                });
        }
    }

    public SessionRecord? FindSession(string tokenHash)
    {
        using var connection = Open();
        var row = connection.QuerySingleOrDefault<SessionRow>(
            "SELECT token_hash AS TokenHash, user_id AS UserId, kind AS Kind, created_at AS CreatedAt, expires_at AS ExpiresAt FROM sessions WHERE token_hash = @Hash",
            new { Hash = tokenHash });
        return row is null
            ? null
            : new SessionRecord(row.TokenHash, row.UserId, row.Kind, StorageFormat.ParseTimestamp(row.CreatedAt), StorageFormat.ParseTimestamp(row.ExpiresAt));
    }

    public void ExtendSession(string tokenHash, DateTimeOffset expiresAt)
    {
        lock (_writeGate)
        {
            using var connection = Open();
            connection.Execute("UPDATE sessions SET expires_at = @Expires WHERE token_hash = @Hash",
                new { Hash = tokenHash, Expires = StorageFormat.Timestamp(expiresAt) });
        }
    }

    public bool RemoveSession(string tokenHash)
    {
        lock (_writeGate)
        {
            using var connection = Open();
            return connection.Execute("DELETE FROM sessions WHERE token_hash = @Hash", new { Hash = tokenHash }) > 0;
        }
    }

    /// <summary>Deletes the sessions that expired before <paramref name="now"/>; returns how many.</summary>
    public int PurgeExpiredSessions(DateTimeOffset now)
    {
        lock (_writeGate)
        {
            using var connection = Open();
            return connection.Execute("DELETE FROM sessions WHERE expires_at <= @Now", new { Now = StorageFormat.Timestamp(now) });
        }
    }

    /// <summary>Ends every session of an account (it signs in again with its new rights).</summary>
    public void EndSessionsOf(string userId)
    {
        lock (_writeGate)
        {
            using var connection = Open();
            connection.Execute("DELETE FROM sessions WHERE user_id = @Id", new { Id = userId });
        }
    }

    // ─── Projects and members ───────────────────────────────────────────────

    public void RegisterProject(ProjectRegistration project)
    {
        ArgumentNullException.ThrowIfNull(project);
        lock (_writeGate)
        {
            using var connection = Open();
            connection.Execute(
                "INSERT INTO projects (project_id, folder, created_at) VALUES (@Id, @Folder, @At) ON CONFLICT(project_id) DO UPDATE SET folder = excluded.folder",
                new { Id = project.ProjectId.ToInt64(), project.Folder, At = StorageFormat.Timestamp(project.CreatedAt) });
        }
    }

    public IReadOnlyList<ProjectRegistration> ListProjects()
    {
        using var connection = Open();
        return [.. connection.Query<ProjectRow>("SELECT project_id AS Id, folder AS Folder, created_at AS CreatedAt FROM projects ORDER BY created_at, project_id")
            .Select(r => r.ToRegistration())];
    }

    public bool IsRegistered(ElementId projectId)
    {
        using var connection = Open();
        return connection.ExecuteScalar<int>("SELECT COUNT(*) FROM projects WHERE project_id = @Id", new { Id = projectId.ToInt64() }) > 0;
    }

    /// <summary>Forgets a project and its members; the folder is not touched.</summary>
    public bool UnregisterProject(ElementId projectId)
    {
        lock (_writeGate)
        {
            using var connection = Open();
            return connection.Execute("DELETE FROM projects WHERE project_id = @Id", new { Id = projectId.ToInt64() }) > 0;
        }
    }

    /// <summary>The role of a user in a project, or null for a non-member (or a disabled account).</summary>
    public ProjectRole? GetRole(ElementId projectId, string userId)
    {
        using var connection = Open();
        var role = connection.QuerySingleOrDefault<string?>(
            "SELECT m.role FROM memberships m JOIN accounts a ON a.id = m.user_id WHERE m.project_id = @Project AND m.user_id = @User AND a.disabled = 0",
            new { Project = projectId.ToInt64(), User = userId });
        return ProjectRoles.TryParse(role, out var parsed) ? parsed : null;
    }

    /// <summary>Adds a member or changes their role.</summary>
    public void SetMember(ElementId projectId, string userId, ProjectRole role)
    {
        lock (_writeGate)
        {
            using var connection = Open();
            connection.Execute(
                "INSERT INTO memberships (project_id, user_id, role) VALUES (@Project, @User, @Role) ON CONFLICT(project_id, user_id) DO UPDATE SET role = excluded.role",
                new { Project = projectId.ToInt64(), User = userId, Role = ProjectRoles.NameOf(role) });
        }
    }

    public bool RemoveMember(ElementId projectId, string userId)
    {
        lock (_writeGate)
        {
            using var connection = Open();
            return connection.Execute("DELETE FROM memberships WHERE project_id = @Project AND user_id = @User",
                new { Project = projectId.ToInt64(), User = userId }) > 0;
        }
    }

    /// <summary>
    /// Adds a member or changes their role, refusing to take the last administrator away (<see cref="MemberRules"/>); the
    /// check and the write are one step under the write lock, so two administrators cannot both leave.
    /// </summary>
    public MemberChange ChangeRole(ElementId projectId, string userId, ProjectRole role)
    {
        lock (_writeGate)
        {
            using var connection = Open();
            if (connection.ExecuteScalar<int>("SELECT COUNT(*) FROM accounts WHERE id = @Id AND disabled = 0", new { Id = userId }) == 0)
            {
                return MemberChange.UnknownAccount;
            }

            if (MemberRules.LeavesNoAdministrator(RoleEntries(connection, projectId), userId, role))
            {
                return MemberChange.LastAdministrator;
            }

            connection.Execute(
                "INSERT INTO memberships (project_id, user_id, role) VALUES (@Project, @User, @Role) ON CONFLICT(project_id, user_id) DO UPDATE SET role = excluded.role",
                new { Project = projectId.ToInt64(), User = userId, Role = ProjectRoles.NameOf(role) });
            return MemberChange.Done;
        }
    }

    /// <summary>Removes a member, refusing to take the last administrator away.</summary>
    public MemberChange RemoveMemberChecked(ElementId projectId, string userId)
    {
        lock (_writeGate)
        {
            using var connection = Open();
            var entries = RoleEntries(connection, projectId);
            if (entries.All(e => e.UserId != userId))
            {
                return MemberChange.NotAMember;
            }

            if (MemberRules.LeavesNoAdministrator(entries, userId, newRole: null))
            {
                return MemberChange.LastAdministrator;
            }

            connection.Execute("DELETE FROM memberships WHERE project_id = @Project AND user_id = @User",
                new { Project = projectId.ToInt64(), User = userId });
            return MemberChange.Done;
        }
    }

    private static List<MemberRoleEntry> RoleEntries(SqliteConnection connection, ElementId projectId) =>
        [.. connection.Query<MemberRow>(
            "SELECT m.user_id AS UserId, a.display_name AS Name, m.role AS Role, a.disabled AS Disabled FROM memberships m JOIN accounts a ON a.id = m.user_id WHERE m.project_id = @Project",
            new { Project = projectId.ToInt64() })
            .Select(r => new MemberRoleEntry(r.UserId, ProjectRoles.TryParse(r.Role, out var role) ? role : ProjectRole.Reader, r.Disabled == 0))];

    public IReadOnlyList<MemberInfo> ListMembers(ElementId projectId)
    {
        using var connection = Open();
        return [.. connection.Query<MemberRow>(
            "SELECT m.user_id AS UserId, a.display_name AS Name, m.role AS Role, a.disabled AS Disabled FROM memberships m JOIN accounts a ON a.id = m.user_id WHERE m.project_id = @Project ORDER BY a.display_name COLLATE NOCASE, m.user_id",
            new { Project = projectId.ToInt64() })
            .Select(r => new MemberInfo(r.UserId, r.Name, ProjectRoles.TryParse(r.Role, out var role) ? role : ProjectRole.Reader, r.Disabled != 0))];
    }

    /// <summary>Active administrators of a project (a disabled account cannot administer anything).</summary>
    public int CountAdmins(ElementId projectId)
    {
        using var connection = Open();
        return connection.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM memberships m JOIN accounts a ON a.id = m.user_id WHERE m.project_id = @Project AND m.role = 'admin' AND a.disabled = 0",
            new { Project = projectId.ToInt64() });
    }

    /// <summary>The projects a user belongs to, with their role.</summary>
    public IReadOnlyList<(ProjectRegistration Project, ProjectRole Role)> ListProjectsOf(string userId)
    {
        using var connection = Open();
        return [.. connection.Query<ProjectRow>(
            "SELECT p.project_id AS Id, p.folder AS Folder, p.created_at AS CreatedAt, m.role AS Role FROM memberships m JOIN projects p ON p.project_id = m.project_id JOIN accounts a ON a.id = m.user_id WHERE m.user_id = @User AND a.disabled = 0 ORDER BY p.created_at, p.project_id",
            new { User = userId })
            .Select(r => (r.ToRegistration(), ProjectRoles.TryParse(r.Role, out var role) ? role : ProjectRole.Reader))];
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        // Authentication data: a login that returned must survive a power cut.
        connection.Execute($"PRAGMA busy_timeout = {BusyTimeoutMs}; PRAGMA synchronous = FULL;");
        return connection;
    }

    private sealed record AccountRow(string Id, string DisplayName, string PasswordHash, long IsAdmin, long Disabled, string CreatedAt)
    {
        public AccountRecord ToRecord() => new(Id, DisplayName, PasswordHash, IsAdmin != 0, Disabled != 0, StorageFormat.ParseTimestamp(CreatedAt));
    }

    private sealed record SessionRow(string TokenHash, string UserId, string Kind, string CreatedAt, string ExpiresAt);

    private sealed record MemberRow(string UserId, string Name, string Role, long Disabled);

    private sealed class ProjectRow
    {
        public long Id { get; init; }

        public string Folder { get; init; } = string.Empty;

        public string CreatedAt { get; init; } = string.Empty;

        public string? Role { get; init; }

        public ProjectRegistration ToRegistration() => new(ElementId.FromInt64(Id), Folder, StorageFormat.ParseTimestamp(CreatedAt));
    }
}
