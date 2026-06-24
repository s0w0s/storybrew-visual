using VisualCompositor.Core.Model;
using VisualCompositor.Core.Primitives;
using Xunit;

namespace VisualCompositor.Core.Tests;

public class CompositionDocumentTests
{
    [Fact]
    public void NewDocument_HasSchemaVersion2()
    {
        var doc = new CompositionDocument();
        Assert.Equal(2, doc.SchemaVersion);
    }

    [Fact]
    public void NewDocument_HasEmptyCollections()
    {
        var doc = new CompositionDocument();
        Assert.Empty(doc.Layers);
        Assert.Empty(doc.Markers);
        Assert.Empty(doc.RawBlocks);
        Assert.Empty(doc.CommandRecords);
        Assert.Empty(doc.ExpandedBlockHistories);
        Assert.Empty(doc.Settings);
        Assert.Empty(doc.Variables);
    }

    [Fact]
    public void Clone_ProducesDeepCopy()
    {
        var doc = new CompositionDocument();
        doc.Layers.Add(new Layer { Id = "layer_0", Name = "BG", OsbLayer = OsbLayer.Background });
        doc.CommandRecords["cmd_001"] = new CommandRecord { CommandId = "cmd_001", ParentLayerId = "layer_0" };
        doc.Variables["$x"] = "100";

        var clone = doc.Clone();

        Assert.NotSame(doc, clone);
        Assert.NotSame(doc.Layers, clone.Layers);
        Assert.Equal(doc.Layers[0].Id, clone.Layers[0].Id);
        Assert.NotSame(doc.CommandRecords, clone.CommandRecords);
        Assert.NotSame(doc.Variables, clone.Variables);
        Assert.Equal("100", clone.Variables["$x"]);

        // Modify clone, original unchanged
        clone.Layers[0].Id = "changed";
        Assert.Equal("layer_0", doc.Layers[0].Id);
    }

    [Fact]
    public void Layer_Clone_ProducesDeepCopy()
    {
        var layer = new Layer { Id = "layer_0", Name = "BG", OsbLayer = OsbLayer.Background };
        layer.Sprites.Add(new SpriteDeclaration { Id = "spr_001", TexturePath = "bg.png" });

        var clone = layer.Clone();

        Assert.NotSame(layer.Sprites, clone.Sprites);
        Assert.Equal("spr_001", clone.Sprites[0].Id);
    }

    [Fact]
    public void LoopBlock_Clone_ProducesDeepCopy()
    {
        var block = new LoopBlock
        {
            Id = "blk_001",
            HeaderCommandId = "cmd_hdr",
            StartTime = 1000,
            LoopCount = 5,
            RelativeCommands = new List<RelativeCommand>
            {
                new() { Id = "cmd_rel_001", CommandType = "F" },
            },
        };

        var clone = block.Clone();

        Assert.NotSame(block.RelativeCommands, clone.RelativeCommands);
        Assert.Equal("cmd_rel_001", clone.RelativeCommands[0].Id);
    }

    [Fact]
    public void TriggerBlock_Clone_ProducesDeepCopy()
    {
        var block = new TriggerBlock
        {
            Id = "blk_002",
            HeaderCommandId = "cmd_hdr2",
            TriggerName = "HitObjects",
            StartTime = 500,
            EndTime = 1500,
            Group = 1,
            RelativeCommands = new List<RelativeCommand>
            {
                new() { Id = "cmd_rel_002", CommandType = "M" },
            },
        };

        var clone = block.Clone();

        Assert.Equal("Trigger", block.BlockType);
        Assert.NotSame(block.RelativeCommands, clone.RelativeCommands);
        Assert.Equal("HitObjects", clone.TriggerName);
    }

    [Fact]
    public void PropertyTrack_Clone_ProducesDeepCopy()
    {
        var track = new PropertyTrack { PropertyName = "Opacity", ValueType = "Float" };
        track.FloatKeyframes.Add(new Keyframe<float> { Time = 100, Value = 0.5f, Easing = OsbEasing.In });

        var clone = track.Clone();

        Assert.NotSame(track.FloatKeyframes, clone.FloatKeyframes);
        Assert.Equal(0.5f, clone.FloatKeyframes[0].Value);
    }

    [Fact]
    public void ParameterSegment_NullEndTime_RepresentsOpenEnded()
    {
        var seg = new ParameterSegment
        {
            Parameter = ParameterType.AdditiveBlending,
            StartTime = 100,
            EndTime = null,
            OpenEndedMode = OpenEndedMode.UntilLayerEnd,
        };

        Assert.Null(seg.EndTime);
        Assert.Equal(OpenEndedMode.UntilLayerEnd, seg.OpenEndedMode);
    }

    [Fact]
    public void RawBlock_LayerStartAnchor_HasNullAnchorCommandId()
    {
        var raw = new RawBlock
        {
            Id = "raw_001",
            Content = "// comment",
            AnchorKind = RawBlockAnchorKind.LayerStart,
            AnchorCommandId = null,
        };

        Assert.Null(raw.AnchorCommandId);
    }

    [Fact]
    public void RawBlock_AfterCommandAnchor_HasNonNullAnchorCommandId()
    {
        var raw = new RawBlock
        {
            Id = "raw_002",
            Content = "unknown_line",
            AnchorKind = RawBlockAnchorKind.AfterCommand,
            AnchorCommandId = "cmd_001",
        };

        Assert.NotNull(raw.AnchorCommandId);
    }
}
