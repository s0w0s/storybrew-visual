using VisualCompositor.Core.Model;
using VisualCompositor.Core.Primitives;
using VisualCompositor.Core.Validation;
using VisualCompositor.State;
using VisualCompositor.State.Actions;
using VisualCompositor.State.History;
using VisualCompositor.State.Reducer;

namespace VisualCompositor.Core.Tests;

public class ExpandBlockTransactionTests
{
    private static CompositionDocument BuildDocumentWithLoopBlock()
    {
        var doc = new CompositionDocument();
        var layer = new Layer { Id = "layer_0", Name = "BG", OsbLayer = OsbLayer.Background };
        var sprite = new SpriteDeclaration { Id = "spr_001", LayerId = "layer_0", TexturePath = "bg.png" };
        var loopBlock = new LoopBlock
        {
            Id = "blk_001",
            LayerId = "layer_0",
            HeaderCommandId = "cmd_hdr_001",
            StartTime = 1000,
            LoopCount = 3,
            RelativeCommands = new List<RelativeCommand>
            {
                new() { Id = "cmd_rel_001", CommandType = "F", StartTime = 0, EndTime = 100, StartValue = "0", EndValue = "1", ParentBlockId = "blk_001" },
            },
        };
        sprite.Blocks.Add(loopBlock);
        layer.Sprites.Add(sprite);
        doc.Layers.Add(layer);

        doc.CommandRecords["cmd_hdr_001"] = new CommandRecord
        {
            CommandId = "cmd_hdr_001",
            Lifecycle = CommandRecordLifecycle.ImportedUnchanged,
            ParentBlockId = "blk_001",
            ParentLayerId = "layer_0",
        };
        doc.CommandRecords["cmd_rel_001"] = new CommandRecord
        {
            CommandId = "cmd_rel_001",
            Lifecycle = CommandRecordLifecycle.ImportedUnchanged,
            ParentBlockId = "blk_001",
            ParentLayerId = "layer_0",
        };

        return doc;
    }

    private static EditorState BuildState() => new() { Document = BuildDocumentWithLoopBlock() };

    [Fact]
    public void CreateTransaction_ValidLoopBlock_PassesABCDFLValidation()
    {
        var doc = BuildDocumentWithLoopBlock();
        var (tx, diagnostics) = ExpandBlockOperations.CreateTransaction(doc, "layer_0", "blk_001");

        Assert.NotNull(tx);
        Assert.False(diagnostics.HasErrors);
        // 3 iterations x 1 relative command = 3 generated commands
        Assert.Equal(3, tx!.GeneratedCommandIds.Count);
        Assert.Equal(3, tx.GeneratedCommandsAfter.Count);
        // InsertionAnchor layer consistency (L)
        Assert.Equal("layer_0", tx.InsertionAnchor.LayerId);
    }

    [Fact]
    public void CreateTransaction_InvalidLayer_ReturnsNullWithDiagnostics()
    {
        var doc = BuildDocumentWithLoopBlock();
        var (tx, diagnostics) = ExpandBlockOperations.CreateTransaction(doc, "nonexistent_layer", "blk_001");

        Assert.Null(tx);
        Assert.True(diagnostics.HasErrors);
    }

    [Fact]
    public void CreateTransaction_NonExistentBlock_ReturnsNullWithDiagnostics()
    {
        var doc = BuildDocumentWithLoopBlock();
        var (tx, diagnostics) = ExpandBlockOperations.CreateTransaction(doc, "layer_0", "nonexistent_block");

        Assert.Null(tx);
        Assert.True(diagnostics.HasErrors);
    }

    [Fact]
    public void ExpandBlock_Undo_RestoresOriginalBlock()
    {
        var state = BuildState();
        // Verify block exists initially
        Assert.Single(state.Document.Layers[0].Sprites[0].Blocks);
        Assert.IsType<LoopBlock>(state.Document.Layers[0].Sprites[0].Blocks[0]);

        // Expand
        var afterExpand = Reducer.Reduce(state, new ExpandBlockAction("layer_0", "blk_001"));
        Assert.NotSame(state, afterExpand); // expand succeeded
        // Block removed after expand
        Assert.Empty(afterExpand.Document.Layers[0].Sprites[0].Blocks);
        // Generated keyframes added (3 iterations of F command)
        var opacityTrack = afterExpand.Document.Layers[0].Sprites[0].PropertyTracks.First(t => t.PropertyName == "Opacity");
        Assert.Equal(3, opacityTrack.FloatKeyframes.Count);
        // Expanded history added
        Assert.Single(afterExpand.Document.ExpandedBlockHistories);
        Assert.Single(afterExpand.UndoStack);
        Assert.Empty(afterExpand.RedoStack);

        // Undo
        var afterUndo = Reducer.Reduce(afterExpand, new UndoAction());

        // Block restored
        Assert.Single(afterUndo.Document.Layers[0].Sprites[0].Blocks);
        var restoredBlock = Assert.IsType<LoopBlock>(afterUndo.Document.Layers[0].Sprites[0].Blocks[0]);
        Assert.Equal("blk_001", restoredBlock.Id);
        Assert.Equal(3, restoredBlock.LoopCount);
        Assert.Single(restoredBlock.RelativeCommands);
        // Generated command records removed (only header + relative remain)
        Assert.Equal(2, afterUndo.Document.CommandRecords.Count);
        Assert.True(afterUndo.Document.CommandRecords.ContainsKey("cmd_hdr_001"));
        Assert.True(afterUndo.Document.CommandRecords.ContainsKey("cmd_rel_001"));
        // Header/rel records restored to ImportedUnchanged
        Assert.Equal(CommandRecordLifecycle.ImportedUnchanged, afterUndo.Document.CommandRecords["cmd_hdr_001"].Lifecycle);
        Assert.Equal(CommandRecordLifecycle.ImportedUnchanged, afterUndo.Document.CommandRecords["cmd_rel_001"].Lifecycle);
        // Expanded history removed
        Assert.Empty(afterUndo.Document.ExpandedBlockHistories);
        // Stacks: undo popped, redo pushed
        Assert.Empty(afterUndo.UndoStack);
        Assert.Single(afterUndo.RedoStack);
    }

    [Fact]
    public void ExpandBlock_Redo_Replays_DoesNotRecompute()
    {
        var state = BuildState();

        // Expand
        var afterExpand = Reducer.Reduce(state, new ExpandBlockAction("layer_0", "blk_001"));
        // Capture the generated command ids from the expanded state (these are the "replay" ids)
        var generatedIds = afterExpand.Document.CommandRecords.Keys
            .Where(k => k != "cmd_hdr_001" && k != "cmd_rel_001")
            .ToList();
        Assert.Equal(3, generatedIds.Count);

        // Undo
        var afterUndo = Reducer.Reduce(afterExpand, new UndoAction());
        // Generated ids gone after undo
        foreach (var genId in generatedIds)
            Assert.False(afterUndo.Document.CommandRecords.ContainsKey(genId));

        // Redo (replay, not recompute)
        var afterRedo = Reducer.Reduce(afterUndo, new RedoAction());

        // Block removed again
        Assert.Empty(afterRedo.Document.Layers[0].Sprites[0].Blocks);
        // Generated command records restored with SAME ids (replay, not recompute)
        foreach (var genId in generatedIds)
            Assert.True(afterRedo.Document.CommandRecords.ContainsKey(genId),
                $"Generated id '{genId}' should exist after replay redo (not recompute)");
        Assert.Equal(5, afterRedo.Document.CommandRecords.Count); // header + rel + 3 generated
        // Generated keyframes restored
        var opacityTrack = afterRedo.Document.Layers[0].Sprites[0].PropertyTracks.First(t => t.PropertyName == "Opacity");
        Assert.Equal(3, opacityTrack.FloatKeyframes.Count);
        // Expanded history restored
        Assert.Single(afterRedo.Document.ExpandedBlockHistories);
        // Stacks: undo pushed, redo popped
        Assert.Single(afterRedo.UndoStack);
        Assert.Empty(afterRedo.RedoStack);
    }

    [Fact]
    public void ExpandBlock_FailedExpand_DoesNotChangeStacks()
    {
        var state = BuildState();
        // Attempt to expand a non-existent block
        var afterFailedExpand = Reducer.Reduce(state, new ExpandBlockAction("layer_0", "nonexistent_block"));

        Assert.Same(state, afterFailedExpand); // no change
        Assert.Empty(afterFailedExpand.UndoStack);
        Assert.Empty(afterFailedExpand.RedoStack);
        // Block still present
        Assert.Single(afterFailedExpand.Document.Layers[0].Sprites[0].Blocks);
    }
}
