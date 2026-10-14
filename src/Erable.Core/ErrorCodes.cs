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
}
