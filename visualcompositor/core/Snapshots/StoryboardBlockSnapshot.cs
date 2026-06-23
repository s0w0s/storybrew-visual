using VisualCompositor.Core.Primitives;

namespace VisualCompositor.Core.Snapshots;

/// <summary>Snapshot of a StoryboardBlock (Loop/Trigger). Transaction-persistent, undo-before-state.
/// Captures the full state of a block before an expand operation.</summary>
public sealed class StoryboardBlockSnapshot
{
    public string BlockId { get; init; } = string.Empty;

    public string LayerId { get; init; } = string.Empty;

    /// <summary>The header command id (L/T command).</summary>
    public string HeaderCommandId { get; init; } = string.Empty;

    public BlockEditAccess EditAccess { get; init; }

    public BlockMaterializationState MaterializationState { get; init; }

    /// <summary>Block type: "Loop" or "Trigger".</summary>
    public string BlockType { get; init; } = string.Empty;

    /// <summary>For Loop: start time. For Trigger: trigger start time.</summary>
    public double StartTime { get; init; }

    /// <summary>For Loop: loop count. For Trigger: trigger end time.</summary>
    public double EndTimeOrCount { get; init; }

    /// <summary>For Trigger: the trigger name (e.g. "HitObjects").</summary>
    public string? TriggerName { get; init; }

    /// <summary>For Trigger: the trigger group number.</summary>
    public int? TriggerGroup { get; init; }

    /// <summary>Relative commands inside this block.</summary>
    public List<RelativeCommandSnapshot> RelativeCommands { get; init; } = new();

    public StoryboardBlockSnapshot DeepClone() => new()
    {
        BlockId = BlockId,
        LayerId = LayerId,
        HeaderCommandId = HeaderCommandId,
        EditAccess = EditAccess,
        MaterializationState = MaterializationState,
        BlockType = BlockType,
        StartTime = StartTime,
        EndTimeOrCount = EndTimeOrCount,
        TriggerName = TriggerName,
        TriggerGroup = TriggerGroup,
        RelativeCommands = RelativeCommands.ConvertAll(c => c.DeepClone()),
    };
}
