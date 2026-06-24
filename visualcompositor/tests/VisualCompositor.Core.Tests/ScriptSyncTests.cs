using VisualCompositor.Core.Model.ScriptSync;

namespace VisualCompositor.Core.Tests;

public class ScriptSyncTests
{
    private static ScriptSource MakeSource(string id = "script_1") => new()
    {
        ScriptId = id,
        Identifier = "MyScript",
        ScriptPath = "scripts/myscript.cs",
        ContentHash = "abc123",
        LastSyncTime = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero),
        Parameters = new Dictionary<string, string> { ["foo"] = "bar", ["count"] = "42" },
    };

    private static ScriptProvenance MakeProvenance(string entityId = "spr_1", string scriptId = "script_1") => new()
    {
        ScriptId = scriptId,
        EntityId = entityId,
        EntityType = "Sprite",
        SyncState = ScriptSyncState.Synced,
    };

    [Fact]
    public void ScriptSource_Clone_RoundTripsAllFields()
    {
        var source = MakeSource();

        var clone = source.Clone();

        Assert.NotSame(source, clone);
        Assert.Equal(source.ScriptId, clone.ScriptId);
        Assert.Equal(source.Identifier, clone.Identifier);
        Assert.Equal(source.ScriptPath, clone.ScriptPath);
        Assert.Equal(source.ContentHash, clone.ContentHash);
        Assert.Equal(source.LastSyncTime, clone.LastSyncTime);
        Assert.NotSame(source.Parameters, clone.Parameters);
        Assert.Equal(source.Parameters["foo"], clone.Parameters["foo"]);
        Assert.Equal(source.Parameters["count"], clone.Parameters["count"]);
    }

    [Fact]
    public void ScriptSource_Clone_ParametersAreIndependent()
    {
        var source = MakeSource();
        var clone = source.Clone();
        clone.Parameters["foo"] = "changed";

        Assert.Equal("bar", source.Parameters["foo"]);
    }

    [Fact]
    public void Manifest_AddOrUpdateSource_AddsNew()
    {
        var manifest = new ScriptSyncManifest();
        var source = MakeSource();

        manifest.AddOrUpdateSource(source);

        Assert.Single(manifest.Sources);
        Assert.Equal("script_1", manifest.Sources[0].ScriptId);
        Assert.NotSame(source, manifest.Sources[0]);
    }

    [Fact]
    public void Manifest_AddOrUpdateSource_UpdatesExisting()
    {
        var manifest = new ScriptSyncManifest();
        manifest.AddOrUpdateSource(MakeSource());
        var updated = new ScriptSource
        {
            ScriptId = "script_1",
            Identifier = "RenamedScript",
            ScriptPath = "scripts/renamed.cs",
            ContentHash = "newhash",
            LastSyncTime = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
            Parameters = new Dictionary<string, string> { ["x"] = "y" },
        };

        manifest.AddOrUpdateSource(updated);

        Assert.Single(manifest.Sources);
        var src = manifest.Sources[0];
        Assert.Equal("RenamedScript", src.Identifier);
        Assert.Equal("scripts/renamed.cs", src.ScriptPath);
        Assert.Equal("newhash", src.ContentHash);
        Assert.Equal("y", src.Parameters["x"]);
        Assert.False(src.Parameters.ContainsKey("foo"));
    }

    [Fact]
    public void Manifest_RemoveSource_RemovesSourceAndProvenance()
    {
        var manifest = new ScriptSyncManifest();
        manifest.AddOrUpdateSource(MakeSource("script_1"));
        manifest.AddOrUpdateSource(MakeSource("script_2"));
        manifest.SetProvenance(MakeProvenance("spr_1", "script_1"));
        manifest.SetProvenance(MakeProvenance("spr_2", "script_2"));

        manifest.RemoveSource("script_1");

        Assert.Single(manifest.Sources);
        Assert.Equal("script_2", manifest.Sources[0].ScriptId);
        Assert.Single(manifest.Provenance);
        Assert.Equal("script_2", manifest.Provenance[0].ScriptId);
    }

    [Fact]
    public void Manifest_GetProvenanceForEntity_ReturnsEntry()
    {
        var manifest = new ScriptSyncManifest();
        manifest.SetProvenance(MakeProvenance("spr_1", "script_1"));
        manifest.SetProvenance(MakeProvenance("spr_2", "script_1"));

        var prov = manifest.GetProvenanceForEntity("spr_2");

        Assert.NotNull(prov);
        Assert.Equal("spr_2", prov!.EntityId);
        Assert.Equal("script_1", prov.ScriptId);
    }

    [Fact]
    public void Manifest_GetProvenanceForEntity_ReturnsNullWhenMissing()
    {
        var manifest = new ScriptSyncManifest();

        var prov = manifest.GetProvenanceForEntity("nope");

        Assert.Null(prov);
    }

    [Fact]
    public void Manifest_GetEntitiesForScript_ReturnsAllForScript()
    {
        var manifest = new ScriptSyncManifest();
        manifest.SetProvenance(MakeProvenance("spr_1", "script_1"));
        manifest.SetProvenance(MakeProvenance("spr_2", "script_1"));
        manifest.SetProvenance(MakeProvenance("spr_3", "script_2"));

        var entities = manifest.GetEntitiesForScript("script_1");

        Assert.Equal(2, entities.Count);
        Assert.Contains(entities, p => p.EntityId == "spr_1");
        Assert.Contains(entities, p => p.EntityId == "spr_2");
    }

    [Fact]
    public void Manifest_SetProvenance_ReplacesExisting()
    {
        var manifest = new ScriptSyncManifest();
        manifest.SetProvenance(MakeProvenance("spr_1", "script_1"));

        manifest.SetProvenance(new ScriptProvenance
        {
            ScriptId = "script_2",
            EntityId = "spr_1",
            EntityType = "Block",
            SyncState = ScriptSyncState.Modified,
        });

        Assert.Single(manifest.Provenance);
        var prov = manifest.Provenance[0];
        Assert.Equal("script_2", prov.ScriptId);
        Assert.Equal("Block", prov.EntityType);
        Assert.Equal(ScriptSyncState.Modified, prov.SyncState);
    }

    [Fact]
    public void Manifest_ClearProvenance_RemovesEntry()
    {
        var manifest = new ScriptSyncManifest();
        manifest.SetProvenance(MakeProvenance("spr_1", "script_1"));
        manifest.SetProvenance(MakeProvenance("spr_2", "script_1"));

        manifest.ClearProvenance("spr_1");

        Assert.Single(manifest.Provenance);
        Assert.Equal("spr_2", manifest.Provenance[0].EntityId);
    }

    [Fact]
    public void Manifest_Clone_RoundTripsAllFields()
    {
        var manifest = new ScriptSyncManifest();
        manifest.AddOrUpdateSource(MakeSource("script_1"));
        manifest.SetProvenance(MakeProvenance("spr_1", "script_1"));

        var clone = manifest.Clone();

        Assert.NotSame(manifest, clone);
        Assert.NotSame(manifest.Sources, clone.Sources);
        Assert.NotSame(manifest.Provenance, clone.Provenance);
        Assert.Single(clone.Sources);
        Assert.Equal("script_1", clone.Sources[0].ScriptId);
        Assert.Single(clone.Provenance);
        Assert.Equal("spr_1", clone.Provenance[0].EntityId);
    }

    [Fact]
    public void Manifest_Clone_IsDeepCopy()
    {
        var manifest = new ScriptSyncManifest();
        manifest.AddOrUpdateSource(MakeSource("script_1"));
        var clone = manifest.Clone();

        clone.Sources[0].Identifier = "Changed";
        clone.Sources.Clear();

        Assert.Equal("MyScript", manifest.Sources[0].Identifier);
        Assert.Single(manifest.Sources);
    }

    [Fact]
    public void Manifest_IsEmpty_TrueWhenNothingAdded()
    {
        var manifest = new ScriptSyncManifest();

        Assert.True(manifest.IsEmpty);
    }

    [Fact]
    public void Manifest_IsEmpty_FalseWhenHasSource()
    {
        var manifest = new ScriptSyncManifest();
        manifest.AddOrUpdateSource(MakeSource());

        Assert.False(manifest.IsEmpty);
    }
}
