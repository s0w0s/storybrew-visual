using VisualCompositor.Core.Model.Overrides;
using VisualCompositor.Core.Primitives;

namespace VisualCompositor.Core.Tests;

public class VisualOverrideTests
{
    private static VisualOverride MakeOverride(string id = "ov_1") => new()
    {
        Id = id,
        TargetType = OverrideTargetType.Sprite,
        TargetId = "spr_1",
        Visible = false,
        OpacityMultiplier = 0.5f,
        PositionOffset = new Vector2(10, 20),
        ScaleMultiplier = new Vector2(2, 2),
        RotationOffset = 1.5f,
        Tint = new Color3(0.1f, 0.2f, 0.3f),
    };

    [Fact]
    public void VisualOverride_Clone_RoundTripsAllFields()
    {
        var over = MakeOverride();

        var clone = over.Clone();

        Assert.NotSame(over, clone);
        Assert.Equal(over.Id, clone.Id);
        Assert.Equal(over.TargetType, clone.TargetType);
        Assert.Equal(over.TargetId, clone.TargetId);
        Assert.Equal(over.Visible, clone.Visible);
        Assert.Equal(over.OpacityMultiplier, clone.OpacityMultiplier);
        Assert.Equal(over.PositionOffset, clone.PositionOffset);
        Assert.Equal(over.ScaleMultiplier, clone.ScaleMultiplier);
        Assert.Equal(over.RotationOffset, clone.RotationOffset);
        Assert.Equal(over.Tint, clone.Tint);
    }

    [Fact]
    public void VisualOverride_Clone_ClampsOpacityMultiplier()
    {
        var over = new VisualOverride { Id = "ov_1", OpacityMultiplier = 2.5f };

        var clone = over.Clone();

        Assert.Equal(1.0f, clone.OpacityMultiplier);
    }

    [Fact]
    public void VisualOverride_Clone_ClampsNegativeOpacity()
    {
        var over = new VisualOverride { Id = "ov_1", OpacityMultiplier = -0.5f };

        var clone = over.Clone();

        Assert.Equal(0.0f, clone.OpacityMultiplier);
    }

    [Fact]
    public void Collection_SetOverride_AddsNew()
    {
        var collection = new VisualOverrideCollection();
        var over = MakeOverride();

        collection.SetOverride(over);

        Assert.Single(collection.Overrides);
        Assert.NotSame(over, collection.Overrides[0]);
    }

    [Fact]
    public void Collection_SetOverride_ReplacesExistingForSameTarget()
    {
        var collection = new VisualOverrideCollection();
        collection.SetOverride(MakeOverride("ov_1"));
        var updated = new VisualOverride
        {
            Id = "ov_2",
            TargetType = OverrideTargetType.Sprite,
            TargetId = "spr_1",
            Visible = true,
            OpacityMultiplier = 0.25f,
        };

        collection.SetOverride(updated);

        Assert.Single(collection.Overrides);
        var over = collection.Overrides[0];
        Assert.Equal("ov_2", over.Id);
        Assert.True(over.Visible);
        Assert.Equal(0.25f, over.OpacityMultiplier);
    }

    [Fact]
    public void Collection_GetOverride_ReturnsByTarget()
    {
        var collection = new VisualOverrideCollection();
        collection.SetOverride(MakeOverride());

        var over = collection.GetOverride(OverrideTargetType.Sprite, "spr_1");

        Assert.NotNull(over);
        Assert.Equal("ov_1", over!.Id);
    }

    [Fact]
    public void Collection_GetOverride_ReturnsNullWhenMissing()
    {
        var collection = new VisualOverrideCollection();

        var over = collection.GetOverride(OverrideTargetType.Layer, "layer_0");

        Assert.Null(over);
    }

    [Fact]
    public void Collection_RemoveOverride_RemovesById()
    {
        var collection = new VisualOverrideCollection();
        collection.SetOverride(MakeOverride("ov_1"));
        collection.SetOverride(new VisualOverride
        {
            Id = "ov_2",
            TargetType = OverrideTargetType.Layer,
            TargetId = "layer_0",
        });

        collection.RemoveOverride("ov_1");

        Assert.Single(collection.Overrides);
        Assert.Equal("ov_2", collection.Overrides[0].Id);
    }

    [Fact]
    public void Collection_SetDiffVisibility_AddsNew()
    {
        var collection = new VisualOverrideCollection();
        var vis = new DiffVisibility
        {
            LayerId = "layer_0",
            VisibleDiffs = new HashSet<string> { "Easy", "Normal" },
        };

        collection.SetDiffVisibility(vis);

        Assert.Single(collection.DiffVisibilities);
        Assert.NotSame(vis, collection.DiffVisibilities[0]);
    }

    [Fact]
    public void Collection_SetDiffVisibility_ReplacesExisting()
    {
        var collection = new VisualOverrideCollection();
        collection.SetDiffVisibility(new DiffVisibility
        {
            LayerId = "layer_0",
            VisibleDiffs = new HashSet<string> { "Easy" },
        });
        collection.SetDiffVisibility(new DiffVisibility
        {
            LayerId = "layer_0",
            HiddenDiffs = new HashSet<string> { "Hard" },
        });

        Assert.Single(collection.DiffVisibilities);
        var vis = collection.DiffVisibilities[0];
        Assert.Empty(vis.VisibleDiffs);
        Assert.Contains("Hard", vis.HiddenDiffs);
    }

    [Fact]
    public void Collection_GetDiffVisibility_ReturnsByLayer()
    {
        var collection = new VisualOverrideCollection();
        collection.SetDiffVisibility(new DiffVisibility { LayerId = "layer_0" });

        var vis = collection.GetDiffVisibility("layer_0");

        Assert.NotNull(vis);
    }

    [Fact]
    public void Collection_RemoveDiffVisibility_RemovesByLayer()
    {
        var collection = new VisualOverrideCollection();
        collection.SetDiffVisibility(new DiffVisibility { LayerId = "layer_0" });
        collection.SetDiffVisibility(new DiffVisibility { LayerId = "layer_1" });

        collection.RemoveDiffVisibility("layer_0");

        Assert.Single(collection.DiffVisibilities);
        Assert.Equal("layer_1", collection.DiffVisibilities[0].LayerId);
    }

    [Fact]
    public void DiffVisibility_IsVisibleOnDiff_EmptySets_VisibleEverywhere()
    {
        var vis = new DiffVisibility { LayerId = "layer_0" };

        Assert.True(vis.IsVisibleOnDiff("Easy"));
        Assert.True(vis.IsVisibleOnDiff("Hard"));
        Assert.True(vis.IsVisibleOnDiff("Insane"));
    }

    [Fact]
    public void DiffVisibility_IsVisibleOnDiff_InVisibleDiffs_Visible()
    {
        var vis = new DiffVisibility
        {
            LayerId = "layer_0",
            VisibleDiffs = new HashSet<string> { "Easy", "Normal" },
        };

        Assert.True(vis.IsVisibleOnDiff("Easy"));
        Assert.True(vis.IsVisibleOnDiff("Normal"));
        Assert.False(vis.IsVisibleOnDiff("Hard"));
    }

    [Fact]
    public void DiffVisibility_IsVisibleOnDiff_InHiddenDiffs_Hidden()
    {
        var vis = new DiffVisibility
        {
            LayerId = "layer_0",
            HiddenDiffs = new HashSet<string> { "Hard" },
        };

        Assert.True(vis.IsVisibleOnDiff("Easy"));
        Assert.False(vis.IsVisibleOnDiff("Hard"));
    }

    [Fact]
    public void DiffVisibility_IsVisibleOnDiff_BothSets_HiddenWins()
    {
        var vis = new DiffVisibility
        {
            LayerId = "layer_0",
            VisibleDiffs = new HashSet<string> { "Easy" },
            HiddenDiffs = new HashSet<string> { "Easy" },
        };

        Assert.False(vis.IsVisibleOnDiff("Easy"));
    }

    [Fact]
    public void Collection_Clone_RoundTripsAllFields()
    {
        var collection = new VisualOverrideCollection();
        collection.SetOverride(MakeOverride());
        collection.SetDiffVisibility(new DiffVisibility
        {
            LayerId = "layer_0",
            VisibleDiffs = new HashSet<string> { "Easy" },
        });

        var clone = collection.Clone();

        Assert.NotSame(collection, clone);
        Assert.NotSame(collection.Overrides, clone.Overrides);
        Assert.NotSame(collection.DiffVisibilities, clone.DiffVisibilities);
        Assert.Single(clone.Overrides);
        Assert.Single(clone.DiffVisibilities);
        Assert.Equal("ov_1", clone.Overrides[0].Id);
        Assert.Equal("layer_0", clone.DiffVisibilities[0].LayerId);
    }

    [Fact]
    public void Collection_Clone_IsDeepCopy()
    {
        var collection = new VisualOverrideCollection();
        collection.SetOverride(MakeOverride());
        var clone = collection.Clone();

        clone.Overrides[0].Id = "changed";
        clone.Overrides.Clear();

        Assert.Equal("ov_1", collection.Overrides[0].Id);
        Assert.Single(collection.Overrides);
    }

    [Fact]
    public void Collection_IsEmpty_TrueWhenNothingAdded()
    {
        var collection = new VisualOverrideCollection();

        Assert.True(collection.IsEmpty);
    }
}
