using VisualCompositor.Core.Model;
using VisualCompositor.Core.Model.GraphEditor;
using VisualCompositor.Core.Primitives;

namespace VisualCompositor.Core.Tests;

/// <summary>Phase 10 (Task 31) tests for the graph editor model types:
/// <see cref="GraphKeyframe"/>, <see cref="GraphTrack"/>, <see cref="GraphCurve"/>,
/// <see cref="GraphEditorState"/>, and <see cref="AutoTangent"/>.</summary>
public class GraphEditorTests
{
    private const float Tolerance = 1e-5f;

    // ---------- GraphKeyframe ----------

    [Fact]
    public void GraphKeyframe_Clone_RoundTripsAllProperties()
    {
        var kf = new GraphKeyframe
        {
            Time = 1000,
            Value = 0.5f,
            Easing = OsbEasing.InQuad,
            Handles = new BezierHandles { InHandle = new Vector2(-1, -2), OutHandle = new Vector2(1, 2) },
            TangentMode = TangentMode.Smooth,
            Locked = true,
        };
        var clone = kf.Clone();
        Assert.Equal(kf.Time, clone.Time);
        Assert.Equal(kf.Value, clone.Value);
        Assert.Equal(kf.Easing, clone.Easing);
        Assert.Equal(kf.TangentMode, clone.TangentMode);
        Assert.Equal(kf.Locked, clone.Locked);
        Assert.NotNull(clone.Handles);
        Assert.Equal(kf.Handles!.InHandle, clone.Handles!.InHandle);
        Assert.Equal(kf.Handles.OutHandle, clone.Handles.OutHandle);
        // Clone is independent
        clone.Value = 999f;
        Assert.Equal(0.5f, kf.Value);
    }

    // ---------- GraphTrack ----------

    [Fact]
    public void GraphTrack_AddKeyframe_SortsByTime()
    {
        var track = new GraphTrack { Id = "t1", Name = "Opacity" };
        track.AddKeyframe(new GraphKeyframe { Time = 1000, Value = 1f });
        track.AddKeyframe(new GraphKeyframe { Time = 0, Value = 0f });
        track.AddKeyframe(new GraphKeyframe { Time = 500, Value = 0.5f });

        Assert.Equal(3, track.Keyframes.Count);
        Assert.Equal(0, track.Keyframes[0].Time);
        Assert.Equal(500, track.Keyframes[1].Time);
        Assert.Equal(1000, track.Keyframes[2].Time);
    }

    [Fact]
    public void GraphTrack_SampleAt_LinearInterpolation()
    {
        var track = new GraphTrack { Id = "t1", Name = "Opacity" };
        track.AddKeyframe(new GraphKeyframe { Time = 0, Value = 0f, Easing = OsbEasing.None });
        track.AddKeyframe(new GraphKeyframe { Time = 1000, Value = 1f, Easing = OsbEasing.None });

        Assert.Equal(0f, track.SampleAt(0), Tolerance);
        Assert.Equal(0.25f, track.SampleAt(250), Tolerance);
        Assert.Equal(0.5f, track.SampleAt(500), Tolerance);
        Assert.Equal(0.75f, track.SampleAt(750), Tolerance);
        Assert.Equal(1f, track.SampleAt(1000), Tolerance);
    }

    [Fact]
    public void GraphTrack_SampleAt_WithInQuadEasing()
    {
        var track = new GraphTrack { Id = "t1", Name = "Opacity" };
        track.AddKeyframe(new GraphKeyframe { Time = 0, Value = 0f, Easing = OsbEasing.InQuad });
        track.AddKeyframe(new GraphKeyframe { Time = 1000, Value = 1f, Easing = OsbEasing.None });

        // At t=0.5, InQuad gives 0.25, so value = 0 + (1-0)*0.25 = 0.25.
        Assert.Equal(0.25f, track.SampleAt(500), Tolerance);
    }

    [Fact]
    public void GraphTrack_SampleAt_BeforeFirstKeyframe_ReturnsFirstValue()
    {
        var track = new GraphTrack { Id = "t1", Name = "Opacity" };
        track.AddKeyframe(new GraphKeyframe { Time = 500, Value = 0.5f });
        track.AddKeyframe(new GraphKeyframe { Time = 1000, Value = 1f });

        Assert.Equal(0.5f, track.SampleAt(0), Tolerance);
        Assert.Equal(0.5f, track.SampleAt(250), Tolerance);
        Assert.Equal(0.5f, track.SampleAt(500), Tolerance);
    }

    [Fact]
    public void GraphTrack_SampleAt_AfterLastKeyframe_ReturnsLastValue()
    {
        var track = new GraphTrack { Id = "t1", Name = "Opacity" };
        track.AddKeyframe(new GraphKeyframe { Time = 0, Value = 0f });
        track.AddKeyframe(new GraphKeyframe { Time = 500, Value = 0.5f });

        Assert.Equal(0.5f, track.SampleAt(500), Tolerance);
        Assert.Equal(0.5f, track.SampleAt(1000), Tolerance);
        Assert.Equal(0.5f, track.SampleAt(9999), Tolerance);
    }

    [Fact]
    public void GraphTrack_RemoveKeyframeAt_RemovesCorrectIndex()
    {
        var track = new GraphTrack { Id = "t1", Name = "Opacity" };
        track.AddKeyframe(new GraphKeyframe { Time = 0, Value = 0f });
        track.AddKeyframe(new GraphKeyframe { Time = 500, Value = 0.5f });
        track.AddKeyframe(new GraphKeyframe { Time = 1000, Value = 1f });

        track.RemoveKeyframeAt(1);
        Assert.Equal(2, track.Keyframes.Count);
        Assert.Equal(0, track.Keyframes[0].Time);
        Assert.Equal(1000, track.Keyframes[1].Time);
    }

    [Fact]
    public void GraphTrack_UpdateKeyframe_ReplacesAndResorts()
    {
        var track = new GraphTrack { Id = "t1", Name = "Opacity" };
        track.AddKeyframe(new GraphKeyframe { Time = 0, Value = 0f });
        track.AddKeyframe(new GraphKeyframe { Time = 500, Value = 0.5f });

        track.UpdateKeyframe(0, new GraphKeyframe { Time = 750, Value = 0.75f });
        Assert.Equal(2, track.Keyframes.Count);
        Assert.Equal(500, track.Keyframes[0].Time);
        Assert.Equal(750, track.Keyframes[1].Time);
        Assert.Equal(0.75f, track.Keyframes[1].Value);
    }

    // ---------- GraphCurve ----------

    [Fact]
    public void GraphCurve_SampleAt_WithBezierHandles()
    {
        // Bezier that exactly matches ease-in (t^2): OutHandle.Y=0, InHandle.Y=-2/3 (for range 1).
        var curve = new GraphCurve
        {
            StartTime = 0,
            EndTime = 1000,
            StartValue = 0f,
            EndValue = 1f,
            Easing = OsbEasing.None,
            StartHandles = new BezierHandles { InHandle = Vector2.Zero, OutHandle = new Vector2(0, 0f) },
            EndHandles = new BezierHandles { InHandle = new Vector2(0, -2f / 3f), OutHandle = Vector2.Zero },
        };
        // At t=0.5, the cubic bezier with p0=0, p1=0, p2=1/3, p3=1 gives t^2 = 0.25.
        Assert.Equal(0.25f, curve.SampleAt(500), Tolerance);
    }

    [Fact]
    public void GraphCurve_SampleAt_WithEasing()
    {
        var curve = new GraphCurve
        {
            StartTime = 0,
            EndTime = 1000,
            StartValue = 0f,
            EndValue = 1f,
            Easing = OsbEasing.OutQuad,
            StartHandles = null,
            EndHandles = null,
        };
        // OutQuad at t=0.5 is 0.75.
        Assert.Equal(0.75f, curve.SampleAt(500), Tolerance);
    }

    // ---------- GraphEditorState ----------

    [Fact]
    public void GraphEditorState_Clone_RoundTripsAllProperties()
    {
        var state = new GraphEditorState
        {
            ActiveTrackId = "track_1",
            VisibleTrackIds = new HashSet<string> { "track_1", "track_2" },
            TimeZoom = 2.5f,
            ValueZoom = 0.5f,
            TimeScroll = 1000.0,
            ValueScroll = -10f,
            SelectedKeyframeIndices = new List<int> { 0, 2, 4 },
        };
        var clone = state.Clone();
        Assert.Equal(state.ActiveTrackId, clone.ActiveTrackId);
        Assert.Equal(state.VisibleTrackIds, clone.VisibleTrackIds);
        Assert.Equal(state.TimeZoom, clone.TimeZoom);
        Assert.Equal(state.ValueZoom, clone.ValueZoom);
        Assert.Equal(state.TimeScroll, clone.TimeScroll);
        Assert.Equal(state.ValueScroll, clone.ValueScroll);
        Assert.Equal(state.SelectedKeyframeIndices, clone.SelectedKeyframeIndices);
        // Clone is independent
        clone.SelectedKeyframeIndices.Add(99);
        Assert.Equal(3, state.SelectedKeyframeIndices.Count);
    }

    // ---------- AutoTangent ----------

    [Fact]
    public void AutoTangent_ComputeAutoTangent_MiddleKeyframe_ReturnsNonNull()
    {
        var handles = AutoTangent.ComputeAutoTangent(
            prevTime: 0, prevValue: 0f,
            time: 500, value: 0.5f,
            nextTime: 1000, nextValue: 1f);
        Assert.NotNull(handles);
        // Slope = (1 - 0) / (1000 - 0) = 0.001 per ms.
        // OutMag = (1000-500)/3 ≈ 166.67; OutHandle.Y = slope * outMag ≈ 0.1667.
        Assert.True(MathF.Abs(handles!.OutHandle.Y - 0.1667f) < 0.01f,
            $"OutHandle.Y={handles.OutHandle.Y}, expected ~0.1667");
    }

    [Fact]
    public void AutoTangent_ComputeAutoTangent_EndpointNoPrev_ReturnsNull()
    {
        var handles = AutoTangent.ComputeAutoTangent(
            prevTime: double.NaN, prevValue: 0f,
            time: 0, value: 0f,
            nextTime: 500, nextValue: 1f);
        Assert.Null(handles);
    }

    [Fact]
    public void AutoTangent_ComputeAutoTangent_EndpointNoNext_ReturnsNull()
    {
        var handles = AutoTangent.ComputeAutoTangent(
            prevTime: 0, prevValue: 0f,
            time: 500, value: 0.5f,
            nextTime: double.NaN, nextValue: 1f);
        Assert.Null(handles);
    }

    [Fact]
    public void AutoTangent_MakeLinear_ReturnsZeroHandles()
    {
        var handles = AutoTangent.MakeLinear();
        Assert.Equal(Vector2.Zero, handles.InHandle);
        Assert.Equal(Vector2.Zero, handles.OutHandle);
    }

    [Fact]
    public void AutoTangent_MakeStepped_ReturnsLargeXHandles()
    {
        var handles = AutoTangent.MakeStepped();
        // Stepped handles have large X (to hold value) and zero Y.
        Assert.True(MathF.Abs(handles.InHandle.X) > 100f);
        Assert.True(MathF.Abs(handles.OutHandle.X) > 100f);
        Assert.Equal(0f, handles.InHandle.Y);
        Assert.Equal(0f, handles.OutHandle.Y);
    }

    [Fact]
    public void AutoTangent_ComputeSmoothTangent_MiddleKeyframe_ReturnsSymmetric()
    {
        var handles = AutoTangent.ComputeSmoothTangent(
            prevTime: 0, prevValue: 0f,
            time: 500, value: 0.5f,
            nextTime: 1000, nextValue: 1f);
        Assert.NotNull(handles);
        // Symmetric: |InHandle.X| == |OutHandle.X|, |InHandle.Y| == |OutHandle.Y|.
        Assert.Equal(MathF.Abs(handles!.InHandle.X), MathF.Abs(handles.OutHandle.X), Tolerance);
        Assert.Equal(MathF.Abs(handles.InHandle.Y), MathF.Abs(handles.OutHandle.Y), Tolerance);
    }
}
