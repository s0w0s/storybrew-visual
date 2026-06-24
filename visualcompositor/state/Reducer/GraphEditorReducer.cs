using VisualCompositor.Core.Model;
using VisualCompositor.Core.Model.GraphEditor;
using VisualCompositor.State.Actions;

namespace VisualCompositor.State.Reducer;

/// <summary>Pure function (session, action) -> session for the graph editor panel.
/// Never mutates input state; all operations return a new <see cref="GraphEditorSession"/>.
/// Handles view-state changes (zoom, scroll, selection) and track mutations (add/remove/update
/// keyframes, tangent mode changes).</summary>
public static class GraphEditorReducer
{
    public static GraphEditorSession Reduce(GraphEditorSession session, object action)
    {
        return action switch
        {
            SetGraphActiveTrackAction a => ReduceSetActiveTrack(session, a),
            SetGraphTrackVisibilityAction a => ReduceSetTrackVisibility(session, a),
            SetGraphZoomAction a => ReduceSetZoom(session, a),
            SetGraphScrollAction a => ReduceSetScroll(session, a),
            SelectGraphKeyframesAction a => ReduceSelectKeyframes(session, a),
            AddGraphKeyframeAction a => ReduceAddKeyframe(session, a),
            RemoveGraphKeyframeAction a => ReduceRemoveKeyframe(session, a),
            UpdateGraphKeyframeAction a => ReduceUpdateKeyframe(session, a),
            SetKeyframeTangentModeAction a => ReduceSetTangentMode(session, a),
            _ => session,
        };
    }

    // ---- View-state actions ----

    private static GraphEditorSession ReduceSetActiveTrack(GraphEditorSession session, SetGraphActiveTrackAction a)
    {
        var editor = session.EditorState.Clone();
        editor.ActiveTrackId = a.TrackId;
        // Clear keyframe selection when switching tracks.
        editor.SelectedKeyframeIndices = new List<int>();
        return WithEditor(session, editor);
    }

    private static GraphEditorSession ReduceSetTrackVisibility(GraphEditorSession session, SetGraphTrackVisibilityAction a)
    {
        var editor = session.EditorState.Clone();
        editor.VisibleTrackIds = new HashSet<string>(session.EditorState.VisibleTrackIds);
        if (a.Visible)
            editor.VisibleTrackIds.Add(a.TrackId);
        else
            editor.VisibleTrackIds.Remove(a.TrackId);
        return WithEditor(session, editor);
    }

    private static GraphEditorSession ReduceSetZoom(GraphEditorSession session, SetGraphZoomAction a)
    {
        var editor = session.EditorState.Clone();
        if (a.TimeZoom.HasValue) editor.TimeZoom = a.TimeZoom.Value;
        if (a.ValueZoom.HasValue) editor.ValueZoom = a.ValueZoom.Value;
        return WithEditor(session, editor);
    }

    private static GraphEditorSession ReduceSetScroll(GraphEditorSession session, SetGraphScrollAction a)
    {
        var editor = session.EditorState.Clone();
        if (a.TimeScroll.HasValue) editor.TimeScroll = a.TimeScroll.Value;
        if (a.ValueScroll.HasValue) editor.ValueScroll = a.ValueScroll.Value;
        return WithEditor(session, editor);
    }

    private static GraphEditorSession ReduceSelectKeyframes(GraphEditorSession session, SelectGraphKeyframesAction a)
    {
        var editor = session.EditorState.Clone();
        editor.SelectedKeyframeIndices = new List<int>(a.Indices);
        return WithEditor(session, editor);
    }

    // ---- Track mutation actions ----

    private static GraphEditorSession ReduceAddKeyframe(GraphEditorSession session, AddGraphKeyframeAction a)
    {
        var track = FindTrack(session, a.TrackId);
        if (track == null) return session;
        var newTrack = track.Clone();
        newTrack.AddKeyframe(a.Keyframe.Clone());
        return WithTrack(session, newTrack);
    }

    private static GraphEditorSession ReduceRemoveKeyframe(GraphEditorSession session, RemoveGraphKeyframeAction a)
    {
        var track = FindTrack(session, a.TrackId);
        if (track == null) return session;
        if (a.Index < 0 || a.Index >= track.Keyframes.Count) return session;
        var newTrack = track.Clone();
        newTrack.RemoveKeyframeAt(a.Index);
        return WithTrack(session, newTrack);
    }

    private static GraphEditorSession ReduceUpdateKeyframe(GraphEditorSession session, UpdateGraphKeyframeAction a)
    {
        var track = FindTrack(session, a.TrackId);
        if (track == null) return session;
        if (a.Index < 0 || a.Index >= track.Keyframes.Count) return session;
        var newTrack = track.Clone();
        newTrack.UpdateKeyframe(a.Index, a.Keyframe.Clone());
        return WithTrack(session, newTrack);
    }

    private static GraphEditorSession ReduceSetTangentMode(GraphEditorSession session, SetKeyframeTangentModeAction a)
    {
        var track = FindTrack(session, a.TrackId);
        if (track == null) return session;
        if (a.Index < 0 || a.Index >= track.Keyframes.Count) return session;

        var newTrack = track.Clone();
        var kf = newTrack.Keyframes[a.Index];
        kf.TangentMode = a.Mode;
        kf.Handles = ComputeHandlesForMode(a.Mode, newTrack, a.Index);
        newTrack.Keyframes[a.Index] = kf;
        return WithTrack(session, newTrack);
    }

    /// <summary>Compute handles for the given tangent mode, based on the keyframe's neighbors.</summary>
    private static BezierHandles? ComputeHandlesForMode(TangentMode mode, GraphTrack track, int index)
    {
        var kf = track.Keyframes[index];
        var hasPrev = index > 0;
        var hasNext = index < track.Keyframes.Count - 1;
        var prev = hasPrev ? track.Keyframes[index - 1] : null;
        var next = hasNext ? track.Keyframes[index + 1] : null;

        // Use NaN to signal "no neighbor" for AutoTangent.
        var prevTime = prev?.Time ?? double.NaN;
        var prevValue = prev?.Value ?? 0f;
        var nextTime = next?.Time ?? double.NaN;
        var nextValue = next?.Value ?? 0f;

        return mode switch
        {
            TangentMode.Auto => AutoTangent.ComputeAutoTangent(prevTime, prevValue, kf.Time, kf.Value, nextTime, nextValue),
            TangentMode.Smooth => AutoTangent.ComputeSmoothTangent(prevTime, prevValue, kf.Time, kf.Value, nextTime, nextValue),
            TangentMode.Linear => AutoTangent.MakeLinear(),
            TangentMode.Stepped => AutoTangent.MakeStepped(),
            TangentMode.Broken => kf.Handles, // leave as-is (independently controlled)
            _ => kf.Handles,
        };
    }

    // ---- Helpers ----

    private static GraphTrack? FindTrack(GraphEditorSession session, string trackId)
        => session.Tracks.FirstOrDefault(t => t.Id == trackId);

    private static GraphEditorSession WithEditor(GraphEditorSession session, GraphEditorState editor)
        => new()
        {
            EditorState = editor,
            Tracks = session.Tracks,
        };

    private static GraphEditorSession WithTrack(GraphEditorSession session, GraphTrack newTrack)
    {
        var tracks = session.Tracks.Select(t => t.Id == newTrack.Id ? newTrack : t).ToList();
        return new()
        {
            EditorState = session.EditorState,
            Tracks = tracks,
        };
    }
}
