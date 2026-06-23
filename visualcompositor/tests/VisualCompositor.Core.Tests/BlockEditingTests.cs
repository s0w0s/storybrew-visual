using VisualCompositor.Core.Model;
using VisualCompositor.Core.Primitives;
using VisualCompositor.Core.Validation;
using VisualCompositor.State;
using VisualCompositor.State.Actions;
using VisualCompositor.State.Reducer;

namespace VisualCompositor.Core.Tests;

public class BlockEditingTests
{
    private static CompositionDocument BuildDocumentWithLoopBlocks()
    {
        var doc = new CompositionDocument();
        var layer = new Layer { Id = "layer_0", Name = "BG", OsbLayer = OsbLayer.Background };
        var sprite = new SpriteDeclaration { Id = "spr_001", LayerId = "layer_0", TexturePath = "bg.png" };

        // Three loop blocks on the sprite
        var block1 = new LoopBlock
        {
            Id = "blk_001",
            LayerId = "layer_0",
            HeaderCommandId = "cmd_hdr_001",
            StartTime = 1000,
            LoopCount = 2,
            RelativeCommands = new List<RelativeCommand>
            {
                new() { Id = "cmd_rel_001", CommandType = "F", StartTime = 0, EndTime = 100, StartValue = "0", EndValue = "1", ParentBlockId = "blk_001" },
            },
        };
        var block2 = new LoopBlock
        {
            Id = "blk_002",
            LayerId = "layer_0",
            HeaderCommandId = "cmd_hdr_002",
            StartTime = 2000,
            LoopCount = 2,
            RelativeCommands = new List<RelativeCommand>
            {
                new() { Id = "cmd_rel_002", CommandType = "F", StartTime = 0, EndTime = 100, StartValue = "0", EndValue = "1", ParentBlockId = "blk_002" },
            },
        };
        var block3 = new LoopBlock
        {
            Id = "blk_003",
            LayerId = "layer_0",
            HeaderCommandId = "cmd_hdr_003",
            StartTime = 3000,
            LoopCount = 2,
            RelativeCommands = new List<RelativeCommand>
            {
                new() { Id = "cmd_rel_003", CommandType = "F", StartTime = 0, EndTime = 100, StartValue = "0", EndValue = "1", ParentBlockId = "blk_003" },
            },
        };
        sprite.Blocks.Add(block1);
        sprite.Blocks.Add(block2);
        sprite.Blocks.Add(block3);
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
        doc.CommandRecords["cmd_hdr_002"] = new CommandRecord
        {
            CommandId = "cmd_hdr_002",
            Lifecycle = CommandRecordLifecycle.ImportedUnchanged,
            ParentBlockId = "blk_002",
            ParentLayerId = "layer_0",
        };
        doc.CommandRecords["cmd_rel_002"] = new CommandRecord
        {
            CommandId = "cmd_rel_002",
            Lifecycle = CommandRecordLifecycle.ImportedUnchanged,
            ParentBlockId = "blk_002",
            ParentLayerId = "layer_0",
        };
        doc.CommandRecords["cmd_hdr_003"] = new CommandRecord
        {
            CommandId = "cmd_hdr_003",
            Lifecycle = CommandRecordLifecycle.ImportedUnchanged,
            ParentBlockId = "blk_003",
            ParentLayerId = "layer_0",
        };
        doc.CommandRecords["cmd_rel_003"] = new CommandRecord
        {
            CommandId = "cmd_rel_003",
            Lifecycle = CommandRecordLifecycle.ImportedUnchanged,
            ParentBlockId = "blk_003",
            ParentLayerId = "layer_0",
        };

        return doc;
    }

    private static CompositionDocument BuildDocumentWithSingleLoopBlock()
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

    private static EditorState BuildStateWithBlocks() => new() { Document = BuildDocumentWithLoopBlocks() };
    private static EditorState BuildStateWithSingleBlock() => new() { Document = BuildDocumentWithSingleLoopBlock() };

    [Fact]
    public void MoveBlock_ChangesBlockPosition()
    {
        var state = BuildStateWithBlocks();
        var blocks = state.Document.Layers[0].Sprites[0].Blocks;
        Assert.Equal("blk_001", blocks[0].Id);
        Assert.Equal("blk_002", blocks[1].Id);
        Assert.Equal("blk_003", blocks[2].Id);

        // Move blk_001 (index 0) to index 2 (end)
        var afterMove = Reducer.Reduce(state, new MoveBlockAction("layer_0", "blk_001", 2));

        var movedBlocks = afterMove.Document.Layers[0].Sprites[0].Blocks;
        Assert.Equal(3, movedBlocks.Count);
        Assert.Equal("blk_002", movedBlocks[0].Id);
        Assert.Equal("blk_003", movedBlocks[1].Id);
        Assert.Equal("blk_001", movedBlocks[2].Id);
    }

    [Fact]
    public void MoveBlock_PushesToUndoStack()
    {
        var state = BuildStateWithBlocks();
        Assert.Empty(state.UndoStack);

        var afterMove = Reducer.Reduce(state, new MoveBlockAction("layer_0", "blk_001", 2));

        Assert.Single(afterMove.UndoStack);
        Assert.Empty(afterMove.RedoStack);
    }

    [Fact]
    public void RemoveBlock_RemovesBlock_AndMarksCommandRecordAsDeleted()
    {
        var state = BuildStateWithBlocks();
        Assert.Equal(3, state.Document.Layers[0].Sprites[0].Blocks.Count);
        Assert.Equal(CommandRecordLifecycle.ImportedUnchanged,
            state.Document.CommandRecords["cmd_hdr_001"].Lifecycle);
        Assert.Equal(CommandRecordLifecycle.ImportedUnchanged,
            state.Document.CommandRecords["cmd_rel_001"].Lifecycle);

        var afterRemove = Reducer.Reduce(state, new RemoveBlockAction("layer_0", "blk_001"));

        // Block removed from container
        Assert.Equal(2, afterRemove.Document.Layers[0].Sprites[0].Blocks.Count);
        Assert.DoesNotContain(afterRemove.Document.Layers[0].Sprites[0].Blocks, b => b.Id == "blk_001");

        // CommandRecord NOT removed from dictionary — marked as Deleted instead
        Assert.True(afterRemove.Document.CommandRecords.ContainsKey("cmd_hdr_001"));
        Assert.True(afterRemove.Document.CommandRecords.ContainsKey("cmd_rel_001"));
        Assert.Equal(CommandRecordLifecycle.Deleted,
            afterRemove.Document.CommandRecords["cmd_hdr_001"].Lifecycle);
        Assert.Equal(CommandRecordLifecycle.Deleted,
            afterRemove.Document.CommandRecords["cmd_rel_001"].Lifecycle);

        // Other blocks' records untouched
        Assert.Equal(CommandRecordLifecycle.ImportedUnchanged,
            afterRemove.Document.CommandRecords["cmd_hdr_002"].Lifecycle);
    }

    [Fact]
    public void RemoveBlock_PushesToUndoStack()
    {
        var state = BuildStateWithBlocks();
        Assert.Empty(state.UndoStack);

        var afterRemove = Reducer.Reduce(state, new RemoveBlockAction("layer_0", "blk_001"));

        Assert.Single(afterRemove.UndoStack);
        Assert.Empty(afterRemove.RedoStack);
    }

    [Fact]
    public void ExpandBlock_RemovesBlock_AndAddsGeneratedKeyframes()
    {
        var state = BuildStateWithSingleBlock();
        Assert.Single(state.Document.Layers[0].Sprites[0].Blocks);
        Assert.IsType<LoopBlock>(state.Document.Layers[0].Sprites[0].Blocks[0]);

        var afterExpand = Reducer.Reduce(state, new ExpandBlockAction("layer_0", "blk_001"));

        Assert.NotSame(state, afterExpand); // expand succeeded
        // Block removed after expand
        Assert.Empty(afterExpand.Document.Layers[0].Sprites[0].Blocks);
        // Generated keyframes added (3 iterations of F command)
        var opacityTrack = afterExpand.Document.Layers[0].Sprites[0].PropertyTracks
            .First(t => t.PropertyName == "Opacity");
        Assert.Equal(3, opacityTrack.FloatKeyframes.Count);
        // Expanded history added
        Assert.Single(afterExpand.Document.ExpandedBlockHistories);
    }

    [Fact]
    public void ExpandBlock_PushesTransactionToUndoStack()
    {
        var state = BuildStateWithSingleBlock();
        Assert.Empty(state.UndoStack);

        var afterExpand = Reducer.Reduce(state, new ExpandBlockAction("layer_0", "blk_001"));

        Assert.Single(afterExpand.UndoStack);
        Assert.Empty(afterExpand.RedoStack);
        // The undo entry must carry the expand transaction for replay
        Assert.NotNull(afterExpand.UndoStack[0].ExpandTransaction);
    }

    [Fact]
    public void Undo_ExpandBlock_RestoresOriginalBlock()
    {
        var state = BuildStateWithSingleBlock();
        Assert.Single(state.Document.Layers[0].Sprites[0].Blocks);

        var afterExpand = Reducer.Reduce(state, new ExpandBlockAction("layer_0", "blk_001"));
        Assert.Empty(afterExpand.Document.Layers[0].Sprites[0].Blocks);
        Assert.Single(afterExpand.UndoStack);

        var afterUndo = Reducer.Reduce(afterExpand, new UndoAction());

        // Block restored
        Assert.Single(afterUndo.Document.Layers[0].Sprites[0].Blocks);
        var restoredBlock = Assert.IsType<LoopBlock>(afterUndo.Document.Layers[0].Sprites[0].Blocks[0]);
        Assert.Equal("blk_001", restoredBlock.Id);
        Assert.Equal(3, restoredBlock.LoopCount);
        Assert.Single(restoredBlock.RelativeCommands);
        // Stacks: undo popped, redo pushed
        Assert.Empty(afterUndo.UndoStack);
        Assert.Single(afterUndo.RedoStack);
        // Redo entry also carries the transaction
        Assert.NotNull(afterUndo.RedoStack[0].ExpandTransaction);
    }

    [Fact]
    public void FailedExpandBlock_InvalidBlock_DoesNotChangeState()
    {
        var state = BuildStateWithSingleBlock();
        Assert.Single(state.Document.Layers[0].Sprites[0].Blocks);
        Assert.Empty(state.UndoStack);
        Assert.Empty(state.RedoStack);

        // Attempt to expand a non-existent block
        var afterFailedExpand = Reducer.Reduce(state, new ExpandBlockAction("layer_0", "nonexistent_block"));

        Assert.Same(state, afterFailedExpand); // no change
        Assert.Empty(afterFailedExpand.UndoStack);
        Assert.Empty(afterFailedExpand.RedoStack);
        // Block still present
        Assert.Single(afterFailedExpand.Document.Layers[0].Sprites[0].Blocks);
    }

    [Fact]
    public void FailedRemoveBlock_NonExistentBlock_DoesNotChangeState()
    {
        var state = BuildStateWithBlocks();
        Assert.Empty(state.UndoStack);

        var afterFailed = Reducer.Reduce(state, new RemoveBlockAction("layer_0", "nonexistent_block"));

        Assert.Same(state, afterFailed);
        Assert.Empty(afterFailed.UndoStack);
        Assert.Equal(3, afterFailed.Document.Layers[0].Sprites[0].Blocks.Count);
    }
}
