using VisualCompositor.Core.Model;
using VisualCompositor.Core.Primitives;
using VisualCompositor.State;
using VisualCompositor.State.Actions;
using VisualCompositor.State.Reducer;

namespace VisualCompositor.Core.Tests;

public class ReducerTests
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

    [Fact]
    public void Reducer_PureFunction_SameActionSameState_ProducesSameResult()
    {
        var state = BuildState();
        var action = new SetCurrentTimeAction(500);

        var result1 = Reducer.Reduce(state, action);
        var result2 = Reducer.Reduce(state, action);

        Assert.Equal(result1.CurrentTime, result2.CurrentTime);
        Assert.Equal(result1.Revision, result2.Revision);
        Assert.Equal(result1.UndoStack.Count, result2.UndoStack.Count);
        Assert.Equal(result1.RedoStack.Count, result2.RedoStack.Count);
        // Original state is not mutated
        Assert.NotSame(result1, state);
        Assert.Equal(0, state.CurrentTime);
    }

    [Fact]
    public void SetCurrentTime_DoesNotChangeStacks()
    {
        var state = BuildState();
        // Populate undo stack with an edit first
        var edited = Reducer.Reduce(state, new EditKeyframeAction("layer_0", "spr_001", "Opacity", 0, 100, "0.5"));
        Assert.Single(edited.UndoStack);

        var afterSetTime = Reducer.Reduce(edited, new SetCurrentTimeAction(999));

        Assert.Equal(999, afterSetTime.CurrentTime);
        Assert.Single(afterSetTime.UndoStack); // unchanged
        Assert.Empty(afterSetTime.RedoStack); // unchanged
    }

    [Fact]
    public void EditKeyframe_PushesToUndoStack_AndClearsRedo()
    {
        var state = BuildState();

        // edit1 -> undo stack 1
        var afterEdit1 = Reducer.Reduce(state, new EditKeyframeAction("layer_0", "spr_001", "Opacity", 0, 100, "0.5"));
        Assert.Single(afterEdit1.UndoStack);
        Assert.Empty(afterEdit1.RedoStack);

        // undo -> undo stack 0, redo stack 1
        var afterUndo = Reducer.Reduce(afterEdit1, new UndoAction());
        Assert.Empty(afterUndo.UndoStack);
        Assert.Single(afterUndo.RedoStack);

        // edit2 -> undo stack 1, redo stack 0 (cleared)
        var afterEdit2 = Reducer.Reduce(afterUndo, new EditKeyframeAction("layer_0", "spr_001", "Opacity", 0, 200, "0.25"));
        Assert.Single(afterEdit2.UndoStack);
        Assert.Empty(afterEdit2.RedoStack); // redo cleared
    }

    [Fact]
    public void FailedEdit_NonExistentSprite_DoesNotChangeStacks()
    {
        var state = BuildState();
        var afterEdit = Reducer.Reduce(state, new EditKeyframeAction("layer_0", "spr_001", "Opacity", 0, 100, "0.5"));
        Assert.Single(afterEdit.UndoStack);

        // Failed edit: non-existent sprite
        var afterFailedEdit = Reducer.Reduce(afterEdit, new EditKeyframeAction("layer_0", "nonexistent", "Opacity", 0, 100, "0.5"));

        Assert.Same(afterEdit, afterFailedEdit); // same state returned
        Assert.Single(afterFailedEdit.UndoStack); // unchanged
        Assert.Empty(afterFailedEdit.RedoStack); // unchanged
    }

    [Fact]
    public void FailedEdit_InvalidKeyframeIndex_DoesNotChangeStacks()
    {
        var state = BuildState();
        var afterEdit = Reducer.Reduce(state, new EditKeyframeAction("layer_0", "spr_001", "Opacity", 0, 100, "0.5"));
        Assert.Single(afterEdit.UndoStack);

        // Failed edit: out-of-range keyframe index
        var afterFailed = Reducer.Reduce(afterEdit, new EditKeyframeAction("layer_0", "spr_001", "Opacity", 99, 100, "0.5"));

        Assert.Same(afterEdit, afterFailed);
        Assert.Single(afterFailed.UndoStack);
    }

    [Fact]
    public void LoadDocument_ClearsBothStacks()
    {
        var state = BuildState();
        var afterEdit = Reducer.Reduce(state, new EditKeyframeAction("layer_0", "spr_001", "Opacity", 0, 100, "0.5"));
        var afterUndo = Reducer.Reduce(afterEdit, new UndoAction());
        Assert.Empty(afterUndo.UndoStack);
        Assert.Single(afterUndo.RedoStack);

        var newDoc = BuildSimpleDocument();
        var afterLoad = Reducer.Reduce(afterUndo, new LoadDocumentAction(newDoc));

        Assert.Empty(afterLoad.UndoStack);
        Assert.Empty(afterLoad.RedoStack);
    }
}
