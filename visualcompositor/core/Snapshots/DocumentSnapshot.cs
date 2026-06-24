namespace VisualCompositor.Core.Snapshots;

/// <summary>Execution-scoped rollback state. Captured at the start of any undo/redo/edit attempt.
/// NEVER enters a transaction, NEVER enters undo/redo stack. Released after success or rollback.</summary>
public sealed class DocumentSnapshot
{
    /// <summary>Deep-cloned layers state (serialized form for rollback).</summary>
    public string DocumentJson { get; init; } = string.Empty;

    /// <summary>Revision number at capture time.</summary>
    public long Revision { get; init; }

    public static DocumentSnapshot Capture(string documentJson, long revision) => new()
    {
        DocumentJson = documentJson,
        Revision = revision,
    };
}
