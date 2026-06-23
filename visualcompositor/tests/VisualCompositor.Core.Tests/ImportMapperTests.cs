using VisualCompositor.Core.Model;
using VisualCompositor.Core.Primitives;
using VisualCompositor.Core.Validation;
using VisualCompositor.Osb.Import;
using VisualCompositor.Osb.Parser;
using Xunit;

namespace VisualCompositor.Core.Tests;

public class ImportMapperTests
{
    private static ImportResult Import(string osb)
    {
        var parsed = new OsbParser().Parse(osb);
        return new ImportMapper().Map(parsed);
    }

    private static SpriteDeclaration FirstSprite(ImportResult result)
    {
        Assert.NotEmpty(result.Document.Layers);
        Assert.NotEmpty(result.Document.Layers[0].Sprites);
        return result.Document.Layers[0].Sprites[0];
    }

    [Fact]
    public void Import_MCommand_MapsToPositionTrackWithBothMask()
    {
        var result = Import("""
[Events]
Sprite,3,4,"bg.jpg",320,240
 M,0,0,2000,320,240,400,240
""");
        var sprite = FirstSprite(result);
        var track = Assert.Single(sprite.PropertyTracks);
        Assert.Equal("Position", track.PropertyName);
        Assert.Equal(Vector2ComponentMask.Both, track.ComponentMask);
        Assert.Equal("Vector2", track.ValueType);
        Assert.Equal(2, track.Vector2Keyframes.Count);
        Assert.Equal(new Vector2(320, 240), track.Vector2Keyframes[0].Value);
        Assert.Equal(new Vector2(400, 240), track.Vector2Keyframes[1].Value);
    }

    [Fact]
    public void Import_MXCommand_MapsToPositionTrackWithXMask()
    {
        var result = Import("""
[Events]
Sprite,3,4,"bg.jpg",320,240
 MX,0,0,1000,320,400
""");
        var sprite = FirstSprite(result);
        var track = Assert.Single(sprite.PropertyTracks);
        Assert.Equal("Position", track.PropertyName);
        Assert.Equal(Vector2ComponentMask.X, track.ComponentMask);
        Assert.Equal("Float", track.ValueType);
        Assert.Equal(2, track.FloatKeyframes.Count);
        Assert.Equal(320f, track.FloatKeyframes[0].Value);
        Assert.Equal(400f, track.FloatKeyframes[1].Value);
    }

    [Fact]
    public void Import_MYCommand_MapsToPositionTrackWithYMask()
    {
        var result = Import("""
[Events]
Sprite,3,4,"bg.jpg",320,240
 MY,0,0,1000,240,300
""");
        var sprite = FirstSprite(result);
        var track = Assert.Single(sprite.PropertyTracks);
        Assert.Equal("Position", track.PropertyName);
        Assert.Equal(Vector2ComponentMask.Y, track.ComponentMask);
        Assert.Equal("Float", track.ValueType);
    }

    [Fact]
    public void Import_FCommand_MapsToOpacityTrack()
    {
        var result = Import("""
[Events]
Sprite,3,4,"bg.jpg",320,240
 F,0,0,1000,0,1
""");
        var sprite = FirstSprite(result);
        var track = Assert.Single(sprite.PropertyTracks);
        Assert.Equal("Opacity", track.PropertyName);
        Assert.Equal("Float", track.ValueType);
        Assert.Equal(2, track.FloatKeyframes.Count);
        Assert.Equal(0f, track.FloatKeyframes[0].Value);
        Assert.Equal(1f, track.FloatKeyframes[1].Value);
    }

    [Fact]
    public void Import_SCommand_MapsToScaleTrackWithVector2Keyframes()
    {
        var result = Import("""
[Events]
Sprite,3,4,"bg.jpg",320,240
 S,0,0,1000,1,2
""");
        var sprite = FirstSprite(result);
        var track = Assert.Single(sprite.PropertyTracks);
        Assert.Equal("Scale", track.PropertyName);
        Assert.Equal(Vector2ComponentMask.Both, track.ComponentMask);
        Assert.Equal(new Vector2(1, 1), track.Vector2Keyframes[0].Value);
        Assert.Equal(new Vector2(2, 2), track.Vector2Keyframes[1].Value);
    }

    [Fact]
    public void Import_VCommand_MapsToScaleTrackWithSeparateComponents()
    {
        var result = Import("""
[Events]
Sprite,3,4,"bg.jpg",320,240
 V,0,0,1000,1,2,3,4
""");
        var sprite = FirstSprite(result);
        var track = Assert.Single(sprite.PropertyTracks);
        Assert.Equal("Scale", track.PropertyName);
        Assert.Equal(new Vector2(1, 2), track.Vector2Keyframes[0].Value);
        Assert.Equal(new Vector2(3, 4), track.Vector2Keyframes[1].Value);
    }

    [Fact]
    public void Import_RCommand_MapsToRotationTrack()
    {
        var result = Import("""
[Events]
Sprite,3,4,"bg.jpg",320,240
 R,0,0,1000,0,3.14
""");
        var sprite = FirstSprite(result);
        var track = Assert.Single(sprite.PropertyTracks);
        Assert.Equal("Rotation", track.PropertyName);
        Assert.Equal("Float", track.ValueType);
        Assert.Equal(0f, track.FloatKeyframes[0].Value);
        Assert.Equal(3.14f, track.FloatKeyframes[1].Value, 3);
    }

    [Fact]
    public void Import_CCommand_MapsToColorTrack()
    {
        var result = Import("""
[Events]
Sprite,3,4,"bg.jpg",320,240
 C,0,0,1000,255,0,0,0,0,255
""");
        var sprite = FirstSprite(result);
        var track = Assert.Single(sprite.PropertyTracks);
        Assert.Equal("Color", track.PropertyName);
        Assert.Equal("Color", track.ValueType);
        Assert.Equal(new Color3(1f, 0f, 0f), track.ColorKeyframes[0].Value);
        Assert.Equal(new Color3(0f, 0f, 1f), track.ColorKeyframes[1].Value);
    }

    [Fact]
    public void Import_PCommand_MapsToParameterTrackWithUntilLayerEnd()
    {
        var result = Import("""
[Events]
Sprite,3,4,"bg.jpg",320,240
 P,0,1000,,A
""");
        var sprite = FirstSprite(result);
        Assert.NotNull(sprite.ParameterTrack);
        var seg = Assert.Single(sprite.ParameterTrack!.Segments);
        Assert.Equal(ParameterType.AdditiveBlending, seg.Parameter);
        Assert.Equal(1000, seg.StartTime);
        Assert.Null(seg.EndTime);
        Assert.Equal(OpenEndedMode.UntilLayerEnd, seg.OpenEndedMode);
    }

    [Fact]
    public void Import_PCommand_WithExplicitEnd_UsesExplicitEnd()
    {
        var result = Import("""
[Events]
Sprite,3,4,"bg.jpg",320,240
 P,0,1000,2000,H
""");
        var sprite = FirstSprite(result);
        Assert.NotNull(sprite.ParameterTrack);
        var seg = Assert.Single(sprite.ParameterTrack!.Segments);
        Assert.Equal(ParameterType.FlipHorizontal, seg.Parameter);
        Assert.Equal(2000, seg.EndTime);
        Assert.Equal(OpenEndedMode.ExplicitEnd, seg.OpenEndedMode);
    }

    [Fact]
    public void Import_LoopBlock_CreatesLoopBlockWithHeaderCommandRecord()
    {
        var result = Import("""
[Events]
Sprite,3,4,"bg.jpg",320,240
 L,1000,3
  F,0,0,500,0,1
  F,1,500,1000,1,0
""");
        var sprite = FirstSprite(result);
        var block = Assert.Single(sprite.Blocks);
        var loop = Assert.IsType<LoopBlock>(block);
        Assert.Equal("Loop", loop.BlockType);
        Assert.Equal(1000, loop.StartTime);
        Assert.Equal(3, loop.LoopCount);
        Assert.Equal(2, loop.RelativeCommands.Count);

        // Header command record exists with ParentBlockId == block.Id
        Assert.True(result.Document.CommandRecords.ContainsKey(loop.HeaderCommandId));
        var headerRecord = result.Document.CommandRecords[loop.HeaderCommandId];
        Assert.Equal(loop.Id, headerRecord.ParentBlockId);
        Assert.Equal(sprite.LayerId, headerRecord.ParentLayerId);

        // Each relative command has a record with ParentBlockId == block.Id
        foreach (var rel in loop.RelativeCommands)
        {
            Assert.True(result.Document.CommandRecords.ContainsKey(rel.Id));
            Assert.Equal(loop.Id, result.Document.CommandRecords[rel.Id].ParentBlockId);
            Assert.Equal(rel.Id, result.Document.CommandRecords[rel.Id].CommandId);
        }
    }

    [Fact]
    public void Import_TriggerBlock_CreatesTriggerBlockWithHeaderCommandRecord()
    {
        var result = Import("""
[Events]
Sprite,3,4,"bg.jpg",320,240
 T,HitObjects,1000,2000,1
  S,0,0,100,1,2
""");
        var sprite = FirstSprite(result);
        var block = Assert.Single(sprite.Blocks);
        var trigger = Assert.IsType<TriggerBlock>(block);
        Assert.Equal("Trigger", trigger.BlockType);
        Assert.Equal("HitObjects", trigger.TriggerName);
        Assert.Equal(1, trigger.Group);
        Assert.Single(trigger.RelativeCommands);

        Assert.True(result.Document.CommandRecords.ContainsKey(trigger.HeaderCommandId));
        var headerRecord = result.Document.CommandRecords[trigger.HeaderCommandId];
        Assert.Equal(trigger.Id, headerRecord.ParentBlockId);
    }

    [Fact]
    public void Import_AllCommandRecords_HaveImportedUnchangedLifecycle()
    {
        var result = Import("""
[Events]
Sprite,3,4,"bg.jpg",320,240
 F,0,0,1000,0,1
 L,1000,3
  F,0,0,500,0,1
""");
        Assert.NotEmpty(result.Document.CommandRecords);
        foreach (var kvp in result.Document.CommandRecords)
        {
            Assert.Equal(CommandRecordLifecycle.ImportedUnchanged, kvp.Value.Lifecycle);
        }
    }

    [Fact]
    public void Import_RawBlocks_FirstOnLayerGetsLayerStart()
    {
        var result = Import("""
[Events]
// comment before sprite
Sprite,3,4,"bg.jpg",320,240
 F,0,0,1000,0,1
""");
        // The comment appears before any command on the layer → LayerStart anchor.
        var rawBlock = Assert.Single(result.Document.RawBlocks);
        Assert.Equal(RawBlockAnchorKind.LayerStart, rawBlock.AnchorKind);
        Assert.Null(rawBlock.AnchorCommandId);
        Assert.Contains("comment", rawBlock.Content);
    }

    [Fact]
    public void Import_RawBlocks_AfterCommandGetsAfterCommandAnchor()
    {
        var result = Import("""
[Events]
Sprite,3,4,"bg.jpg",320,240
 F,0,0,1000,0,1
 // comment after command
""");
        var rawBlock = Assert.Single(result.Document.RawBlocks);
        Assert.Equal(RawBlockAnchorKind.AfterCommand, rawBlock.AnchorKind);
        Assert.NotNull(rawBlock.AnchorCommandId);
        Assert.True(result.Document.CommandRecords.ContainsKey(rawBlock.AnchorCommandId!));
    }

    [Fact]
    public void Import_RawBlocks_MultipleAfterCommandAnchors()
    {
        var result = Import("""
[Events]
Sprite,3,4,"bg.jpg",320,240
 F,0,0,1000,0,1
 // first comment
 // second comment
""");
        Assert.Equal(2, result.Document.RawBlocks.Count);
        Assert.Equal(RawBlockAnchorKind.AfterCommand, result.Document.RawBlocks[0].AnchorKind);
        Assert.Equal(RawBlockAnchorKind.AfterCommand, result.Document.RawBlocks[1].AnchorKind);
        // Both anchor to an existing command
        Assert.True(result.Document.CommandRecords.ContainsKey(result.Document.RawBlocks[0].AnchorCommandId!));
        Assert.True(result.Document.CommandRecords.ContainsKey(result.Document.RawBlocks[1].AnchorCommandId!));
    }

    [Fact]
    public void Import_CreatesLayersForUsedOsbLayers()
    {
        var result = Import("""
[Events]
Sprite,0,4,"bg.jpg",0,0
Sprite,3,4,"fg.jpg",320,240
""");
        Assert.Equal(2, result.Document.Layers.Count);
        Assert.Contains(result.Document.Layers, l => l.OsbLayer == OsbLayer.Background);
        Assert.Contains(result.Document.Layers, l => l.OsbLayer == OsbLayer.Foreground);
    }

    [Fact]
    public void Import_CopiesVariablesToDocument()
    {
        var result = Import("""
[Variables]
$x=320
$y=240

[Events]
Sprite,3,4,"bg.jpg",$x,$y
""");
        Assert.Equal("320", result.Document.Variables["$x"]);
        Assert.Equal("240", result.Document.Variables["$y"]);
    }

    [Fact]
    public void Import_PassesDocumentStateValidation()
    {
        var result = Import("""
[Variables]
$x=320

[Events]
// comment
Sprite,3,4,"bg.jpg",320,240
 F,0,0,1000,0,1
 M,0,0,2000,$x,240,400,240
 L,1000,3
  F,0,0,500,0,1
  F,1,500,1000,1,0
 T,HitObjects,1000,2000,1
  S,0,0,100,1,2
 P,0,3000,,A
 // trailing comment
""");
        var diagnostics = ValidationDispatcher.Validate(result.Document, ValidationEntryPoint.PreOsbExport);
        Assert.False(diagnostics.HasErrors,
            diagnostics.HasErrors
                ? string.Join("\n", diagnostics.Diagnostics.Select(d => d.ToString()))
                : "");
    }

    [Fact]
    public void Import_PassesDocumentStateValidation_DebugIntegrityScan()
    {
        var result = Import("""
[Events]
Sprite,3,4,"bg.jpg",320,240
 F,0,0,1000,0,1
 L,1000,2
  F,0,0,500,0,1
""");
        var diagnostics = ValidationDispatcher.Validate(result.Document, ValidationEntryPoint.DebugIntegrityScan);
        Assert.False(diagnostics.HasErrors,
            diagnostics.HasErrors
                ? string.Join("\n", diagnostics.Diagnostics.Select(d => d.ToString()))
                : "");
    }

    [Fact]
    public void Import_SpriteDeclaration_HasCorrectFields()
    {
        var result = Import("""
[Events]
Sprite,3,4,"bg.jpg",320,240
 F,0,0,1000,0,1
""");
        var sprite = FirstSprite(result);
        Assert.Equal("Sprite", sprite.DeclarationType);
        Assert.Equal(OsbLayer.Foreground, sprite.OsbLayer);
        Assert.Equal(OsbOrigin.Centre, sprite.Origin);
        Assert.Equal("bg.jpg", sprite.TexturePath);
        Assert.Equal(new Vector2(320, 240), sprite.InitialPosition);
        Assert.StartsWith("spr_", sprite.Id);
        Assert.False(string.IsNullOrEmpty(sprite.LayerId));
    }

    [Fact]
    public void Import_AnimationDeclaration_HasAnimationFields()
    {
        var result = Import("""
[Events]
Animation,3,4,"frame.jpg",320,240,8,50,1
 F,0,0,1000,0,1
""");
        var sprite = FirstSprite(result);
        Assert.Equal("Animation", sprite.DeclarationType);
        Assert.Equal(8, sprite.FrameCount);
        Assert.Equal(50, sprite.FrameDelay);
        Assert.Equal(OsbLoopType.LoopOnce, sprite.LoopType);
    }

    [Fact]
    public void Import_AllIdsAreUnique()
    {
        var result = Import("""
[Events]
Sprite,3,4,"bg.jpg",320,240
 F,0,0,1000,0,1
 M,0,0,2000,320,240,400,240
 L,1000,2
  F,0,0,500,0,1
 // comment
""");
        var allIds = new HashSet<string>();
        foreach (var layer in result.Document.Layers)
        {
            Assert.True(allIds.Add(layer.Id), $"Duplicate layer id: {layer.Id}");
            foreach (var sprite in layer.Sprites)
            {
                Assert.True(allIds.Add(sprite.Id), $"Duplicate sprite id: {sprite.Id}");
                foreach (var block in sprite.Blocks)
                {
                    Assert.True(allIds.Add(block.Id), $"Duplicate block id: {block.Id}");
                    Assert.True(allIds.Add(block.HeaderCommandId), $"Duplicate header cmd id: {block.HeaderCommandId}");
                }
            }
        }
        foreach (var raw in result.Document.RawBlocks)
            Assert.True(allIds.Add(raw.Id), $"Duplicate raw id: {raw.Id}");
    }
}
