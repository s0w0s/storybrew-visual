using VisualCompositor.Core.Primitives;

namespace VisualCompositor.Core.Model;

/// <summary>Preserves unknown/comment/blank lines from .osb. Does NOT produce a CommandRecord.
/// Anchor: LayerStart (AnchorCommandId==null) or AfterCommand (AnchorCommandId!=null and exists in commandRecords).</summary>
public sealed class RawBlock
{
    public string Id { get; set; } = string.Empty;

    /// <summary>The raw text content (unknown line, comment, blank line, etc.).</summary>
    public string Content { get; set; } = string.Empty;

    public RawBlockAnchorKind AnchorKind { get; set; }

    /// <summary>For AfterCommand: the command id this raw block follows. For LayerStart: null.</summary>
    public string? AnchorCommandId { get; set; }

    public string LayerId { get; set; } = string.Empty;

    public RawBlock Clone() => new()
    {
        Id = Id,
        Content = Content,
        AnchorKind = AnchorKind,
        AnchorCommandId = AnchorCommandId,
        LayerId = LayerId,
    };
}
