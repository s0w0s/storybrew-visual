using VisualCompositor.Core.Model;
using VisualCompositor.Core.Primitives;
using Xunit;

namespace VisualCompositor.Core.Tests;

public class PersistentIdGeneratorTests
{
    [Fact]
    public void GenerateForCommand_IsDeterministic()
    {
        var id1 = PersistentIdGenerator.GenerateForCommand("layer_0", "spr_001", null, "F", 100, 200, "0", "1", 0);
        var id2 = PersistentIdGenerator.GenerateForCommand("layer_0", "spr_001", null, "F", 100, 200, "0", "1", 0);
        Assert.Equal(id1, id2);
    }

    [Fact]
    public void GenerateForCommand_DifferentInputs_ProduceDifferentIds()
    {
        var id1 = PersistentIdGenerator.GenerateForCommand("layer_0", "spr_001", null, "F", 100, 200, "0", "1", 0);
        var id2 = PersistentIdGenerator.GenerateForCommand("layer_0", "spr_001", null, "F", 100, 200, "0", "0.5", 0);
        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void GenerateForCommand_HasCmdPrefix()
    {
        var id = PersistentIdGenerator.GenerateForCommand("layer_0", null, null, "M", 0, 100, "0,0", "100,100", 0);
        Assert.StartsWith("cmd_", id);
    }

    [Fact]
    public void GenerateForBlock_HasBlkPrefix()
    {
        var id = PersistentIdGenerator.GenerateForBlock("layer_0", null, "Loop", 1000, 5, null, 0);
        Assert.StartsWith("blk_", id);
    }

    [Fact]
    public void GenerateForSprite_HasSprPrefix()
    {
        var id = PersistentIdGenerator.GenerateForSprite("layer_0", OsbLayer.Background, "bg.png", 320, 240, 0);
        Assert.StartsWith("spr_", id);
    }

    [Fact]
    public void GenerateForRawBlock_HasRawPrefix()
    {
        var id = PersistentIdGenerator.GenerateForRawBlock("layer_0", "// comment", 0);
        Assert.StartsWith("raw_", id);
    }

    [Fact]
    public void GenerateNewCommandId_IsUnique()
    {
        var id1 = PersistentIdGenerator.GenerateNewCommandId();
        var id2 = PersistentIdGenerator.GenerateNewCommandId();
        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void GenerateNewBlockId_IsUnique()
    {
        var id1 = PersistentIdGenerator.GenerateNewBlockId();
        var id2 = PersistentIdGenerator.GenerateNewBlockId();
        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void GenerateTransactionId_IsUnique()
    {
        var id1 = PersistentIdGenerator.GenerateTransactionId();
        var id2 = PersistentIdGenerator.GenerateTransactionId();
        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void GenerateLayerId_IsDeterministic()
    {
        var id1 = PersistentIdGenerator.GenerateLayerId(OsbLayer.Background, 0);
        var id2 = PersistentIdGenerator.GenerateLayerId(OsbLayer.Background, 0);
        Assert.Equal(id1, id2);
        Assert.Contains("Background", id1);
    }

    [Fact]
    public void ValidateIdUniqueness_NoDuplicates_ReturnsTrue()
    {
        var doc = new CompositionDocument();
        doc.Layers.Add(new Layer { Id = "layer_0", Name = "BG", OsbLayer = OsbLayer.Background });
        doc.Layers[0].Sprites.Add(new SpriteDeclaration { Id = "spr_001", LayerId = "layer_0" });

        var result = PersistentIdGenerator.ValidateIdUniqueness(doc, out var duplicates);
        Assert.True(result);
        Assert.Empty(duplicates);
    }

    [Fact]
    public void ValidateIdUniqueness_WithDuplicates_ReturnsFalse()
    {
        var doc = new CompositionDocument();
        doc.Layers.Add(new Layer { Id = "dup_id", Name = "BG", OsbLayer = OsbLayer.Background });
        doc.Layers[0].Sprites.Add(new SpriteDeclaration { Id = "dup_id", LayerId = "dup_id" });

        var result = PersistentIdGenerator.ValidateIdUniqueness(doc, out var duplicates);
        Assert.False(result);
        Assert.Contains("dup_id", duplicates);
    }

    [Fact]
    public void ValidateIdUniqueness_DoesNotRecomputeHash()
    {
        // This test verifies that the validation only checks uniqueness,
        // not hash-content consistency. An id that doesn't match its content hash
        // should still pass uniqueness validation.
        var doc = new CompositionDocument();
        doc.Layers.Add(new Layer { Id = "custom_non_hash_id", Name = "BG", OsbLayer = OsbLayer.Background });

        var result = PersistentIdGenerator.ValidateIdUniqueness(doc, out var duplicates);
        Assert.True(result);
    }
}
