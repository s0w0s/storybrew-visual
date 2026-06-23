using VisualCompositor.Core.Primitives;

namespace VisualCompositor.Core.Snapshots;

/// <summary>Snapshot of a CommandRecord. Transaction/serialization persistent.</summary>
public sealed class CommandRecordSnapshot
{
    public string CommandId { get; init; } = string.Empty;

    public CommandRecordLifecycle Lifecycle { get; init; }

    /// <summary>Ids of commands generated from this record during expand operations (provenance, not undo-delete scope).</summary>
    public List<string> DerivedCommandIds { get; init; } = new();

    /// <summary>Parent block id (null for layer-level commands).</summary>
    public string? ParentBlockId { get; init; }

    /// <summary>Parent layer id.</summary>
    public string ParentLayerId { get; init; } = string.Empty;

    /// <summary>Provenance reference to original source.</summary>
    public SourceReferenceSnapshot? SourceReference { get; init; }

    public CommandRecordSnapshot DeepClone() => new()
    {
        CommandId = CommandId,
        Lifecycle = Lifecycle,
        DerivedCommandIds = new List<string>(DerivedCommandIds),
        ParentBlockId = ParentBlockId,
        ParentLayerId = ParentLayerId,
        SourceReference = SourceReference?.DeepClone(),
    };
}
