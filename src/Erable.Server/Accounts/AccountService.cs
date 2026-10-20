using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Erable.Core;
using Erable.Core.Accounts;
using Erable.Core.Projects;
using Erable.Server.Hosting;
using Erable.Storage;
using Microsoft.AspNetCore.Identity;

namespace Erable.Server.Accounts;

/// <summary>An authenticated session and the account behind it.</summary>
/// <param name="Account">Owner of the session.</param>
/// <param name="Session">The session itself (hash of the token, expiry).</param>
/// <param name="Renewed">The session was extended by this request: the cookie must be written again.</param>
public sealed record AuthenticatedSession(AccountRecord Account, SessionRecord Session, bool Renewed);

/// <summary>A freshly issued session: <see cref="Token"/> is shown once, only its hash is stored.</summary>
public sealed record IssuedSession(string Token, DateTimeOffset ExpiresAt);

/// <summary>
/// Accounts, sessions and project roles of the server. Passwords are hashed with the ASP.NET Core Identity
/// hasher (PBKDF2, per-password salt, algorithm upgrades handled by <see cref="PasswordHasher{TUser}"/>); the rest of
/// Identity (EF stores, cookie scheme) is not used: sessions need server-side revocation and the storage is Dapper.
/// Without a store (desktop mode) the only user is the local one.
/// </summary>
public sealed class AccountService
{
    private readonly ServerStore? _store;
    private readonly ErableHostOptions _options;
    private readonly TimeProvider _clock;
    private readonly PasswordHasher<string> _hasher = new();
    private readonly string _dummyHash;
    private readonly ConcurrentDictionary<string, string> _names = new(StringComparer.Ordinal);

    public AccountService(ErableHostOptions options, TimeProvider clock, ServerStore? store = null)
    {
        _options = options;
        _clock = clock;
        _store = store;
        // Verified for a login that does not exist, so that "unknown login" and "wrong password" take the same time.
        _dummyHash = _hasher.HashPassword(string.Empty, Convert.ToHexString(RandomNumberGenerator.GetBytes(16)));
    }

    /// <summary>True when the backend has accounts (server mode).</summary>
    public bool Enabled => _store is not null;

    public ServerStore Store => _store ?? throw new InvalidOperationException("Accounts exist only in server mode.");

    // ─── Accounts ───────────────────────────────────────────────────────────

    public Result<AccountRecord> CreateAccount(string login, string displayName, string password, bool isServerAdmin = false)
    {
        var normalizedLogin = AccountRules.NormalizeLogin(login);
        if (!normalizedLogin.IsSuccess)
        {
            return normalizedLogin.Error;
        }

        var name = AccountRules.NormalizeDisplayName(displayName);
        if (!name.IsSuccess)
        {
            return name.Error;
        }

        if (AccountRules.ValidatePassword(password) is { } weak)
        {
            return weak;
        }

        var created = Store.CreateAccount(new AccountRecord(
            normalizedLogin.Value, name.Value, _hasher.HashPassword(normalizedLogin.Value, password), isServerAdmin, false, _clock.GetUtcNow()));
        Invalidate();
        return created;
    }

    public Result<bool> ChangePassword(string login, string newPassword)
    {
        if (AccountRules.ValidatePassword(newPassword) is { } weak)
        {
            return weak;
        }

        return Store.SetPassword(login, _hasher.HashPassword(login, newPassword))
            ? true
            : new DomainError(ErrorCodes.AccountNotFound, $"No account '{login}'.");
    }

    public AccountRecord? Find(string login) => Store.GetAccount(login);

    /// <summary>
    /// Changes an account. Disabling it, changing its password or taking its administration away ends its
    /// sessions; the server keeps at least one active administrator.
    /// </summary>
    public Result<AccountRecord> UpdateAccount(string login, string? name, bool? disabled, bool? isServerAdmin, string? password)
    {
        var account = Store.GetAccount(login);
        if (account is null)
        {
            return new DomainError(ErrorCodes.AccountNotFound, $"No account '{login}'.");
        }

        var displayName = account.DisplayName;
        if (name is not null)
        {
            var normalized = AccountRules.NormalizeDisplayName(name);
            if (!normalized.IsSuccess)
            {
                return normalized.Error;
            }

            displayName = normalized.Value;
        }

        if (password is not null && AccountRules.ValidatePassword(password) is { } weak)
        {
            return weak;
        }

        var newDisabled = disabled ?? account.Disabled;
        var newAdmin = isServerAdmin ?? account.IsServerAdmin;
        var losesAdministration = account.IsServerAdmin && !account.Disabled && (newDisabled || !newAdmin);
        if (losesAdministration && Store.ListAccounts().Count(a => a.IsServerAdmin && !a.Disabled) <= 1)
        {
            return new DomainError(ErrorCodes.AccountLastAdmin, "The server keeps at least one administrator.");
        }

        Store.UpdateAccount(login, displayName, newAdmin, newDisabled);
        if (password is not null)
        {
            Store.SetPassword(login, _hasher.HashPassword(login, password));
        }

        if (!newAdmin && account.IsServerAdmin)
        {
            // Rights are read at each request, but a session made with the old rights is not kept.
            Store.EndSessionsOf(login);
        }

        Invalidate();
        return Store.GetAccount(login)!;
    }

    /// <summary>Display name of an author: the account name, or null when this id is not an account.</summary>
    public string? DisplayNameOf(string userId)
    {
        if (_store is null)
        {
            return null;
        }

        if (_names.TryGetValue(userId, out var cached))
        {
            return cached;
        }

        var name = _store.GetAccount(userId)?.DisplayName;
        if (name is not null)
        {
            _names[userId] = name;
        }

        return name;
    }

    /// <summary>Forgets cached display names (after an account changed).</summary>
    public void Invalidate() => _names.Clear();

    /// <summary>Creates the first administrator from the configuration, when the server has no account yet.</summary>
    public bool EnsureBootstrapAdmin()
    {
        if (_store is null || _store.CountAccounts() > 0
            || string.IsNullOrEmpty(_options.BootstrapAdminLogin) || string.IsNullOrEmpty(_options.BootstrapAdminPassword))
        {
            return false;
        }

        return CreateAccount(_options.BootstrapAdminLogin, _options.BootstrapAdminLogin, _options.BootstrapAdminPassword, isServerAdmin: true).IsSuccess;
    }

    // ─── Sessions ───────────────────────────────────────────────────────────

    /// <summary>Checks a login and password; null if either is wrong or the account is disabled.</summary>
    public AccountRecord? Verify(string? login, string? password)
    {
        var normalized = AccountRules.NormalizeLogin(login);
        var account = normalized.IsSuccess ? Store.GetAccount(normalized.Value) : null;
        var result = _hasher.VerifyHashedPassword(account?.Id ?? string.Empty, account?.PasswordHash ?? _dummyHash, password ?? string.Empty);
        if (account is null || account.Disabled || result == PasswordVerificationResult.Failed)
        {
            return null;
        }

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            // Not SetPassword: the password is the same, so the other devices of the user stay signed in.
            Store.UpgradePasswordHash(account.Id, account.PasswordHash, _hasher.HashPassword(account.Id, password!));
        }

        return account;
    }

    public IssuedSession StartSession(string userId, string kind)
    {
        var token = Base64Url(RandomNumberGenerator.GetBytes(32));
        var now = _clock.GetUtcNow();
        var expires = now + _options.SessionLifetime;
        Store.AddSession(new SessionRecord(Hash(token), userId, kind, now, expires));
        return new IssuedSession(token, expires);
    }

    /// <summary>The live session of a token, extended when more than half of its lifetime has gone; null when unknown or expired.</summary>
    public AuthenticatedSession? Authenticate(string token)
    {
        if (_store is null || string.IsNullOrEmpty(token) || token.Length > 128)
        {
            return null;
        }

        var hash = Hash(token);
        if (_store.FindSession(hash) is not { } session)
        {
            return null;
        }

        var now = _clock.GetUtcNow();
        if (session.ExpiresAt <= now)
        {
            _store.RemoveSession(hash);
            return null;
        }

        if (_store.GetAccount(session.UserId) is not { Disabled: false } account)
        {
            return null;
        }

        var renewed = false;
        if (session.ExpiresAt - now < _options.SessionLifetime / 2)
        {
            session = session with { ExpiresAt = now + _options.SessionLifetime };
            _store.ExtendSession(hash, session.ExpiresAt);
            renewed = true;
        }

        return new AuthenticatedSession(account, session, renewed);
    }

    public bool EndSession(string token) => _store is not null && _store.RemoveSession(Hash(token));

    // ─── Roles ──────────────────────────────────────────────────────────────

    public ProjectRole? RoleOf(ElementId projectId, string userId) =>
        userId == KnownUsers.Local ? ProjectRole.Admin : _store?.GetRole(projectId, userId);

    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
