using VisualCompositor.Core.Primitives;

namespace VisualCompositor.Core.Model;

/// <summary>Base class for Loop/Trigger blocks. First-class structures, not expanded by default.</summary>
public abstract class StoryboardBlock
{
    /// <summary>Opaque persistent id. Hash only for initial import allocation; never recomputed after save.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The header command id (L/T command). Must have a corresponding CommandRecord.</summary>
    public string HeaderCommandId { get; set; } = string.Empty;

    public BlockEditAccess EditAccess { get; set; } = BlockEditAccess.BlockEditable;

    public BlockMaterializationState MaterializationState { get; set; } = BlockMaterializationState.PreservedBlock;

    public string LayerId { get; set; } = string.Empty;

    public abstract string BlockType { get; }
}

/// <summary>Loop block (L command). First-class, not expanded by default.</summary>
public sealed class LoopBlock : StoryboardBlock
{
    public override string BlockType => "Loop";

    public double StartTime { get; set; }

    public int LoopCount { get; set; }

    public List<RelativeCommand> RelativeCommands { get; set; } = new();

    public LoopBlock Clone() => new()
    {
        Id = Id,
        HeaderCommandId = HeaderCommandId,
        EditAccess = EditAccess,
        MaterializationState = MaterializationState,
        LayerId = LayerId,
        StartTime = StartTime,
        LoopCount = LoopCount,
        RelativeCommands = RelativeCommands.ConvertAll(c => c.Clone()),
    };
}

/// <summary>Trigger block (T command). First-class, not expanded by default.</summary>
public sealed class TriggerBlock : StoryboardBlock
{
    public override string BlockType => "Trigger";

    public string TriggerName { get; set; } = string.Empty;

    public double StartTime { get; set; }

    public double EndTime { get; set; }

    public int Group { get; set; }

    public List<RelativeCommand> RelativeCommands { get; set; } = new();

    public TriggerBlock Clone() => new()
    {
        Id = Id,
        HeaderCommandId = HeaderCommandId,
        EditAccess = EditAccess,
        MaterializationState = MaterializationState,
        LayerId = LayerId,
        TriggerName = TriggerName,
        StartTime = StartTime,
        EndTime = EndTime,
        Group = Group,
        RelativeCommands = RelativeCommands.ConvertAll(c => c.Clone()),
    };
}
