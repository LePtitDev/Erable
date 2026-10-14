using System.Reflection;
using Dapper;
using Erable.Core;
using Microsoft.Data.Sqlite;

namespace Erable.Storage;

/// <summary>
/// The SQLite file of a project (docs/architecture.md §7). WAL mode, foreign keys, busy timeout on every connection.
/// Writes are serialized in-process (SQLite has a single writer anyway): a writer never waits on <c>SQLITE_BUSY</c>.
/// </summary>
public sealed class ProjectDatabase : IDisposable
{
    /// <summary>How long a connection waits for a lock held by another connection.</summary>
    private const int BusyTimeoutMs = 5000;

    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private readonly string _connectionString;

    private ProjectDatabase(string path)
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

    /// <summary>Opens an existing database file (never creates one).</summary>
    public static ProjectDatabase Open(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Project database not found.", path);
        }

        return new ProjectDatabase(path);
    }

    /// <summary>Creates the database file with the current schema.</summary>
    public static ProjectDatabase Create(string path)
    {
        using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false,
        }.ToString()))
        {
            connection.Open();
            // Persistent: stored in the file, applies to every later connection.
            connection.Execute("PRAGMA journal_mode = WAL;");
        }

        var database = new ProjectDatabase(path);
        using var migrating = database.OpenConnection();
        Migrations.Apply(migrating, fromVersion: 0);
        return database;
    }

    /// <summary>Schema version stored in the file (<c>PRAGMA user_version</c>).</summary>
    public int ReadSchemaVersion()
    {
        using var connection = OpenConnection();
        return connection.ExecuteScalar<int>("PRAGMA user_version;");
    }

    /// <summary>A new connection for reads (WAL: readers never block the writer).</summary>
    public SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        // Durability first: a wiki write is rare and small; FULL survives a power cut with the last commit.
        connection.Execute($"PRAGMA busy_timeout = {BusyTimeoutMs}; PRAGMA synchronous = FULL;");
        return connection;
    }

    /// <summary>Runs <paramref name="work"/> in one write transaction, committed only if it returns normally.</summary>
    public async Task<T> WriteAsync<T>(Func<SqliteConnection, SqliteTransaction, T> work, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(work);
        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return Write(work);
        }
        finally
        {
            _writeGate.Release();
        }
    }

    /// <summary>Synchronous variant of <see cref="WriteAsync{T}"/> for tools and tests.</summary>
    public T Write<T>(Func<SqliteConnection, SqliteTransaction, T> work)
    {
        ArgumentNullException.ThrowIfNull(work);
        using var connection = OpenConnection();
        // IMMEDIATE: take the write lock at BEGIN, so that the transaction never fails half-way on a busy upgrade.
        using var transaction = connection.BeginTransaction(deferred: false);
        var result = work(connection, transaction);
        transaction.Commit();
        return result;
    }

    /// <summary>Folds the WAL into the main file (clean close, F16.R3) and releases pooled connections.</summary>
    public void Dispose()
    {
        try
        {
            using var connection = OpenConnection();
            connection.Execute("PRAGMA wal_checkpoint(TRUNCATE);");
        }
        catch (SqliteException)
        {
            // Best effort: the WAL is replayed at the next opening anyway.
        }

        SqliteConnection.ClearPool(new SqliteConnection(_connectionString));
        _writeGate.Dispose();
    }

    /// <summary>Backup through the SQLite online backup API (consistent even with a WAL).</summary>
    public string Backup(DateTimeOffset now)
    {
        var target = System.IO.Path.Combine(
            System.IO.Path.GetDirectoryName(Path)!,
            ProjectFiles.BackupPrefix + now.UtcDateTime.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture));
        using var source = OpenConnection();
        using var destination = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = target,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false,
        }.ToString());
        destination.Open();
        source.BackupDatabase(destination);
        return target;
    }
}

/// <summary>
/// Versioned SQL migrations embedded in the assembly (<c>Migrations/NNNN_name.sql</c>). Each one runs in a
/// transaction that also sets <c>PRAGMA user_version</c>: a migration is applied entirely or not at all.
/// </summary>
public static class Migrations
{
    private static readonly List<(int Version, string Name, string Sql)> All = Load();

    /// <summary>Schema version produced by this build; equals the project <c>formatVersion</c>.</summary>
    public static int LatestVersion => All.Count;

    public static void Apply(SqliteConnection connection, int fromVersion)
    {
        ArgumentNullException.ThrowIfNull(connection);
        foreach (var (version, _, sql) in All.Where(m => m.Version > fromVersion))
        {
            using var transaction = connection.BeginTransaction(deferred: false);
            connection.Execute(sql, transaction: transaction);
            connection.Execute($"PRAGMA user_version = {version};", transaction: transaction);
            transaction.Commit();
        }
    }

    private static List<(int, string, string)> Load()
    {
        var assembly = typeof(Migrations).Assembly;
        const string prefix = "Erable.Storage.Migrations.";
        var migrations = assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith(prefix, StringComparison.Ordinal) && n.EndsWith(".sql", StringComparison.Ordinal))
            .Select(n =>
            {
                var name = n[prefix.Length..^4];
                var version = int.Parse(name.AsSpan(0, 4), System.Globalization.CultureInfo.InvariantCulture);
                return (version, name, Read(assembly, n));
            })
            .OrderBy(m => m.version)
            .ToList();

        // Versions are contiguous from 1: a gap would silently skip a migration.
        for (var i = 0; i < migrations.Count; i++)
        {
            if (migrations[i].version != i + 1)
            {
                throw new InvalidOperationException($"Migration {i + 1} is missing (found {migrations[i].name}).");
            }
        }

        if (migrations.Count != ProjectFormat.CurrentVersion)
        {
            throw new InvalidOperationException(
                $"{migrations.Count} migrations but ProjectFormat.CurrentVersion is {ProjectFormat.CurrentVersion}: update both together.");
        }

        return migrations;
    }

    private static string Read(Assembly assembly, string resource)
    {
        using var stream = assembly.GetManifestResourceStream(resource)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
