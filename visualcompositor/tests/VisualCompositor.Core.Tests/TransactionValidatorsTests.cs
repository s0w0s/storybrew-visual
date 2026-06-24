using VisualCompositor.Core.Primitives;
using VisualCompositor.Core.Snapshots;
using VisualCompositor.Core.Validation;
using Xunit;

namespace VisualCompositor.Core.Tests;

public class TransactionValidatorsTests
{
    private static ExpandBlockTransaction CreateValidTransaction(
        StoryboardBlockSnapshot? originalBlockSnapshot = null,
        BlockInsertionAnchor? insertionAnchor = null)
    {
        return new ExpandBlockTransaction
        {
            TransactionId = "txn_001",
            LayerId = "layer_0",
            OriginalBlockSnapshot = originalBlockSnapshot ?? new StoryboardBlockSnapshot
            {
                BlockId = "blk_001",
                LayerId = "layer_0",
                HeaderCommandId = "cmd_header_001",
                BlockType = "Loop",
                RelativeCommands = new List<RelativeCommandSnapshot>
                {
                    new() { Id = "cmd_rel_001", CommandType = "F", ParentBlockId = "blk_001" },
                },
            },
            InsertionAnchor = insertionAnchor ?? new BlockInsertionAnchor { LayerId = "layer_0", OriginalIndexFallback = 0 },
            GeneratedCommandIds = new List<string> { "cmd_gen_001", "cmd_gen_002" },
            GeneratedCommandsAfter = new List<GeneratedCommandSnapshot>
            {
                new() { CommandId = "cmd_gen_001", LayerId = "layer_0", CommandType = "F" },
                new() { CommandId = "cmd_gen_002", LayerId = "layer_0", CommandType = "F" },
            },
            AffectedCommandRecordsBefore = new List<CommandRecordSnapshot>
            {
                new() { CommandId = "cmd_header_001", Lifecycle = CommandRecordLifecycle.Split, ParentBlockId = "blk_001", ParentLayerId = "layer_0" },
                new() { CommandId = "cmd_rel_001", Lifecycle = CommandRecordLifecycle.Split, ParentBlockId = "blk_001", ParentLayerId = "layer_0" },
            },
            AffectedCommandRecordsAfter = new List<CommandRecordSnapshot>
            {
                new() { CommandId = "cmd_header_001", Lifecycle = CommandRecordLifecycle.Split, ParentBlockId = "blk_001", ParentLayerId = "layer_0" },
                new() { CommandId = "cmd_rel_001", Lifecycle = CommandRecordLifecycle.Split, ParentBlockId = "blk_001", ParentLayerId = "layer_0" },
                new() { CommandId = "cmd_gen_001", Lifecycle = CommandRecordLifecycle.Created, ParentLayerId = "layer_0" },
                new() { CommandId = "cmd_gen_002", Lifecycle = CommandRecordLifecycle.Created, ParentLayerId = "layer_0" },
            },
            RawBlockAnchorsBefore = new List<RawBlockAnchorSnapshot>
            {
                new() { RawBlockId = "raw_001", AnchorKind = RawBlockAnchorKind.AfterCommand, AnchorCommandId = "cmd_header_001", LayerId = "layer_0" },
            },
            RawBlockAnchorsAfter = new List<RawBlockAnchorSnapshot>
            {
                new() { RawBlockId = "raw_001", AnchorKind = RawBlockAnchorKind.AfterCommand, AnchorCommandId = "cmd_gen_001", LayerId = "layer_0" },
            },
        };
    }

    // A: GeneratedCommandSetConsistency
    [Fact]
    public void A_Valid_SetConsistency_Passes()
    {
        var tx = CreateValidTransaction();
        var diagnostics = new DiagnosticCollection();
        TransactionValidators.ValidateGeneratedCommandSetConsistency(tx, diagnostics);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void A_SetMismatch_Fails()
    {
        var tx = CreateValidTransaction();
        tx.GeneratedCommandIds.Add("cmd_gen_003"); // extra id not in GeneratedCommandsAfter
        var diagnostics = new DiagnosticCollection();
        TransactionValidators.ValidateGeneratedCommandSetConsistency(tx, diagnostics);
        Assert.True(diagnostics.HasErrors);
        Assert.Contains(diagnostics.Diagnostics, d => d.Code == FailureCodes.EXPAND_TRANSACTION_GENERATED_SET_MISMATCH);
    }

    // B: GeneratedCommandUniqueness
    [Fact]
    public void B_NoDuplicates_Passes()
    {
        var tx = CreateValidTransaction();
        var diagnostics = new DiagnosticCollection();
        TransactionValidators.ValidateGeneratedCommandUniqueness(tx, diagnostics);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void B_DuplicateGeneratedCommandId_Fails()
    {
        var tx = CreateValidTransaction();
        tx.GeneratedCommandIds.Add("cmd_gen_001"); // duplicate
        var diagnostics = new DiagnosticCollection();
        TransactionValidators.ValidateGeneratedCommandUniqueness(tx, diagnostics);
        Assert.True(diagnostics.HasErrors);
        Assert.Contains(diagnostics.Diagnostics, d => d.Code == FailureCodes.EXPAND_TRANSACTION_GENERATED_ID_DUPLICATE);
    }

    [Fact]
    public void B_EmptySnapshotCommandId_Fails()
    {
        var tx = CreateValidTransaction();
        tx.GeneratedCommandsAfter.Add(new GeneratedCommandSnapshot { CommandId = "", LayerId = "layer_0" });
        tx.GeneratedCommandIds.Add(""); // make sets match, but empty id should still fail
        var diagnostics = new DiagnosticCollection();
        TransactionValidators.ValidateGeneratedCommandUniqueness(tx, diagnostics);
        Assert.True(diagnostics.HasErrors);
        Assert.Contains(diagnostics.Diagnostics, d => d.Code == FailureCodes.EXPAND_TRANSACTION_GENERATED_SNAPSHOT_ID_EMPTY);
    }

    [Fact]
    public void B_DuplicateSnapshotCommandId_Fails()
    {
        var tx = CreateValidTransaction();
        tx.GeneratedCommandsAfter.Add(new GeneratedCommandSnapshot { CommandId = "cmd_gen_001", LayerId = "layer_0" });
        tx.GeneratedCommandIds.Add("cmd_gen_001"); // keep set consistent but now duplicate in list
        var diagnostics = new DiagnosticCollection();
        TransactionValidators.ValidateGeneratedCommandUniqueness(tx, diagnostics);
        Assert.True(diagnostics.HasErrors);
        Assert.Contains(diagnostics.Diagnostics, d => d.Code == FailureCodes.EXPAND_TRANSACTION_GENERATED_SNAPSHOT_ID_DUPLICATE);
    }

    // C: OriginalBlockRecordCompleteness
    [Fact]
    public void C_CompleteBeforeRecords_Passes()
    {
        var tx = CreateValidTransaction();
        var diagnostics = new DiagnosticCollection();
        TransactionValidators.ValidateOriginalBlockRecordCompleteness(tx, diagnostics);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void C_MissingHeaderRecord_Fails()
    {
        var tx = CreateValidTransaction(originalBlockSnapshot: new StoryboardBlockSnapshot
        {
            BlockId = "blk_001",
            LayerId = "layer_0",
            HeaderCommandId = "cmd_missing_header",
            BlockType = "Loop",
        });
        var diagnostics = new DiagnosticCollection();
        TransactionValidators.ValidateOriginalBlockRecordCompleteness(tx, diagnostics);
        Assert.True(diagnostics.HasErrors);
        Assert.Contains(diagnostics.Diagnostics, d => d.Code == FailureCodes.EXPAND_ORIGINAL_BLOCK_HEADER_RECORD_MISSING);
    }

    [Fact]
    public void C_MissingRelativeRecord_Fails()
    {
        var tx = CreateValidTransaction();
        tx.OriginalBlockSnapshot.RelativeCommands.Add(
            new RelativeCommandSnapshot { Id = "cmd_missing_rel", CommandType = "F", ParentBlockId = "blk_001" });
        var diagnostics = new DiagnosticCollection();
        TransactionValidators.ValidateOriginalBlockRecordCompleteness(tx, diagnostics);
        Assert.True(diagnostics.HasErrors);
        Assert.Contains(diagnostics.Diagnostics, d => d.Code == FailureCodes.EXPAND_ORIGINAL_BLOCK_RELATIVE_RECORD_MISSING);
    }

    // D: RedoAfterRecordCompleteness
    [Fact]
    public void D_CompleteAfterRecords_Passes()
    {
        var tx = CreateValidTransaction();
        var diagnostics = new DiagnosticCollection();
        TransactionValidators.ValidateRedoAfterRecordCompleteness(tx, diagnostics);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void D_MissingGeneratedRecord_Fails()
    {
        var tx = CreateValidTransaction();
        tx.AffectedCommandRecordsAfter.RemoveAll(r => r.CommandId == "cmd_gen_001");
        var diagnostics = new DiagnosticCollection();
        TransactionValidators.ValidateRedoAfterRecordCompleteness(tx, diagnostics);
        Assert.True(diagnostics.HasErrors);
        Assert.Contains(diagnostics.Diagnostics, d => d.Code == FailureCodes.EXPAND_REDO_AFTER_GENERATED_RECORD_MISSING);
    }

    // F: RawBlockAnchorSnapshotSetConsistency
    [Fact]
    public void F_MatchingAnchorSets_Passes()
    {
        var tx = CreateValidTransaction();
        var diagnostics = new DiagnosticCollection();
        TransactionValidators.ValidateRawBlockAnchorSnapshotSetConsistency(tx, diagnostics);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void F_MismatchedAnchorSets_Fails()
    {
        var tx = CreateValidTransaction();
        tx.RawBlockAnchorsAfter.Add(new RawBlockAnchorSnapshot { RawBlockId = "raw_extra", AnchorKind = RawBlockAnchorKind.LayerStart, LayerId = "layer_0" });
        var diagnostics = new DiagnosticCollection();
        TransactionValidators.ValidateRawBlockAnchorSnapshotSetConsistency(tx, diagnostics);
        Assert.True(diagnostics.HasErrors);
        Assert.Contains(diagnostics.Diagnostics, d => d.Code == FailureCodes.RAW_BLOCK_ANCHOR_SET_MISMATCH);
    }

    // L: InsertionAnchorLayerConsistency
    [Fact]
    public void L_MatchingLayerId_Passes()
    {
        var tx = CreateValidTransaction();
        var diagnostics = new DiagnosticCollection();
        TransactionValidators.ValidateInsertionAnchorLayerConsistency(tx, diagnostics);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void L_MismatchedLayerId_Fails()
    {
        var tx = CreateValidTransaction(insertionAnchor: new BlockInsertionAnchor { LayerId = "wrong_layer", OriginalIndexFallback = 0 });
        var diagnostics = new DiagnosticCollection();
        TransactionValidators.ValidateInsertionAnchorLayerConsistency(tx, diagnostics);
        Assert.True(diagnostics.HasErrors);
        Assert.Contains(diagnostics.Diagnostics, d => d.Code == FailureCodes.INSERTION_ANCHOR_LAYER_MISMATCH);
    }
}
