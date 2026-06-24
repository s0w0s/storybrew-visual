namespace VisualCompositor.Core.Snapshots;

/// <summary>Transaction-level positioning data for inserting a block back during undo.
/// Per Freeze Patch §1: required by §11 undo insertion step.</summary>
public sealed class BlockInsertionAnchor
{
    public string LayerId { get; init; } = string.Empty;

    /// <summary>If present and exists in target layer, insert after this block.</summary>
    public string? PreviousSiblingBlockId { get; init; }

    /// <summary>If PreviousSibling not found, and this exists in target layer, insert before this block.</summary>
    public string? NextSiblingBlockId { get; init; }

    /// <summary>Fallback: clamp to current collection range. Emits UNDO_INSERTION_ANCHOR_FALLBACK warning.</summary>
    public int OriginalIndexFallback { get; init; }

    public BlockInsertionAnchor DeepClone() => new()
    {
        LayerId = LayerId,
        PreviousSiblingBlockId = PreviousSiblingBlockId,
        NextSiblingBlockId = NextSiblingBlockId,
        OriginalIndexFallback = OriginalIndexFallback,
    };
}
