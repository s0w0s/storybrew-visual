using VisualCompositor.Core.Primitives;

namespace VisualCompositor.Core.Snapshots;

/// <summary>Snapshot of a RawBlock's anchor state. Transaction-persistent, undo/redo anchor state.</summary>
public sealed class RawBlockAnchorSnapshot
{
    public string RawBlockId { get; init; } = string.Empty;

    public RawBlockAnchorKind AnchorKind { get; init; }

    /// <summary>For AfterCommand: the command id this raw block follows. For LayerStart: null.</summary>
    public string? AnchorCommandId { get; init; }

    /// <summary>The layer this raw block belongs to.</summary>
    public string LayerId { get; init; } = string.Empty;

    public RawBlockAnchorSnapshot DeepClone() => new()
    {
        RawBlockId = RawBlockId,
        AnchorKind = AnchorKind,
        AnchorCommandId = AnchorCommandId,
        LayerId = LayerId,
    };
}
