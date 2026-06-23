using VisualCompositor.Core.Model;

namespace VisualCompositor.State;

/// <summary>Immutable editor state. Reducer produces new states, never mutates.</summary>
public sealed class EditorState
{
    public CompositionDocument Document { get; init; } = new();
    public long Revision { get; init; }
    public List<string> SelectedSpriteIds { get; init; } = new();
    public List<string> SelectedLayerIds { get; init; } = new();
    public double CurrentTime { get; init; }
    public double ViewportZoom { get; init; } = 1.0;
    public Core.Primitives.Vector2 ViewportPan { get; init; }
    public bool IsDirty { get; init; }

    // Undo/Redo stacks store serialized document snapshots + action descriptions
    public List<HistoryEntry> UndoStack { get; init; } = new();
    public List<HistoryEntry> RedoStack { get; init; } = new();

    public static EditorState Initial => new() { Document = new CompositionDocument() };
}

public sealed class HistoryEntry
{
    public string ActionDescription { get; init; } = string.Empty;
    public string DocumentJson { get; init; } = string.Empty;
    public long Revision { get; init; }
    // For ExpandBlock transactions, store the transaction for undo/redo replay
    public Core.Validation.ExpandBlockTransaction? ExpandTransaction { get; init; }
}
