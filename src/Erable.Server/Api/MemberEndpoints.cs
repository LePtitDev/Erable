using Erable.Core;
using Erable.Core.Accounts;
using Erable.Server.Accounts;
using Erable.Server.Hosting;
using Erable.Server.Realtime;
using Erable.Server.Security;
using Erable.Storage;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Erable.Server.Api;

/// <summary>A member of a project, for its administrators (F14.R1).</summary>
public sealed record MemberItem(string UserId, string Name, ProjectRole Role, bool Disabled, bool IsMe);

public sealed record SetMemberRequest(ProjectRole Role);

/// <summary>An account of the server, for its administrators.</summary>
public sealed record AccountItem(string Id, string Name, bool IsServerAdmin, bool Disabled, DateTimeOffset CreatedAt);

public sealed record CreateAccountRequest(string Login, string Name, string Password, bool IsServerAdmin = false);

/// <param name="Name">New display name; null to keep it.</param>
/// <param name="Disabled">Disable (ends the sessions) or enable the account; null to keep it.</param>
/// <param name="IsServerAdmin">Grant or take away the administration of the server; null to keep it.</param>
/// <param name="Password">New password (ends the sessions); null to keep it.</param>
public sealed record UpdateAccountRequest(string? Name = null, bool? Disabled = null, bool? IsServerAdmin = null, string? Password = null);

/// <summary>
/// The members of a project (administrators of the project only) and the accounts of the server (administrators of the
/// server only) — F14.R1, decision Q5. Accounts are created by a server administrator: no free sign-up in v1.
/// </summary>
public static class MemberEndpoints
{
    private const string Unavailable = "accounts.unavailable";

    public static RouteGroupBuilder MapMemberEndpoints(this RouteGroupBuilder project)
    {
        ArgumentNullException.ThrowIfNull(project);
        var members = project.MapGroup("/members").WithTags("Members");
        members.MapGet("/", ListMembers).WithName("ListMembers").RequireProjectRole(ProjectRole.Admin);
        members.MapPut("/{userId}", SetMember).WithName("SetMember").RequireProjectRole(ProjectRole.Admin);
        members.MapDelete("/{userId}", RemoveMember).WithName("RemoveMember").RequireProjectRole(ProjectRole.Admin);
        return project;
    }

    public static RouteGroupBuilder MapAccountEndpoints(this RouteGroupBuilder api)
    {
        ArgumentNullException.ThrowIfNull(api);
        var accounts = api.MapGroup("/accounts").WithTags("Accounts").RequireServerAdmin();
        accounts.MapGet("/", ListAccounts).WithName("ListAccounts");
        accounts.MapPost("/", CreateAccount).WithName("CreateAccount");
        accounts.MapPatch("/{login}", UpdateAccount).WithName("UpdateAccount");
        return api;
    }

    private static ProblemHttpResult NoAccounts() =>
        Problems.Problem(StatusCodes.Status404NotFound, Unavailable, "Accounts exist on a server only.");

    // ─── Members of a project ───────────────────────────────────────────────

    private static Results<Ok<MemberItem[]>, ProblemHttpResult> ListMembers(ElementId projectId, AccountService accounts, HttpContext http)
    {
        if (!accounts.Enabled)
        {
            return NoAccounts();
        }

        var me = CurrentUser.Id(http);
        return TypedResults.Ok(accounts.Store.ListMembers(projectId).Select(m => ItemOf(m, me)).ToArray());
    }

    private static async Task<Results<Ok<MemberItem>, ProblemHttpResult>> SetMember(
        ElementId projectId, string userId, SetMemberRequest request, AccountService accounts, ProjectEvents events, HttpContext http)
    {
        if (!accounts.Enabled)
        {
            return NoAccounts();
        }

        if (!Enum.IsDefined(request.Role))
        {
            return Problems.Problem(StatusCodes.Status422UnprocessableEntity, ErrorCodes.MemberRoleInvalid, "Roles: reader, editor, admin.");
        }

        var login = userId.Trim().ToLowerInvariant();
        switch (accounts.Store.ChangeRole(projectId, login, request.Role))
        {
            case MemberChange.UnknownAccount:
                return Problems.From(new DomainError(ErrorCodes.AccountNotFound, $"No account '{login}'."));
            case MemberChange.LastAdministrator:
                return Problems.From(new DomainError(ErrorCodes.MemberLastAdmin, "A project keeps at least one administrator."));
            case MemberChange.Done:
                break;
            default:
                return Problems.From(new DomainError(ErrorCodes.MemberNotFound, "Not a member."));
        }

        // The members connected to the project learn that rights changed; their screen asks again who they are.
        await events.MembersChangedAsync(projectId).ConfigureAwait(false);
        var member = accounts.Store.ListMembers(projectId).First(m => m.UserId == login);
        return TypedResults.Ok(ItemOf(member, CurrentUser.Id(http)));
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> RemoveMember(
        ElementId projectId, string userId, AccountService accounts, ProjectEvents events)
    {
        if (!accounts.Enabled)
        {
            return NoAccounts();
        }

        var login = userId.Trim().ToLowerInvariant();
        switch (accounts.Store.RemoveMemberChecked(projectId, login))
        {
            case MemberChange.NotAMember:
                return Problems.From(new DomainError(ErrorCodes.MemberNotFound, $"'{login}' is not a member."));
            case MemberChange.LastAdministrator:
                return Problems.From(new DomainError(ErrorCodes.MemberLastAdmin, "A project keeps at least one administrator."));
            default:
                break;
        }

        // Out of the project at once: no event of it reaches them any more.
        await events.RevokeAsync(projectId, login).ConfigureAwait(false);
        await events.MembersChangedAsync(projectId).ConfigureAwait(false);
        return TypedResults.NoContent();
    }

    private static MemberItem ItemOf(Storage.MemberInfo member, string me) =>
        new(member.UserId, member.DisplayName, member.Role, member.Disabled, member.UserId == me);

    // ─── Accounts of the server ─────────────────────────────────────────────

    private static Results<Ok<AccountItem[]>, ProblemHttpResult> ListAccounts(AccountService accounts) =>
        accounts.Enabled
            ? TypedResults.Ok(accounts.Store.ListAccounts().Select(AccountOf).ToArray())
            : NoAccounts();

    private static Results<Created<AccountItem>, ProblemHttpResult> CreateAccount(CreateAccountRequest request, AccountService accounts)
    {
        if (!accounts.Enabled)
        {
            return NoAccounts();
        }

        var created = accounts.CreateAccount(request.Login, request.Name, request.Password, request.IsServerAdmin);
        return created.IsSuccess
            ? TypedResults.Created($"{ApiRoutes.V1}/accounts/{created.Value.Id}", AccountOf(created.Value))
            : Problems.From(created.Error);
    }

    private static async Task<Results<Ok<AccountItem>, ProblemHttpResult>> UpdateAccount(
        string login, UpdateAccountRequest request, AccountService accounts, ProjectEvents events)
    {
        if (!accounts.Enabled)
        {
            return NoAccounts();
        }

        var updated = accounts.UpdateAccount(login.Trim().ToLowerInvariant(), request.Name, request.Disabled, request.IsServerAdmin, request.Password);
        if (!updated.IsSuccess)
        {
            return Problems.From(updated.Error);
        }

        if (request.Disabled == true)
        {
            // A disabled account loses its open connections too, not only its sessions.
            await events.RevokeEverywhereAsync(updated.Value.Id).ConfigureAwait(false);
        }

        return TypedResults.Ok(AccountOf(updated.Value));
    }

    private static AccountItem AccountOf(AccountRecord a) => new(a.Id, a.DisplayName, a.IsServerAdmin, a.Disabled, a.CreatedAt);
}
