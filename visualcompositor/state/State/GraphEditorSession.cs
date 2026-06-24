using VisualCompositor.Core.Model.GraphEditor;

namespace VisualCompositor.State;

/// <summary>State for the graph editor panel. Holds the view state (<see cref="EditorState"/>)
/// and the editable graph tracks. This is a parallel state object separate from the main
/// <c>EditorState</c> — the graph editor has its own lightweight state to avoid bloating the
/// document undo/redo stack with view-only changes (zoom, scroll, selection).</summary>
public sealed class GraphEditorSession
{
    /// <summary>View/editor state for the graph editor panel.</summary>
    public GraphEditorState EditorState { get; init; } = new();

    /// <summary>Editable graph tracks (one per property component).</summary>
    public List<GraphTrack> Tracks { get; init; } = new();

    /// <summary>Deep clone.</summary>
    public GraphEditorSession Clone() => new()
    {
        EditorState = EditorState.Clone(),
        Tracks = Tracks.Select(t => t.Clone()).ToList(),
    };
}
