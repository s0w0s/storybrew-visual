using VisualCompositor.Core.Snapshots;

namespace VisualCompositor.Core.Validation;

/// <summary>Atomic expand-to-keyframes transaction. Per §10 + Freeze Patch §1.
/// GeneratedCommandIds is the undo-delete authoritative list.
/// GeneratedCommandsAfter is the redo-replay authoritative payload (redo does NOT recompute).</summary>
public sealed class ExpandBlockTransaction
{
    public string TransactionId { get; init; } = string.Empty;
    public string LayerId { get; init; } = string.Empty;

    public StoryboardBlockSnapshot OriginalBlockSnapshot { get; init; } = new();
    public BlockInsertionAnchor InsertionAnchor { get; init; } = new();

    /// <summary>History entry created by this expansion.</summary>
    public ExpandedBlockHistoryRef CreatedHistory { get; init; } = new();

    /// <summary>Undo-delete authoritative list of generated command ids.</summary>
    public List<string> GeneratedCommandIds { get; init; } = new();

    /// <summary>Redo-replay authoritative payload.</summary>
    public List<GeneratedCommandSnapshot> GeneratedCommandsAfter { get; init; } = new();

    public List<CommandRecordSnapshot> AffectedCommandRecordsBefore { get; init; } = new();
    public List<CommandRecordSnapshot> AffectedCommandRecordsAfter { get; init; } = new();

    public List<RawBlockAnchorSnapshot> RawBlockAnchorsBefore { get; init; } = new();
    public List<RawBlockAnchorSnapshot> RawBlockAnchorsAfter { get; init; } = new();
}

/// <summary>Lightweight reference to an ExpandedBlockHistory entry.</summary>
public sealed class ExpandedBlockHistoryRef
{
    public string ExpandedBlockId { get; init; } = string.Empty;
    public string TransactionId { get; init; } = string.Empty;
}
