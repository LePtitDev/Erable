namespace Erable.Core;

/// <summary>
/// Stable business error codes (<see cref="DomainError.Code"/>). The SPA translates them (<c>errors.&lt;code&gt;</c>):
/// never rename one without updating the translations.
/// </summary>
public static class ErrorCodes
{
    public const string NameEmpty = "element.name.empty";
    public const string NameTooLong = "element.name.tooLong";
    public const string NameInvalid = "element.name.invalid";

    public const string ElementNotFound = "element.notFound";
    public const string ParentNotFound = "tree.parent.notFound";
    public const string IdAlreadyUsed = "element.id.alreadyUsed";

    /// <summary>The four roots cannot be renamed, moved or deleted (F02.R1).</summary>
    public const string RootImmutable = "tree.root.immutable";

    /// <summary>An element stays under its own root (F02.R2).</summary>
    public const string MoveCrossRoot = "tree.move.crossRoot";

    /// <summary>An element cannot go under itself or one of its descendants (F02.R3).</summary>
    public const string MoveIntoDescendant = "tree.move.intoDescendant";

    /// <summary>The target cannot contain this kind of element (e.g. a folder under a page).</summary>
    public const string InvalidParent = "tree.parent.invalid";

    /// <summary>Attachments belong to their page and are not moved through the tree.</summary>
    public const string NotMovable = "tree.move.notMovable";

    /// <summary>The element kind cannot be created through this operation.</summary>
    public const string KindNotCreatable = "element.kind.notCreatable";

    /// <summary>The file exceeds the configured maximum attachment size (F06.R5).</summary>
    public const string AttachmentTooLarge = "attachment.tooLarge";

    /// <summary>The project has used the room the server grants to its attachments (M4-06).</summary>
    public const string AttachmentQuotaExceeded = "attachment.quotaExceeded";

    /// <summary>The chosen parent category does not exist.</summary>
    public const string CategoryParentNotFound = "category.parent.notFound";

    /// <summary>The chosen parent would create an inheritance loop (F07.R2); the loop is in the <c>loop</c> extension.</summary>
    public const string CategoryCycle = "category.parent.cycle";

    /// <summary>Not one of the property types of docs/architecture.md §5.4.</summary>
    public const string PropertyTypeInvalid = "property.type.invalid";

    /// <summary>A value does not match the type of its property (refused, unlike rule violations which only warn).</summary>
    public const string PropertyValueInvalid = "property.value.invalid";

    /// <summary>A rule is malformed or not available for the type (F08.R2).</summary>
    public const string PropertyRuleInvalid = "property.rule.invalid";

    public const string PropertyKeyInvalid = "property.key.invalid";

    /// <summary>Another property already uses this key.</summary>
    public const string PropertyKeyTaken = "property.key.taken";

    public const string PropertyOptionInvalid = "property.option.invalid";

    public const string PropertyDescriptionTooLong = "property.description.tooLong";

    /// <summary>A value cannot be converted to the new type (F08.R6).</summary>
    public const string PropertyValueNotConvertible = "property.value.notConvertible";

    /// <summary>An enum option still used by values was removed without a replacement (F08.R5); pages in <c>pages</c>.</summary>
    public const string PropertyOptionInUse = "property.option.inUse";

    /// <summary>A category given to a page does not exist (or is in the trash).</summary>
    public const string PageCategoryInvalid = "page.category.invalid";

    /// <summary>A value is given for a property that does not exist (or is in the trash).</summary>
    public const string PropertyNotFound = "property.notFound";

    /// <summary>The description of a link type is too long.</summary>
    public const string LinkTypeDescriptionTooLong = "link.type.description.tooLong";

    /// <summary>The inverse label of a link type is too long or malformed.</summary>
    public const string LinkTypeInverseInvalid = "link.type.inverse.invalid";

    /// <summary>The link type does not exist (or is in the trash).</summary>
    public const string LinkTypeNotFound = "link.type.notFound";

    /// <summary>An end of a link is not a live page.</summary>
    public const string LinkPageInvalid = "link.page.invalid";

    /// <summary>A page cannot be linked to itself.</summary>
    public const string LinkSelf = "link.self";

    /// <summary>The same link (source, type, target) already exists.</summary>
    public const string LinkDuplicate = "link.duplicate";

    /// <summary>The link does not exist.</summary>
    public const string LinkNotFound = "link.notFound";

    /// <summary>The entry is not (or no longer) in the trash.</summary>
    public const string TrashEntryNotFound = "trash.entry.notFound";

    /// <summary>An attachment cannot come back while its page is in the trash: restore the page first.</summary>
    public const string TrashParentDeleted = "trash.parent.deleted";

    /// <summary>A property condition of a search is malformed, or its operator does not apply to the property.</summary>
    public const string SearchConditionInvalid = "search.condition.invalid";

    /// <summary>The cursor of a search page is not one this server issued.</summary>
    public const string SearchCursorInvalid = "search.cursor.invalid";

    /// <summary>The element has no such revision.</summary>
    public const string RevisionNotFound = "revision.notFound";

    /// <summary>Only the revisions of a live page can be restored.</summary>
    public const string RevisionNotRestorable = "revision.notRestorable";

    // ─── Accounts and access (M4-01) ────────────────────────────────────────

    public const string AccountLoginInvalid = "account.login.invalid";
    public const string AccountNameInvalid = "account.name.invalid";
    public const string AccountPasswordInvalid = "account.password.invalid";

    /// <summary>Another account already has this login.</summary>
    public const string AccountLoginTaken = "account.login.taken";

    public const string AccountNotFound = "account.notFound";

    /// <summary>The project must keep at least one administrator (M4-08).</summary>
    public const string MemberLastAdmin = "member.lastAdmin";

    /// <summary>The server must keep at least one active administrator.</summary>
    public const string AccountLastAdmin = "account.lastAdmin";

    public const string MemberNotFound = "member.notFound";
    public const string MemberRoleInvalid = "member.role.invalid";
}
