using System.Text;
using System.Text.RegularExpressions;
using DiffPlex;

namespace Erable.Core.Merge;

/// <summary>A part of a merged text.</summary>
public abstract record MergeBlock;

/// <summary>Text both sides agree on (or that only one side touched), line breaks included.</summary>
public sealed record StableBlock(string Text) : MergeBlock;

/// <summary>A place where both sides changed the same text differently: the three versions, to be settled by a person.</summary>
public sealed record ConflictBlock(string Base, string Mine, string Theirs) : MergeBlock;

/// <summary>The result of merging two edits of one text: stable parts and conflicts, in order.</summary>
public sealed record TextMerge(IReadOnlyList<MergeBlock> Blocks)
{
    public bool HasConflicts => Blocks.Any(b => b is ConflictBlock);

    /// <summary>The merged text when nothing is in conflict; with conflicts, the stable parts and the version chosen by <paramref name="choose"/>.</summary>
    public string Resolve(Func<ConflictBlock, string> choose)
    {
        ArgumentNullException.ThrowIfNull(choose);
        var text = new StringBuilder();
        foreach (var block in Blocks)
        {
            text.Append(block switch
            {
                StableBlock stable => stable.Text,
                ConflictBlock conflict => choose(conflict),
                _ => throw new InvalidOperationException("Unknown block."),
            });
        }

        return text.ToString();
    }

    /// <summary>The merged text of a merge without conflict.</summary>
    public string Text => HasConflicts
        ? throw new InvalidOperationException("The merge has conflicts: choose a version of each with Resolve.")
        : Resolve(_ => string.Empty);
}

/// <summary>
/// Three-way merge of a text (F14.R5): <c>base</c> is the version both editors started from, <c>mine</c>
/// the text being saved and <c>theirs</c> what was saved meanwhile. It is the classic <i>diff3</i>: the two edits are
/// computed against the base, and the places where they do not meet are merged as they are. Where they meet — both
/// changed the same lines, or lines that touch — the three versions are compared again <b>word by word</b>; only when
/// the same words were changed differently does a conflict remain. Nothing is ever dropped silently: a text that is
/// neither kept nor reported in a conflict does not exist (property-tested).
/// </summary>
/// <remarks>
/// The differences themselves come from DiffPlex (Myers, the algorithm of <c>diff</c>); the merge on top is ours
/// because its properties — <c>merge(b, b, x) = x</c>, <c>merge(b, x, b) = x</c>, disjoint edits never conflict — are
/// the ones the product depends on.
/// </remarks>
public static partial class ThreeWayMerge
{
    /// <summary>Merges line by line, then word by word inside the lines both sides changed.</summary>
    public static TextMerge Merge(string baseText, string mine, string theirs)
    {
        ArgumentNullException.ThrowIfNull(baseText);
        ArgumentNullException.ThrowIfNull(mine);
        ArgumentNullException.ThrowIfNull(theirs);
        var blocks = MergeSequences(Lines(baseText), Lines(mine), Lines(theirs), refine: true);
        return new TextMerge(Coalesce(blocks));
    }

    // ─── Sequences ──────────────────────────────────────────────────────────

    /// <summary>One edit of the base: <c>BaseLength</c> elements from <c>BaseStart</c> became <c>Length</c> from <c>Start</c>.</summary>
    private readonly record struct Hunk(int BaseStart, int BaseLength, int Start, int Length)
    {
        public int BaseEnd => BaseStart + BaseLength;
    }

    private static List<MergeBlock> MergeSequences(string[] baseSeq, string[] mine, string[] theirs, bool refine)
    {
        var mineHunks = Hunks(baseSeq, mine);
        var theirHunks = Hunks(baseSeq, theirs);
        var blocks = new List<MergeBlock>();
        int i = 0, j = 0, cursor = 0;

        while (i < mineHunks.Count || j < theirHunks.Count)
        {
            var next = (i < mineHunks.Count, j < theirHunks.Count) switch
            {
                (true, false) => mineHunks[i],
                (false, true) => theirHunks[j],
                _ => mineHunks[i].BaseStart <= theirHunks[j].BaseStart ? mineHunks[i] : theirHunks[j],
            };

            // The cluster: every hunk of either side that overlaps or touches what has been gathered so far.
            var start = next.BaseStart;
            var end = next.BaseEnd;
            var mineInCluster = new List<Hunk>();
            var theirsInCluster = new List<Hunk>();
            while (true)
            {
                if (i < mineHunks.Count && mineHunks[i].BaseStart <= end)
                {
                    mineInCluster.Add(mineHunks[i]);
                    end = Math.Max(end, mineHunks[i].BaseEnd);
                    i++;
                }
                else if (j < theirHunks.Count && theirHunks[j].BaseStart <= end)
                {
                    theirsInCluster.Add(theirHunks[j]);
                    end = Math.Max(end, theirHunks[j].BaseEnd);
                    j++;
                }
                else
                {
                    break;
                }
            }

            if (start > cursor)
            {
                blocks.Add(new StableBlock(Join(baseSeq, cursor, start)));
            }

            var original = Join(baseSeq, start, end);
            var mineText = Apply(baseSeq, mine, mineInCluster, start, end);
            var theirText = Apply(baseSeq, theirs, theirsInCluster, start, end);
            blocks.Add(Settle(baseSeq, start, end, original, mineText, theirText, mineInCluster.Count > 0, theirsInCluster.Count > 0, refine));
            cursor = end;
        }

        if (cursor < baseSeq.Length)
        {
            blocks.Add(new StableBlock(Join(baseSeq, cursor, baseSeq.Length)));
        }

        return blocks;
    }

    /// <summary>What a cluster becomes: the side that changed it alone, a change both made, a word-level merge, or a conflict.</summary>
    private static MergeBlock Settle(
        string[] baseSeq, int start, int end, string original, string mine, string theirs, bool mineChanged, bool theirsChanged, bool refine)
    {
        _ = baseSeq;
        _ = start;
        _ = end;
        if (!theirsChanged || string.Equals(theirs, original, StringComparison.Ordinal))
        {
            return new StableBlock(mine);
        }

        if (!mineChanged || string.Equals(mine, original, StringComparison.Ordinal) || string.Equals(mine, theirs, StringComparison.Ordinal))
        {
            return new StableBlock(theirs);
        }

        if (refine && original.Length > 0)
        {
            // Both changed the same lines: perhaps not the same words.
            var words = MergeSequences(Words(original), Words(mine), Words(theirs), refine: false);
            if (words.All(b => b is StableBlock))
            {
                return new StableBlock(string.Concat(words.Cast<StableBlock>().Select(b => b.Text)));
            }
        }

        return new ConflictBlock(original, mine, theirs);
    }

    /// <summary>The text of <c>[start, end)</c> of the base once the hunks of one side are applied to it.</summary>
    private static string Apply(string[] baseSeq, string[] side, List<Hunk> hunks, int start, int end)
    {
        var text = new StringBuilder();
        var position = start;
        foreach (var hunk in hunks)
        {
            text.Append(Join(baseSeq, position, hunk.BaseStart));
            text.Append(Join(side, hunk.Start, hunk.Start + hunk.Length));
            position = hunk.BaseEnd;
        }

        text.Append(Join(baseSeq, position, end));
        return text.ToString();
    }

    private static string Join(string[] sequence, int from, int to) => from >= to ? string.Empty : string.Concat(sequence[from..to]);

    private static List<Hunk> Hunks(string[] baseSeq, string[] side)
    {
        if (baseSeq.AsSpan().SequenceEqual(side))
        {
            return [];
        }

        // The sentinel texts only name the two sequences for the chunker, which ignores their content.
        var chunker = new SequenceChunker(baseSeq, side);
        var result = new Differ().CreateDiffs("base", "side", ignoreWhiteSpace: false, ignoreCase: false, chunker);
        return [.. result.DiffBlocks.Select(b => new Hunk(b.DeleteStartA, b.DeleteCountA, b.InsertStartB, b.InsertCountB))];
    }

    /// <summary>Feeds DiffPlex with sequences already cut into pieces.</summary>
    private sealed class SequenceChunker(string[] baseSeq, string[] side) : IChunker
    {
        public IReadOnlyList<string> Chunk(string str) => str == "base" ? baseSeq : side;
    }

    /// <summary>Merges neighbouring stable blocks, so that the result is as short as possible.</summary>
    private static List<MergeBlock> Coalesce(List<MergeBlock> blocks)
    {
        var merged = new List<MergeBlock>(blocks.Count);
        foreach (var block in blocks)
        {
            if (block is StableBlock { Text.Length: 0 })
            {
                continue;
            }

            if (block is StableBlock current && merged.Count > 0 && merged[^1] is StableBlock previous)
            {
                merged[^1] = new StableBlock(previous.Text + current.Text);
            }
            else
            {
                merged.Add(block);
            }
        }

        return merged;
    }

    // ─── Pieces ─────────────────────────────────────────────────────────────

    /// <summary>Lines with their line break: the pieces joined together give the text back exactly.</summary>
    private static string[] Lines(string text)
    {
        var lines = new List<string>();
        var from = 0;
        while (from < text.Length)
        {
            var newline = text.IndexOf('\n', from);
            var to = newline < 0 ? text.Length : newline + 1;
            lines.Add(text[from..to]);
            from = to;
        }

        return [.. lines];
    }

    /// <summary>Words, runs of spaces and single punctuation marks: the pieces joined together give the text back exactly.</summary>
    private static string[] Words(string text) => [.. WordPattern().Matches(text).Select(m => m.Value)];

    [GeneratedRegex(@"[\p{L}\p{N}_]+|\s+|[^\p{L}\p{N}_\s]", RegexOptions.CultureInvariant)]
    private static partial Regex WordPattern();
}
