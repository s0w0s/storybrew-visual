using VisualCompositor.Core.Model.GraphEditor;

namespace VisualCompositor.State.Actions;

/// <summary>Actions for the graph editor panel. These operate on <see cref="GraphEditorSession"/>,
/// which is a parallel state object separate from the main <c>EditorState</c> (to avoid bloating
/// the document undo/redo stack with view-only changes). These actions do not extend
/// <c>EditorAction</c> — they are dispatched to the <c>GraphEditorReducer</c> only.</summary>

/// <summary>Set which track is active in the graph editor (null to deselect).</summary>
public record SetGraphActiveTrackAction(string? TrackId);

/// <summary>Toggle visibility of a track in the graph editor.</summary>
public record SetGraphTrackVisibilityAction(string TrackId, bool Visible);

/// <summary>Set zoom levels (null to leave unchanged).</summary>
public record SetGraphZoomAction(float? TimeZoom, float? ValueZoom);

/// <summary>Set scroll positions (null to leave unchanged).</summary>
public record SetGraphScrollAction(double? TimeScroll, float? ValueScroll);

/// <summary>Select keyframes in the active track by index.</summary>
public record SelectGraphKeyframesAction(List<int> Indices);

/// <summary>Add a keyframe to a graph track.</summary>
public record AddGraphKeyframeAction(string TrackId, GraphKeyframe Keyframe);

/// <summary>Remove a keyframe from a graph track by index.</summary>
public record RemoveGraphKeyframeAction(string TrackId, int Index);

/// <summary>Update a keyframe in a graph track by index.</summary>
public record UpdateGraphKeyframeAction(string TrackId, int Index, GraphKeyframe Keyframe);

/// <summary>Set the tangent mode of a keyframe (Auto/Linear/Smooth/Broken/Stepped).</summary>
public record SetKeyframeTangentModeAction(string TrackId, int Index, TangentMode Mode);
