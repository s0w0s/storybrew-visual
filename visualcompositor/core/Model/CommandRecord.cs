using VisualCompositor.Core.Primitives;
using VisualCompositor.Core.Snapshots;

namespace VisualCompositor.Core.Model;

/// <summary>Records the lifecycle and provenance of a command.
/// Loop/Trigger header must have a HeaderCommandId with a corresponding CommandRecord.
/// RelativeCommand.Id must equal its CommandRecord.CommandId.</summary>
public sealed class CommandRecord
{
    /// <summary>The command id. Must match the command's Id.</summary>
    public string CommandId { get; set; } = string.Empty;

    public CommandRecordLifecycle Lifecycle { get; set; } = CommandRecordLifecycle.ImportedUnchanged;

    /// <summary>Ids of commands generated from this record during expand (provenance, NOT undo-delete scope).
    /// Per §12: HeaderCommandRecord.DerivedCommandIds == disjoint union of non-header split records' DerivedCommandIds.</summary>
    public List<string> DerivedCommandIds { get; set; } = new();

    /// <summary>Parent block id (null for layer-level / sprite-level commands).</summary>
    public string? ParentBlockId { get; set; }

    /// <summary>Parent layer id.</summary>
    public string ParentLayerId { get; set; } = string.Empty;

    /// <summary>Provenance reference to original .osb source.</summary>
    public SourceReferenceSnapshot? SourceReference { get; set; }

    public CommandRecord Clone() => new()
    {
        CommandId = CommandId,
        Lifecycle = Lifecycle,
        DerivedCommandIds = new List<string>(DerivedCommandIds),
        ParentBlockId = ParentBlockId,
        ParentLayerId = ParentLayerId,
        SourceReference = SourceReference?.DeepClone(),
    };
}
