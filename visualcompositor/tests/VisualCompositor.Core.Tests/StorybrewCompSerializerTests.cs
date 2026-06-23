using VisualCompositor.Core.Model;
using VisualCompositor.Core.Primitives;
using VisualCompositor.Core.Serialization;
using Xunit;

namespace VisualCompositor.Core.Tests;

public class StorybrewCompSerializerTests
{
    [Fact]
    public void Serialize_EmptyDocument_HasSchemaVersion2()
    {
        var doc = new CompositionDocument();
        var json = StorybrewCompSerializer.Serialize(doc);

        Assert.Contains("\"schemaVersion\": 2", json);
    }

    [Fact]
    public void SerializeDeserialize_EmptyDocument_RoundTrips()
    {
        var doc = new CompositionDocument();
        var json = StorybrewCompSerializer.Serialize(doc);
        var result = StorybrewCompSerializer.Deserialize(json);

        Assert.Equal(2, result.Document.SchemaVersion);
        Assert.Empty(result.Document.Layers);
    }

    [Fact]
    public void SerializeDeserialize_WithLayers_RoundTrips()
    {
        var doc = new CompositionDocument();
        doc.Layers.Add(new Layer { Id = "layer_0", Name = "Background", OsbLayer = OsbLayer.Background });
        doc.Layers[0].Sprites.Add(new SpriteDeclaration
        {
            Id = "spr_001",
            LayerId = "layer_0",
            DeclarationType = "Sprite",
            OsbLayer = OsbLayer.Background,
            TexturePath = "bg.png",
            InitialPosition = new Vector2(320, 240),
        });

        var json = StorybrewCompSerializer.Serialize(doc);
        var result = StorybrewCompSerializer.Deserialize(json);

        Assert.Single(result.Document.Layers);
        Assert.Equal("layer_0", result.Document.Layers[0].Id);
        Assert.Equal("Background", result.Document.Layers[0].Name);
        Assert.Single(result.Document.Layers[0].Sprites);
        Assert.Equal("spr_001", result.Document.Layers[0].Sprites[0].Id);
        Assert.Equal("bg.png", result.Document.Layers[0].Sprites[0].TexturePath);
    }

    [Fact]
    public void SerializeDeserialize_WithLoopBlock_RoundTrips()
    {
        var doc = new CompositionDocument();
        var layer = new Layer { Id = "layer_0", Name = "BG", OsbLayer = OsbLayer.Background };
        var sprite = new SpriteDeclaration { Id = "spr_001", LayerId = "layer_0", TexturePath = "bg.png" };
        sprite.Blocks.Add(new LoopBlock
        {
            Id = "blk_001",
            LayerId = "layer_0",
            HeaderCommandId = "cmd_hdr_001",
            StartTime = 1000,
            LoopCount = 5,
            RelativeCommands = new List<RelativeCommand>
            {
                new() { Id = "cmd_rel_001", CommandType = "F", StartTime = 0, EndTime = 100, StartValue = "0", EndValue = "1" },
            },
        });
        layer.Sprites.Add(sprite);
        doc.Layers.Add(layer);

        var json = StorybrewCompSerializer.Serialize(doc);
        var result = StorybrewCompSerializer.Deserialize(json);

        var block = Assert.IsType<LoopBlock>(result.Document.Layers[0].Sprites[0].Blocks[0]);
        Assert.Equal("blk_001", block.Id);
        Assert.Equal(1000, block.StartTime);
        Assert.Equal(5, block.LoopCount);
        Assert.Single(block.RelativeCommands);
        Assert.Equal("cmd_rel_001", block.RelativeCommands[0].Id);
    }

    [Fact]
    public void SerializeDeserialize_WithTriggerBlock_RoundTrips()
    {
        var doc = new CompositionDocument();
        var layer = new Layer { Id = "layer_0", Name = "BG", OsbLayer = OsbLayer.Background };
        var sprite = new SpriteDeclaration { Id = "spr_001", LayerId = "layer_0", TexturePath = "bg.png" };
        sprite.Blocks.Add(new TriggerBlock
        {
            Id = "blk_002",
            LayerId = "layer_0",
            HeaderCommandId = "cmd_hdr_002",
            TriggerName = "HitObjects",
            StartTime = 500,
            EndTime = 1500,
            Group = 1,
        });
        layer.Sprites.Add(sprite);
        doc.Layers.Add(layer);

        var json = StorybrewCompSerializer.Serialize(doc);
        var result = StorybrewCompSerializer.Deserialize(json);

        var block = Assert.IsType<TriggerBlock>(result.Document.Layers[0].Sprites[0].Blocks[0]);
        Assert.Equal("blk_002", block.Id);
        Assert.Equal("HitObjects", block.TriggerName);
    }

    [Fact]
    public void SerializeDeserialize_WithVariables_RoundTrips()
    {
        var doc = new CompositionDocument();
        doc.Variables["$x"] = "100";
        doc.Variables["$y"] = "200";

        var json = StorybrewCompSerializer.Serialize(doc);
        var result = StorybrewCompSerializer.Deserialize(json);

        Assert.Equal("100", result.Document.Variables["$x"]);
        Assert.Equal("200", result.Document.Variables["$y"]);
    }

    [Fact]
    public void SerializeDeserialize_WithRawBlocks_RoundTrips()
    {
        var doc = new CompositionDocument();
        doc.RawBlocks.Add(new RawBlock
        {
            Id = "raw_001",
            Content = "// this is a comment",
            AnchorKind = RawBlockAnchorKind.LayerStart,
            LayerId = "layer_0",
        });

        var json = StorybrewCompSerializer.Serialize(doc);
        var result = StorybrewCompSerializer.Deserialize(json);

        Assert.Single(result.Document.RawBlocks);
        Assert.Equal("// this is a comment", result.Document.RawBlocks[0].Content);
    }

    [Fact]
    public void SerializeDeserialize_WithCommandRecords_RoundTrips()
    {
        var doc = new CompositionDocument();
        doc.CommandRecords["cmd_001"] = new CommandRecord
        {
            CommandId = "cmd_001",
            Lifecycle = CommandRecordLifecycle.ImportedUnchanged,
            ParentLayerId = "layer_0",
            DerivedCommandIds = new List<string> { "cmd_gen_001" },
        };

        var json = StorybrewCompSerializer.Serialize(doc);
        var result = StorybrewCompSerializer.Deserialize(json);

        Assert.True(result.Document.CommandRecords.ContainsKey("cmd_001"));
        Assert.Equal(CommandRecordLifecycle.ImportedUnchanged, result.Document.CommandRecords["cmd_001"].Lifecycle);
        Assert.Contains("cmd_gen_001", result.Document.CommandRecords["cmd_001"].DerivedCommandIds);
    }

    [Fact]
    public void Deserialize_MissingSchemaVersion_MigratesToV2()
    {
        // JSON without schemaVersion
        var json = "{\"layers\":[],\"settings\":{},\"variables\":{}}";
        var result = StorybrewCompSerializer.Deserialize(json);

        Assert.Equal(2, result.Document.SchemaVersion);
        Assert.True(result.WasMigrated);
        Assert.True(result.IsDirty);
    }

    [Fact]
    public void Deserialize_V1Schema_MigratesToV2()
    {
        var json = "{\"schemaVersion\":1,\"layers\":[],\"settings\":{},\"variables\":{}}";
        var result = StorybrewCompSerializer.Deserialize(json);

        Assert.Equal(2, result.Document.SchemaVersion);
        Assert.True(result.WasMigrated);
    }

    [Fact]
    public void Deserialize_MissingCommandRecords_BackfillsAndMarksDirty()
    {
        // v2 file with layers but no commandRecords
        var json = "{\"schemaVersion\":2,\"layers\":[{\"id\":\"layer_0\",\"name\":\"BG\",\"osbLayer\":0,\"sprites\":[{\"id\":\"spr_001\",\"blocks\":[{\"id\":\"blk_001\",\"blockType\":\"Loop\",\"headerCommandId\":\"cmd_hdr\"}]}]}],\"settings\":{},\"variables\":{},\"rawBlocks\":[],\"expandedBlockHistories\":[]}";
        var result = StorybrewCompSerializer.Deserialize(json);

        Assert.NotNull(result.Document.CommandRecords);
        Assert.True(result.IsDirty);
        Assert.Contains(result.Diagnostics, d => d.Contains("commandRecords"));
    }

    [Fact]
    public void Deserialize_MissingExpandedBlockHistories_BackfillsAndMarksDirty()
    {
        // expandedBlockHistories explicitly null (a v2 file written without the field
        // serializes as null). The deserializer must backfill it and mark the doc dirty.
        var json = "{\"schemaVersion\":2,\"layers\":[],\"settings\":{},\"variables\":{},\"rawBlocks\":[],\"commandRecords\":{},\"expandedBlockHistories\":null}";
        var result = StorybrewCompSerializer.Deserialize(json);

        Assert.NotNull(result.Document.ExpandedBlockHistories);
        Assert.True(result.IsDirty);
    }
}
