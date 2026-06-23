using VisualCompositor.Core.Model;
using VisualCompositor.Core.Primitives;
using VisualCompositor.Core.Validation;

namespace VisualCompositor.Osb.Export;

/// <summary>Places <see cref="RawBlock"/>s back into a layer's output lines according to their
/// anchors (design document §7, line 184). Handles SubTask 27.4.
/// <list type="bullet">
/// <item><see cref="RawBlockAnchorKind.LayerStart"/>: insert at the beginning of the layer content.</item>
/// <item><see cref="RawBlockAnchorKind.AfterCommand"/>: insert immediately after the command line
///   for <see cref="RawBlock.AnchorCommandId"/>.</item>
/// <item>If the anchor command is not present in the output: rebase to the nearest surviving
///   tagged command in the same scope (by SourceLine), then fall back to scope end.</item>
/// </list>
/// Only truly orphaned RawBlocks (layer does not exist) produce <c>RAW_ANCHOR_ORPHANED</c> warnings;
/// those are handled by the caller (<see cref="OsbExportCompiler"/>).</summary>
internal sealed class RawBlockPlacer
{
    private readonly Dictionary<string, CommandRecord> _commandRecords;
    private readonly List<Diagnostic> _diagnostics;

    public RawBlockPlacer(Dictionary<string, CommandRecord> commandRecords, List<Diagnostic> diagnostics)
    {
        _commandRecords = commandRecords;
        _diagnostics = diagnostics;
    }

    /// <summary>Interleave RawBlocks into the layer's output lines according to their anchors.
    /// <paramref name="rawBlocks"/> should be the RawBlocks belonging to this layer, in document order.</summary>
    public List<string> Place(List<OutputLine> outputLines, List<RawBlock> rawBlocks)
    {
        if (rawBlocks.Count == 0)
            return outputLines.ConvertAll(l => l.Text);

        // Build a map from command id -> output line index, for tagged lines only.
        var commandIndex = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < outputLines.Count; i++)
        {
            if (!string.IsNullOrEmpty(outputLines[i].CommandId))
                commandIndex[outputLines[i].CommandId!] = i;
        }

        // Build a list of (sourceLine, outputIndex) for all tagged commands, for rebase lookups.
        var taggedBySourceLine = new List<(int SourceLine, int OutputIndex)>();
        foreach (var kvp in commandIndex)
        {
            if (_commandRecords.TryGetValue(kvp.Key, out var rec) &&
                rec.SourceReference?.SourceLine is int sl)
            {
                taggedBySourceLine.Add((sl, kvp.Value));
            }
        }
        taggedBySourceLine.Sort((a, b) => a.SourceLine.CompareTo(b.SourceLine));

        // Group RawBlocks by insertion target.
        var atStart = new List<RawBlock>();
        var afterLine = new Dictionary<int, List<RawBlock>>(); // output line index -> raw blocks
        var atEnd = new List<RawBlock>();

        foreach (var raw in rawBlocks)
        {
            if (raw.AnchorKind == RawBlockAnchorKind.LayerStart)
            {
                atStart.Add(raw);
            }
            else // AfterCommand
            {
                var anchorId = raw.AnchorCommandId;
                if (anchorId != null && commandIndex.TryGetValue(anchorId, out var idx))
                {
                    AddToBucket(afterLine, idx, raw);
                }
                else
                {
                    // Anchor command not in output: rebase to nearest surviving tagged command.
                    var rebasedIndex = FindNearestTaggedCommand(anchorId, taggedBySourceLine);
                    if (rebasedIndex >= 0)
                        AddToBucket(afterLine, rebasedIndex, raw);
                    else
                        atEnd.Add(raw);
                }
            }
        }

        // Build the final list of lines.
        var result = new List<string>(outputLines.Count + rawBlocks.Count);

        // LayerStart raw blocks first (in document order).
        foreach (var raw in atStart)
            result.Add(raw.Content);

        // Output lines with interleaved AfterCommand raw blocks.
        for (var i = 0; i < outputLines.Count; i++)
        {
            result.Add(outputLines[i].Text);
            if (afterLine.TryGetValue(i, out var bucket))
                foreach (var raw in bucket)
                    result.Add(raw.Content);
        }

        // Scope-end raw blocks last (in document order).
        foreach (var raw in atEnd)
            result.Add(raw.Content);

        return result;
    }

    private static void AddToBucket(Dictionary<int, List<RawBlock>> buckets, int idx, RawBlock raw)
    {
        if (!buckets.TryGetValue(idx, out var list))
        {
            list = new List<RawBlock>();
            buckets[idx] = list;
        }
        list.Add(raw);
    }

    /// <summary>Find the nearest surviving tagged command (by SourceLine) to the anchor command.
    /// Returns the output line index of the nearest tagged command, or -1 if none found.</summary>
    private int FindNearestTaggedCommand(string? anchorId, List<(int SourceLine, int OutputIndex)> tagged)
    {
        if (tagged.Count == 0)
            return -1;

        // Get the anchor command's SourceLine.
        int? anchorSourceLine = null;
        if (anchorId != null && _commandRecords.TryGetValue(anchorId, out var rec))
            anchorSourceLine = rec.SourceReference?.SourceLine;

        if (anchorSourceLine == null)
            return -1;

        var target = anchorSourceLine.Value;
        var best = -1;
        var bestDiff = int.MaxValue;
        foreach (var (sl, idx) in tagged)
        {
            var diff = Math.Abs(sl - target);
            if (diff < bestDiff || (diff == bestDiff && sl <= target))
            {
                bestDiff = diff;
                best = idx;
            }
        }
        return best;
    }
}
