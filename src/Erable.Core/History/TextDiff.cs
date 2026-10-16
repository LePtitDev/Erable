using DiffPlex;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;

namespace Erable.Core.History;

/// <summary>How a row of a text diff differs between the two versions.</summary>
public enum DiffRowKind
{
    Unchanged,

    /// <summary>The line only exists in the new text.</summary>
    Added,

    /// <summary>The line only exists in the old text.</summary>
    Removed,

    /// <summary>The line exists in both texts with different words (see the pieces).</summary>
    Modified,
}

/// <summary>A run of a line; <see cref="Changed"/> when the words differ from the other version.</summary>
public sealed record DiffPiece(string Text, bool Changed);

/// <summary>A line of one version: its number (from 1) and its pieces.</summary>
public sealed record DiffLine(int Number, string Text, IReadOnlyList<DiffPiece> Pieces);

/// <summary>One row of the side by side diff; <see cref="Old"/> is null for an added line, <see cref="New"/> for a removed one.</summary>
public sealed record DiffRow(DiffRowKind Kind, DiffLine? Old, DiffLine? New);

/// <summary>
/// Diff of two texts, line by line and word by word inside a modified line (F13.R2). A thin layer over DiffPlex that
/// returns plain records: the rows rebuild both texts exactly, which the tests check. M4-05 builds on it.
/// </summary>
public static class TextDiff
{
    public static IReadOnlyList<DiffRow> Compare(string? oldText, string? newText)
    {
        var model = new SideBySideDiffBuilder(new Differ()).BuildDiffModel(oldText ?? string.Empty, newText ?? string.Empty, ignoreWhitespace: false);
        var rows = new List<DiffRow>(model.OldText.Lines.Count);
        for (var i = 0; i < model.OldText.Lines.Count; i++)
        {
            var old = model.OldText.Lines[i];
            var current = model.NewText.Lines[i];
            var kind = (old.Type, current.Type) switch
            {
                (ChangeType.Unchanged, ChangeType.Unchanged) => DiffRowKind.Unchanged,
                (ChangeType.Imaginary, _) => DiffRowKind.Added,
                (_, ChangeType.Imaginary) => DiffRowKind.Removed,
                _ => DiffRowKind.Modified,
            };
            rows.Add(new DiffRow(kind, LineOf(old), LineOf(current)));
        }

        return rows;
    }

    private static DiffLine? LineOf(DiffPlex.DiffBuilder.Model.DiffPiece piece)
    {
        if (piece.Type == ChangeType.Imaginary || piece.Position is not { } number)
        {
            return null;
        }

        var text = piece.Text ?? string.Empty;
        IReadOnlyList<DiffPiece> pieces = piece.SubPieces.Count == 0
            ? [new DiffPiece(text, piece.Type != ChangeType.Unchanged)]
            : [.. piece.SubPieces
                .Where(p => p.Type != ChangeType.Imaginary)
                .Select(p => new DiffPiece(p.Text ?? string.Empty, p.Type != ChangeType.Unchanged))];
        return new DiffLine(number, text, pieces);
    }
}
