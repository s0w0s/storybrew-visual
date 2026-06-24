using VisualCompositor.Core.Model;
using VisualCompositor.Core.Model.GraphEditor;
using VisualCompositor.Core.Primitives;
using VisualCompositor.State;
using VisualCompositor.State.Actions;
using VisualCompositor.State.Reducer;

namespace VisualCompositor.Core.Tests;

/// <summary>Phase 10 (Task 31) tests for <see cref="GraphEditorReducer"/>.
/// Verifies view-state changes (active track, visibility, zoom, selection) and
/// track mutations (add/remove/update keyframes, tangent mode changes).</summary>
public class GraphEditorReducerTests
{
    private static GraphEditorSession BuildSession()
    {
        var track = new GraphTrack { Id = "track_opacity", Name = "Opacity" };
        track.AddKeyframe(new GraphKeyframe { Time = 0, Value = 0f, Easing = OsbEasing.None });
        track.AddKeyframe(new GraphKeyframe { Time = 500, Value = 0.5f, Easing = OsbEasing.None });
        track.AddKeyframe(new GraphKeyframe { Time = 1000, Value = 1f, Easing = OsbEasing.None });
        return new GraphEditorSession
        {
            EditorState = new GraphEditorState { ActiveTrackId = "track_opacity" },
            Tracks = { track },
        };
    }

    [Fact]
    public void SetGraphActiveTrackAction_SetsActiveTrack()
    {
        var session = BuildSession();
        var result = GraphEditorReducer.Reduce(session, new SetGraphActiveTrackAction("track_other"));
        Assert.Equal("track_other", result.EditorState.ActiveTrackId);
        // Original session unchanged.
        Assert.Equal("track_opacity", session.EditorState.ActiveTrackId);
    }

    [Fact]
    public void SetGraphTrackVisibilityAction_TogglesVisibility()
    {
        var session = BuildSession();
        var result = GraphEditorReducer.Reduce(session,
            new SetGraphTrackVisibilityAction("track_opacity", false));
        Assert.DoesNotContain("track_opacity", result.EditorState.VisibleTrackIds);

        var result2 = GraphEditorReducer.Reduce(result,
            new SetGraphTrackVisibilityAction("track_opacity", true));
        Assert.Contains("track_opacity", result2.EditorState.VisibleTrackIds);
    }

    [Fact]
    public void AddGraphKeyframeAction_AddsAndSorts()
    {
        var session = BuildSession();
        Assert.Equal(3, session.Tracks[0].Keyframes.Count);

        var result = GraphEditorReducer.Reduce(session,
            new AddGraphKeyframeAction("track_opacity",
                new GraphKeyframe { Time = 250, Value = 0.25f }));

        Assert.Equal(4, result.Tracks[0].Keyframes.Count);
        // Sorted: 0, 250, 500, 1000.
        Assert.Equal(250, result.Tracks[0].Keyframes[1].Time);
        Assert.Equal(0.25f, result.Tracks[0].Keyframes[1].Value);
        // Original unchanged.
        Assert.Equal(3, session.Tracks[0].Keyframes.Count);
    }

    [Fact]
    public void RemoveGraphKeyframeAction_Removes()
    {
        var session = BuildSession();
        var result = GraphEditorReducer.Reduce(session,
            new RemoveGraphKeyframeAction("track_opacity", 1));
        Assert.Equal(2, result.Tracks[0].Keyframes.Count);
        Assert.Equal(0, result.Tracks[0].Keyframes[0].Time);
        Assert.Equal(1000, result.Tracks[0].Keyframes[1].Time);
    }

    [Fact]
    public void UpdateGraphKeyframeAction_Updates()
    {
        var session = BuildSession();
        var result = GraphEditorReducer.Reduce(session,
            new UpdateGraphKeyframeAction("track_opacity", 1,
                new GraphKeyframe { Time = 750, Value = 0.75f }));
        Assert.Equal(3, result.Tracks[0].Keyframes.Count);
        // Re-sorted: 0, 750, 1000.
        Assert.Equal(750, result.Tracks[0].Keyframes[1].Time);
        Assert.Equal(0.75f, result.Tracks[0].Keyframes[1].Value);
    }

    [Fact]
    public void SetKeyframeTangentModeAction_AutoToLinear_ZeroesHandles()
    {
        var session = BuildSession();
        // Give the middle keyframe some handles first.
        session.Tracks[0].Keyframes[1].Handles = new BezierHandles
        {
            InHandle = new Vector2(-1, -1),
            OutHandle = new Vector2(1, 1),
        };
        session.Tracks[0].Keyframes[1].TangentMode = TangentMode.Auto;

        var result = GraphEditorReducer.Reduce(session,
            new SetKeyframeTangentModeAction("track_opacity", 1, TangentMode.Linear));

        var kf = result.Tracks[0].Keyframes[1];
        Assert.Equal(TangentMode.Linear, kf.TangentMode);
        Assert.NotNull(kf.Handles);
        Assert.Equal(Vector2.Zero, kf.Handles!.InHandle);
        Assert.Equal(Vector2.Zero, kf.Handles.OutHandle);
    }

    [Fact]
    public void SetKeyframeTangentModeAction_AutoToAuto_ComputesHandles()
    {
        var session = BuildSession();
        // Middle keyframe at t=500, value=0.5, neighbors at (0,0) and (1000,1).
        var result = GraphEditorReducer.Reduce(session,
            new SetKeyframeTangentModeAction("track_opacity", 1, TangentMode.Auto));

        var kf = result.Tracks[0].Keyframes[1];
        Assert.Equal(TangentMode.Auto, kf.TangentMode);
        Assert.NotNull(kf.Handles);
    }

    [Fact]
    public void SetGraphZoomAction_SetsZoom()
    {
        var session = BuildSession();
        var result = GraphEditorReducer.Reduce(session,
            new SetGraphZoomAction(2.0f, 0.5f));
        Assert.Equal(2.0f, result.EditorState.TimeZoom);
        Assert.Equal(0.5f, result.EditorState.ValueZoom);
    }

    [Fact]
    public void SetGraphZoomAction_PartialUpdate_KeepsOtherValue()
    {
        var session = BuildSession();
        session.EditorState.TimeZoom = 1.5f;
        session.EditorState.ValueZoom = 1.5f;
        var result = GraphEditorReducer.Reduce(session,
            new SetGraphZoomAction(3.0f, null));
        Assert.Equal(3.0f, result.EditorState.TimeZoom);
        Assert.Equal(1.5f, result.EditorState.ValueZoom);
    }

    [Fact]
    public void SelectGraphKeyframesAction_SetsSelection()
    {
        var session = BuildSession();
        var result = GraphEditorReducer.Reduce(session,
            new SelectGraphKeyframesAction(new List<int> { 0, 2 }));
        Assert.Equal(new[] { 0, 2 }, result.EditorState.SelectedKeyframeIndices);
    }

    [Fact]
    public void SetGraphScrollAction_SetsScroll()
    {
        var session = BuildSession();
        var result = GraphEditorReducer.Reduce(session,
            new SetGraphScrollAction(500.0, 10f));
        Assert.Equal(500.0, result.EditorState.TimeScroll);
        Assert.Equal(10f, result.EditorState.ValueScroll);
    }

    [Fact]
    public void SetGraphActiveTrackAction_ClearsSelection()
    {
        var session = BuildSession();
        session.EditorState.SelectedKeyframeIndices = new List<int> { 0, 1 };
        var result = GraphEditorReducer.Reduce(session,
            new SetGraphActiveTrackAction("track_other"));
        Assert.Empty(result.EditorState.SelectedKeyframeIndices);
    }

    [Fact]
    public void UnknownAction_ReturnsSameSession()
    {
        var session = BuildSession();
        var result = GraphEditorReducer.Reduce(session, new UnknownAction());
        Assert.Same(session, result);
    }

    private sealed record UnknownAction;
}
