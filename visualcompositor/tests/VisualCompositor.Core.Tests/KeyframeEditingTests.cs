using VisualCompositor.Core.Model;
using VisualCompositor.Core.Primitives;
using VisualCompositor.State;
using VisualCompositor.State.Actions;
using VisualCompositor.State.Reducer;

namespace VisualCompositor.Core.Tests;

public class KeyframeEditingTests
{
    private static CompositionDocument BuildSimpleDocument()
    {
        var doc = new CompositionDocument();
        var layer = new Layer { Id = "layer_0", Name = "BG", OsbLayer = OsbLayer.Background };
        var sprite = new SpriteDeclaration { Id = "spr_001", LayerId = "layer_0", TexturePath = "bg.png" };
        var track = new PropertyTrack { PropertyName = "Opacity", ValueType = "Float" };
        track.FloatKeyframes.Add(new Keyframe<float> { Time = 0, Value = 1.0f, Easing = OsbEasing.None });
        sprite.PropertyTracks.Add(track);
        layer.Sprites.Add(sprite);
        doc.Layers.Add(layer);
        return doc;
    }

    private static EditorState BuildState() => new() { Document = BuildSimpleDocument() };

    private static List<Keyframe<float>> GetOpacityKeyframes(EditorState state)
        => state.Document.Layers[0].Sprites[0]
            .PropertyTracks.First(t => t.PropertyName == "Opacity").FloatKeyframes;

    [Fact]
    public void AddKeyframe_AddsToCorrectPropertyTrack()
    {
        var state = BuildState();
        Assert.Single(GetOpacityKeyframes(state));

        var afterAdd = Reducer.Reduce(state, new AddKeyframeAction(
            "layer_0", "spr_001", "Opacity", 500, "0.5", OsbEasing.Out));

        var keyframes = GetOpacityKeyframes(afterAdd);
        Assert.Equal(2, keyframes.Count);
        Assert.Contains(keyframes, k => k.Time == 500 && Math.Abs(k.Value - 0.5f) < 1e-6);
    }

    [Fact]
    public void AddKeyframe_KeepsKeyframesSortedByTime()
    {
        var state = BuildState();
        // Existing keyframe at t=0; add at t=1000, then t=500, then t=250
        var s1 = Reducer.Reduce(state, new AddKeyframeAction(
            "layer_0", "spr_001", "Opacity", 1000, "0.3", OsbEasing.None));
        var s2 = Reducer.Reduce(s1, new AddKeyframeAction(
            "layer_0", "spr_001", "Opacity", 500, "0.5", OsbEasing.None));
        var s3 = Reducer.Reduce(s2, new AddKeyframeAction(
            "layer_0", "spr_001", "Opacity", 250, "0.7", OsbEasing.None));

        var keyframes = GetOpacityKeyframes(s3);
        Assert.Equal(4, keyframes.Count);
        // Verify sorted ascending by Time
        for (int i = 1; i < keyframes.Count; i++)
            Assert.True(keyframes[i - 1].Time <= keyframes[i].Time,
                $"Keyframe at index {i - 1} (t={keyframes[i - 1].Time}) should precede index {i} (t={keyframes[i].Time})");
        Assert.Equal(0, keyframes[0].Time);
        Assert.Equal(250, keyframes[1].Time);
        Assert.Equal(500, keyframes[2].Time);
        Assert.Equal(1000, keyframes[3].Time);
    }

    [Fact]
    public void AddKeyframe_PushesToUndoStack_AndClearsRedo()
    {
        var state = BuildState();
        // First make an edit so we have something on the redo stack after undo
        var afterEdit = Reducer.Reduce(state, new EditKeyframeAction(
            "layer_0", "spr_001", "Opacity", 0, 100, "0.5"));
        var afterUndo = Reducer.Reduce(afterEdit, new UndoAction());
        Assert.Single(afterUndo.RedoStack);
        Assert.Empty(afterUndo.UndoStack);

        // Now add a keyframe — should push to undo and clear redo
        var afterAdd = Reducer.Reduce(afterUndo, new AddKeyframeAction(
            "layer_0", "spr_001", "Opacity", 500, "0.5", OsbEasing.None));

        Assert.Single(afterAdd.UndoStack);
        Assert.Empty(afterAdd.RedoStack);
    }

    [Fact]
    public void EditKeyframe_UpdatesValue()
    {
        var state = BuildState();
        var original = GetOpacityKeyframes(state)[0];
        Assert.Equal(1.0f, original.Value);

        var afterEdit = Reducer.Reduce(state, new EditKeyframeAction(
            "layer_0", "spr_001", "Opacity", 0, 0, "0.25"));

        var kf = GetOpacityKeyframes(afterEdit)[0];
        Assert.Equal(0.25f, kf.Value);
        Assert.Equal(0, kf.Time); // time unchanged
    }

    [Fact]
    public void EditKeyframe_ReSortsWhenTimeChanges()
    {
        var state = BuildState();
        // Add keyframes at t=0, 500, 1000
        var s1 = Reducer.Reduce(state, new AddKeyframeAction(
            "layer_0", "spr_001", "Opacity", 500, "0.5", OsbEasing.None));
        var s2 = Reducer.Reduce(s1, new AddKeyframeAction(
            "layer_0", "spr_001", "Opacity", 1000, "0.3", OsbEasing.None));
        // Keyframes sorted: [0, 500, 1000]
        Assert.Equal(0, GetOpacityKeyframes(s2)[0].Time);
        Assert.Equal(500, GetOpacityKeyframes(s2)[1].Time);
        Assert.Equal(1000, GetOpacityKeyframes(s2)[2].Time);

        // Edit the keyframe at index 2 (t=1000) to t=250 — should re-sort
        var afterEdit = Reducer.Reduce(s2, new EditKeyframeAction(
            "layer_0", "spr_001", "Opacity", 2, 250, "0.9"));

        var keyframes = GetOpacityKeyframes(afterEdit);
        Assert.Equal(3, keyframes.Count);
        // After re-sort: [0, 250, 500]
        Assert.Equal(0, keyframes[0].Time);
        Assert.Equal(250, keyframes[1].Time);
        Assert.Equal(500, keyframes[2].Time);
        // The edited value should be on the t=250 keyframe
        Assert.Equal(0.9f, keyframes[1].Value);
    }

    [Fact]
    public void RemoveKeyframe_RemovesCorrectKeyframe()
    {
        var state = BuildState();
        // Add two more keyframes: t=500 (val=0.5), t=1000 (val=0.3)
        var s1 = Reducer.Reduce(state, new AddKeyframeAction(
            "layer_0", "spr_001", "Opacity", 500, "0.5", OsbEasing.None));
        var s2 = Reducer.Reduce(s1, new AddKeyframeAction(
            "layer_0", "spr_001", "Opacity", 1000, "0.3", OsbEasing.None));
        // Sorted: [0 (1.0), 500 (0.5), 1000 (0.3)]
        Assert.Equal(3, GetOpacityKeyframes(s2).Count);

        // Remove the middle one (index 1, t=500)
        var afterRemove = Reducer.Reduce(s2, new RemoveKeyframeAction(
            "layer_0", "spr_001", "Opacity", 1));

        var keyframes = GetOpacityKeyframes(afterRemove);
        Assert.Equal(2, keyframes.Count);
        Assert.Equal(0, keyframes[0].Time);
        Assert.Equal(1000, keyframes[1].Time);
        Assert.DoesNotContain(keyframes, k => k.Time == 500);
    }

    [Fact]
    public void RemoveKeyframe_PushesToUndoStack()
    {
        var state = BuildState();
        Assert.Empty(state.UndoStack);

        var afterRemove = Reducer.Reduce(state, new RemoveKeyframeAction(
            "layer_0", "spr_001", "Opacity", 0));

        Assert.Single(afterRemove.UndoStack);
        Assert.Empty(afterRemove.RedoStack);
        Assert.Empty(GetOpacityKeyframes(afterRemove));
    }

    [Fact]
    public void AddParameterSegment_AddsToParameterTrack()
    {
        var state = BuildState();
        // No parameter track initially
        Assert.Null(state.Document.Layers[0].Sprites[0].ParameterTrack);

        var afterAdd = Reducer.Reduce(state, new AddParameterSegmentAction(
            "layer_0", "spr_001", ParameterType.AdditiveBlending, 100, 500, OpenEndedMode.ExplicitEnd));

        var track = afterAdd.Document.Layers[0].Sprites[0].ParameterTrack;
        Assert.NotNull(track);
        Assert.Single(track!.Segments);
        Assert.Equal(ParameterType.AdditiveBlending, track.Segments[0].Parameter);
        Assert.Equal(100, track.Segments[0].StartTime);
        Assert.Equal(500, track.Segments[0].EndTime);
        Assert.Equal(OpenEndedMode.ExplicitEnd, track.Segments[0].OpenEndedMode);
    }

    [Fact]
    public void AddParameterSegment_PushesToUndoStack()
    {
        var state = BuildState();
        Assert.Empty(state.UndoStack);

        var afterAdd = Reducer.Reduce(state, new AddParameterSegmentAction(
            "layer_0", "spr_001", ParameterType.FlipHorizontal, 0, null, OpenEndedMode.UntilLayerEnd));

        Assert.Single(afterAdd.UndoStack);
        Assert.Empty(afterAdd.RedoStack);
    }

    [Fact]
    public void RemoveParameterSegment_RemovesCorrectSegment()
    {
        var state = BuildState();
        // Add two segments
        var s1 = Reducer.Reduce(state, new AddParameterSegmentAction(
            "layer_0", "spr_001", ParameterType.FlipHorizontal, 0, 100, OpenEndedMode.ExplicitEnd));
        var s2 = Reducer.Reduce(s1, new AddParameterSegmentAction(
            "layer_0", "spr_001", ParameterType.AdditiveBlending, 200, 500, OpenEndedMode.ExplicitEnd));
        Assert.Equal(2, s2.Document.Layers[0].Sprites[0].ParameterTrack!.Segments.Count);

        // Remove the first segment (index 0)
        var afterRemove = Reducer.Reduce(s2, new RemoveParameterSegmentAction(
            "layer_0", "spr_001", 0));

        var segments = afterRemove.Document.Layers[0].Sprites[0].ParameterTrack!.Segments;
        Assert.Single(segments);
        Assert.Equal(ParameterType.AdditiveBlending, segments[0].Parameter);
        Assert.Equal(200, segments[0].StartTime);
    }

    [Fact]
    public void RemoveParameterSegment_PushesToUndoStack()
    {
        var state = BuildState();
        var s1 = Reducer.Reduce(state, new AddParameterSegmentAction(
            "layer_0", "spr_001", ParameterType.FlipHorizontal, 0, 100, OpenEndedMode.ExplicitEnd));
        Assert.Single(s1.UndoStack);

        var afterRemove = Reducer.Reduce(s1, new RemoveParameterSegmentAction(
            "layer_0", "spr_001", 0));

        Assert.Equal(2, afterRemove.UndoStack.Count);
        Assert.Empty(afterRemove.RedoStack);
    }

    [Fact]
    public void Undo_RestoresKeyframeAfterAdd()
    {
        var state = BuildState();
        Assert.Single(GetOpacityKeyframes(state));

        var afterAdd = Reducer.Reduce(state, new AddKeyframeAction(
            "layer_0", "spr_001", "Opacity", 500, "0.5", OsbEasing.None));
        Assert.Equal(2, GetOpacityKeyframes(afterAdd).Count);
        Assert.Single(afterAdd.UndoStack);

        var afterUndo = Reducer.Reduce(afterAdd, new UndoAction());

        Assert.Single(GetOpacityKeyframes(afterUndo)); // back to 1 keyframe
        Assert.Empty(afterUndo.UndoStack);
        Assert.Single(afterUndo.RedoStack);
    }

    [Fact]
    public void Undo_RestoresKeyframeAfterRemove()
    {
        var state = BuildState();
        Assert.Single(GetOpacityKeyframes(state));

        var afterRemove = Reducer.Reduce(state, new RemoveKeyframeAction(
            "layer_0", "spr_001", "Opacity", 0));
        Assert.Empty(GetOpacityKeyframes(afterRemove));
        Assert.Single(afterRemove.UndoStack);

        var afterUndo = Reducer.Reduce(afterRemove, new UndoAction());

        Assert.Single(GetOpacityKeyframes(afterUndo)); // keyframe restored
        Assert.Empty(afterUndo.UndoStack);
        Assert.Single(afterUndo.RedoStack);
    }
}
