namespace Erable.Core.Elements;

/// <summary>Type of an element; all kinds share one ID space (docs/architecture.md §4, §5.1).</summary>
public enum ElementKind
{
    /// <summary>One of the four fixed roots of the explorer (F02.R1).</summary>
    Root,

    /// <summary>Only used to file elements of its root's kind.</summary>
    Folder,

    Page,
    Category,
    PropertyDefinition,
    LinkType,

    /// <summary>
    /// File attached to a page (decision P-02): an element of the shared ID space whose parent is its page.
    /// It is not shown in the explorer and is never moved through the tree.
    /// </summary>
    Attachment,
}
