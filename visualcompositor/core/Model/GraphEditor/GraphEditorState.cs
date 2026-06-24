namespace VisualCompositor.Core.Model.GraphEditor;

/// <summary>View/editor state for the graph editor panel. This is separate from the document-level
/// <c>EditorState</c> to avoid bloating undo/redo history with view-only changes (zoom, scroll).
/// The graph editor maintains its own lightweight state.</summary>
public sealed class GraphEditorState
{
    /// <summary>Id of the track currently being edited (null if none).</summary>
    public string? ActiveTrackId { get; set; }

    /// <summary>Ids of tracks visible in the graph editor.</summary>
    public HashSet<string> VisibleTrackIds { get; set; } = new();

    /// <summary>Horizontal (time) zoom level. Default 1.0.</summary>
    public float TimeZoom { get; set; } = 1.0f;

    /// <summary>Vertical (value) zoom level. Default 1.0.</summary>
    public float ValueZoom { get; set; } = 1.0f;

    /// <summary>Horizontal scroll position in milliseconds.</summary>
    public double TimeScroll { get; set; }

    /// <summary>Vertical scroll position in value space.</summary>
    public float ValueScroll { get; set; }

    /// <summary>Indices of selected keyframes in the active track.</summary>
    public List<int> SelectedKeyframeIndices { get; set; } = new();

    /// <summary>Deep clone.</summary>
    public GraphEditorState Clone() => new()
    {
        ActiveTrackId = ActiveTrackId,
        VisibleTrackIds = new HashSet<string>(VisibleTrackIds),
        TimeZoom = TimeZoom,
        ValueZoom = ValueZoom,
        TimeScroll = TimeScroll,
        ValueScroll = ValueScroll,
        SelectedKeyframeIndices = new List<int>(SelectedKeyframeIndices),
    };
}
