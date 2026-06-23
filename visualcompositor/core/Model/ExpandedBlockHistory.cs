using VisualCompositor.Core.Snapshots;

namespace VisualCompositor.Core.Model;

/// <summary>History entry for a block that was expanded to keyframes.
/// ExpandedBlockId is a historical reference, NOT an active foreign key.
/// Used for audit, comment export, and future restore tools. Not for undo after file reopen.</summary>
public sealed class ExpandedBlockHistory
{
    /// <summary>Historical reference to the expanded block's id. Not an active foreign key.</summary>
    public string ExpandedBlockId { get; set; } = string.Empty;

    /// <summary>Snapshot of the original block before expansion.</summary>
    public StoryboardBlockSnapshot Snapshot { get; set; } = new();

    /// <summary>The transaction id that performed the expansion.</summary>
    public string TransactionId { get; set; } = string.Empty;

    /// <summary>When the expansion occurred.</summary>
    public DateTimeOffset Timestamp { get; set; }

    public ExpandedBlockHistory Clone() => new()
    {
        ExpandedBlockId = ExpandedBlockId,
        Snapshot = Snapshot.DeepClone(),
        TransactionId = TransactionId,
        Timestamp = Timestamp,
    };
}
