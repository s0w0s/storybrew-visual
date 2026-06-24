using VisualCompositor.Core.Model;
using VisualCompositor.Core.Primitives;
using VisualCompositor.State;
using VisualCompositor.State.Actions;
using VisualCompositor.State.Reducer;

namespace VisualCompositor.Core.Tests;

public class UndoRedoTests
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

    private static float GetOpacityValue(EditorState state)
    {
        var sprite = state.Document.Layers[0].Sprites[0];
        var track = sprite.PropertyTracks.First(t => t.PropertyName == "Opacity");
        return track.FloatKeyframes[0].Value;
    }

    [Fact]
    public void Undo_RestoresPreviousState()
    {
        var state = BuildState();
        Assert.Equal(1.0f, GetOpacityValue(state));

        var afterEdit = Reducer.Reduce(state, new EditKeyframeAction("layer_0", "spr_001", "Opacity", 0, 100, "0.5"));
        Assert.Equal(0.5f, GetOpacityValue(afterEdit));
        Assert.Single(afterEdit.UndoStack);

        var afterUndo = Reducer.Reduce(afterEdit, new UndoAction());

        Assert.Equal(1.0f, GetOpacityValue(afterUndo)); // restored to previous value
        Assert.Empty(afterUndo.UndoStack);
        Assert.Single(afterUndo.RedoStack);
    }

    [Fact]
    public void Redo_ReplaysAfterUndo()
    {
        var state = BuildState();
        var afterEdit = Reducer.Reduce(state, new EditKeyframeAction("layer_0", "spr_001", "Opacity", 0, 100, "0.5"));
        var afterUndo = Reducer.Reduce(afterEdit, new UndoAction());
        Assert.Equal(1.0f, GetOpacityValue(afterUndo));

        var afterRedo = Reducer.Reduce(afterUndo, new RedoAction());

        Assert.Equal(0.5f, GetOpacityValue(afterRedo)); // replayed
        Assert.Single(afterRedo.UndoStack);
        Assert.Empty(afterRedo.RedoStack);
    }

    [Fact]
    public void NewEditAfterUndo_ClearsRedoStack()
    {
        var state = BuildState();
        var afterEdit = Reducer.Reduce(state, new EditKeyframeAction("layer_0", "spr_001", "Opacity", 0, 100, "0.5"));
        var afterUndo = Reducer.Reduce(afterEdit, new UndoAction());
        Assert.Single(afterUndo.RedoStack);

        var afterNewEdit = Reducer.Reduce(afterUndo, new EditKeyframeAction("layer_0", "spr_001", "Opacity", 0, 200, "0.25"));

        Assert.Single(afterNewEdit.UndoStack);
        Assert.Empty(afterNewEdit.RedoStack); // cleared
    }

    [Fact]
    public void FailedUndo_EmptyStack_DoesNotChangeStacks()
    {
        var state = BuildState();
        Assert.Empty(state.UndoStack);
        Assert.Empty(state.RedoStack);

        var afterFailedUndo = Reducer.Reduce(state, new UndoAction());

        Assert.Same(state, afterFailedUndo);
        Assert.Empty(afterFailedUndo.UndoStack);
        Assert.Empty(afterFailedUndo.RedoStack);
    }

    [Fact]
    public void FailedRedo_EmptyStack_DoesNotChangeStacks()
    {
        var state = BuildState();
        var afterEdit = Reducer.Reduce(state, new EditKeyframeAction("layer_0", "spr_001", "Opacity", 0, 100, "0.5"));
        Assert.Single(afterEdit.UndoStack);
        Assert.Empty(afterEdit.RedoStack);

        // Redo on empty redo stack -> no change
        var afterFailedRedo = Reducer.Reduce(afterEdit, new RedoAction());

        Assert.Same(afterEdit, afterFailedRedo);
        Assert.Single(afterFailedRedo.UndoStack);
        Assert.Empty(afterFailedRedo.RedoStack);
    }
}
