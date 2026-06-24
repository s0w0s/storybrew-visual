using VisualCompositor.Core.Model;
using VisualCompositor.Core.Primitives;
using VisualCompositor.Core.Validation;
using Xunit;

namespace VisualCompositor.Core.Tests;

public class DocumentValidatorsTests
{
    private static CompositionDocument CreateValidDocument()
    {
        var doc = new CompositionDocument();
        var layer = new Layer { Id = "layer_0", Name = "Background", OsbLayer = OsbLayer.Background };
        var sprite = new SpriteDeclaration { Id = "spr_001", LayerId = "layer_0", TexturePath = "bg.png" };

        var loopBlock = new LoopBlock
        {
            Id = "blk_001",
            LayerId = "layer_0",
            HeaderCommandId = "cmd_header_001",
            StartTime = 1000,
            LoopCount = 5,
            RelativeCommands = new List<RelativeCommand>
            {
                new() { Id = "cmd_rel_001", CommandType = "F", ParentBlockId = "blk_001" },
            },
        };
        sprite.Blocks.Add(loopBlock);
        layer.Sprites.Add(sprite);
        doc.Layers.Add(layer);

        doc.CommandRecords["cmd_header_001"] = new CommandRecord
        {
            CommandId = "cmd_header_001",
            Lifecycle = CommandRecordLifecycle.ImportedUnchanged,
            ParentBlockId = "blk_001",
            ParentLayerId = "layer_0",
            DerivedCommandIds = new List<string>(),
        };
        doc.CommandRecords["cmd_rel_001"] = new CommandRecord
        {
            CommandId = "cmd_rel_001",
            Lifecycle = CommandRecordLifecycle.ImportedUnchanged,
            ParentBlockId = "blk_001",
            ParentLayerId = "layer_0",
            DerivedCommandIds = new List<string>(),
        };

        return doc;
    }

    // G: RawBlockAnchorTargetExistence
    [Fact]
    public void G_ValidRawBlockAnchors_Pass()
    {
        var doc = CreateValidDocument();
        doc.RawBlocks.Add(new RawBlock { Id = "raw_001", Content = "// comment", AnchorKind = RawBlockAnchorKind.AfterCommand, AnchorCommandId = "cmd_header_001", LayerId = "layer_0" });
        doc.RawBlocks.Add(new RawBlock { Id = "raw_002", Content = "", AnchorKind = RawBlockAnchorKind.LayerStart, AnchorCommandId = null, LayerId = "layer_0" });
        var diagnostics = new DiagnosticCollection();
        DocumentValidators.ValidateRawBlockAnchorTargetExistence(doc, diagnostics);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void G_LayerStartWithNonNullAnchor_Fails()
    {
        var doc = CreateValidDocument();
        doc.RawBlocks.Add(new RawBlock { Id = "raw_001", Content = "", AnchorKind = RawBlockAnchorKind.LayerStart, AnchorCommandId = "cmd_header_001", LayerId = "layer_0" });
        var diagnostics = new DiagnosticCollection();
        DocumentValidators.ValidateRawBlockAnchorTargetExistence(doc, diagnostics);
        Assert.True(diagnostics.HasErrors);
        Assert.Contains(diagnostics.Diagnostics, d => d.Code == FailureCodes.RAW_BLOCK_ANCHOR_INVALID_COMBINATION);
    }

    [Fact]
    public void G_AfterCommandWithNullAnchor_Fails()
    {
        var doc = CreateValidDocument();
        doc.RawBlocks.Add(new RawBlock { Id = "raw_001", Content = "", AnchorKind = RawBlockAnchorKind.AfterCommand, AnchorCommandId = null, LayerId = "layer_0" });
        var diagnostics = new DiagnosticCollection();
        DocumentValidators.ValidateRawBlockAnchorTargetExistence(doc, diagnostics);
        Assert.True(diagnostics.HasErrors);
        Assert.Contains(diagnostics.Diagnostics, d => d.Code == FailureCodes.RAW_BLOCK_ANCHOR_INVALID_COMBINATION);
    }

    [Fact]
    public void G_AfterCommandMissingTarget_Fails()
    {
        var doc = CreateValidDocument();
        doc.RawBlocks.Add(new RawBlock { Id = "raw_001", Content = "", AnchorKind = RawBlockAnchorKind.AfterCommand, AnchorCommandId = "cmd_nonexistent", LayerId = "layer_0" });
        var diagnostics = new DiagnosticCollection();
        DocumentValidators.ValidateRawBlockAnchorTargetExistence(doc, diagnostics);
        Assert.True(diagnostics.HasErrors);
        Assert.Contains(diagnostics.Diagnostics, d => d.Code == FailureCodes.RAW_BLOCK_ANCHOR_TARGET_MISSING);
    }

    // H: RelativeCommandRecordIdentity
    [Fact]
    public void H_ValidRelativeCommands_Pass()
    {
        var doc = CreateValidDocument();
        var diagnostics = new DiagnosticCollection();
        DocumentValidators.ValidateRelativeCommandRecordIdentity(doc, diagnostics);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void H_MissingRelativeCommandRecord_Fails()
    {
        var doc = CreateValidDocument();
        var loopBlock = (LoopBlock)doc.Layers[0].Sprites[0].Blocks[0];
        loopBlock.RelativeCommands.Add(new RelativeCommand { Id = "cmd_nonexistent", CommandType = "F", ParentBlockId = "blk_001" });
        var diagnostics = new DiagnosticCollection();
        DocumentValidators.ValidateRelativeCommandRecordIdentity(doc, diagnostics);
        Assert.True(diagnostics.HasErrors);
        Assert.Contains(diagnostics.Diagnostics, d => d.Code == FailureCodes.RELATIVE_COMMAND_RECORD_MISSING);
    }

    // I: HeaderCommandIdentity
    [Fact]
    public void H_ValidHeaderCommand_Passes()
    {
        var doc = CreateValidDocument();
        var diagnostics = new DiagnosticCollection();
        DocumentValidators.ValidateHeaderCommandIdentity(doc, diagnostics);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void I_MissingHeaderRecord_Fails()
    {
        var doc = CreateValidDocument();
        var loopBlock = (LoopBlock)doc.Layers[0].Sprites[0].Blocks[0];
        loopBlock.HeaderCommandId = "cmd_nonexistent";
        var diagnostics = new DiagnosticCollection();
        DocumentValidators.ValidateHeaderCommandIdentity(doc, diagnostics);
        Assert.True(diagnostics.HasErrors);
        Assert.Contains(diagnostics.Diagnostics, d => d.Code == FailureCodes.HEADER_COMMAND_RECORD_MISSING);
    }

    [Fact]
    public void I_HeaderParentMismatch_Fails()
    {
        var doc = CreateValidDocument();
        doc.CommandRecords["cmd_header_001"].ParentBlockId = "wrong_block";
        var diagnostics = new DiagnosticCollection();
        DocumentValidators.ValidateHeaderCommandIdentity(doc, diagnostics);
        Assert.True(diagnostics.HasErrors);
        Assert.Contains(diagnostics.Diagnostics, d => d.Code == FailureCodes.HEADER_COMMAND_PARENT_MISMATCH);
    }

    // J: OpaquePersistentIds
    [Fact]
    public void J_UniqueIds_Pass()
    {
        var doc = CreateValidDocument();
        var diagnostics = new DiagnosticCollection();
        DocumentValidators.ValidateOpaquePersistentIds(doc, diagnostics);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void J_EmptyId_Fails()
    {
        var doc = CreateValidDocument();
        doc.Layers[0].Id = "";
        var diagnostics = new DiagnosticCollection();
        DocumentValidators.ValidateOpaquePersistentIds(doc, diagnostics);
        Assert.True(diagnostics.HasErrors);
        Assert.Contains(diagnostics.Diagnostics, d => d.Code == FailureCodes.ID_EMPTY);
    }

    [Fact]
    public void J_DuplicateId_Fails()
    {
        var doc = CreateValidDocument();
        doc.Layers[0].Sprites[0].Id = "layer_0"; // duplicate with layer id
        var diagnostics = new DiagnosticCollection();
        DocumentValidators.ValidateOpaquePersistentIds(doc, diagnostics);
        Assert.True(diagnostics.HasErrors);
        Assert.Contains(diagnostics.Diagnostics, d => d.Code == FailureCodes.ID_DUPLICATE);
    }

    // E: DerivedCommandPartition
    [Fact]
    public void E_EmptyDerivedIds_Pass()
    {
        var doc = CreateValidDocument();
        var diagnostics = new DiagnosticCollection();
        DocumentValidators.ValidateDerivedCommandPartition(doc, diagnostics);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void E_ValidPartition_Passes()
    {
        var doc = CreateValidDocument();
        // Header has derived ids = union of relative derived ids
        doc.CommandRecords["cmd_header_001"].DerivedCommandIds = new List<string> { "cmd_gen_001", "cmd_gen_002" };
        doc.CommandRecords["cmd_rel_001"].DerivedCommandIds = new List<string> { "cmd_gen_001", "cmd_gen_002" };
        var diagnostics = new DiagnosticCollection();
        DocumentValidators.ValidateDerivedCommandPartition(doc, diagnostics);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void E_NonHeaderDerivedNotInHeader_Fails()
    {
        var doc = CreateValidDocument();
        doc.CommandRecords["cmd_rel_001"].DerivedCommandIds = new List<string> { "cmd_gen_orphan" };
        // Header has empty derived, but relative has one not in header
        var diagnostics = new DiagnosticCollection();
        DocumentValidators.ValidateDerivedCommandPartition(doc, diagnostics);
        Assert.True(diagnostics.HasErrors);
        Assert.Contains(diagnostics.Diagnostics, d => d.Code == FailureCodes.DERIVED_COMMAND_PARTITION_MISMATCH);
    }
}
