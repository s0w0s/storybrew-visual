using VisualCompositor.Core.Model;
using VisualCompositor.Core.Model.Overrides;
using VisualCompositor.Core.Model.ScriptSync;
using VisualCompositor.Core.Primitives;
using VisualCompositor.State;
using VisualCompositor.State.Actions;
using VisualCompositor.State.Reducer;

namespace VisualCompositor.Core.Tests;

public class ScriptSyncReducerTests
{
    private static EditorState BuildState() => new() { Document = new CompositionDocument() };

    private static ScriptSource MakeSource(string id = "script_1") => new()
    {
        ScriptId = id,
        Identifier = "MyScript",
        ScriptPath = "scripts/myscript.cs",
        ContentHash = "abc123",
        LastSyncTime = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero),
        Parameters = new Dictionary<string, string> { ["foo"] = "bar" },
    };

    [Fact]
    public void RegisterScript_CreatesManifestIfNull_AndAddsSource()
    {
        var state = BuildState();
        Assert.Null(state.Document.ScriptSyncManifest);

        var result = Reducer.Reduce(state, new RegisterScriptAction(MakeSource()));

        Assert.NotNull(result.Document.ScriptSyncManifest);
        Assert.Single(result.Document.ScriptSyncManifest!.Sources);
        Assert.Equal("script_1", result.Document.ScriptSyncManifest.Sources[0].ScriptId);
        Assert.Equal(state.Revision + 1, result.Revision);
        Assert.Single(result.UndoStack);
    }

    [Fact]
    public void RegisterScript_UpdatesExistingSource()
    {
        var state = BuildState();
        var afterRegister = Reducer.Reduce(state, new RegisterScriptAction(MakeSource()));
        var updated = new ScriptSource
        {
            ScriptId = "script_1",
            Identifier = "Renamed",
            ScriptPath = "scripts/renamed.cs",
            ContentHash = "newhash",
            LastSyncTime = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
            Parameters = new Dictionary<string, string>(),
        };

        var result = Reducer.Reduce(afterRegister, new RegisterScriptAction(updated));

        Assert.NotNull(result.Document.ScriptSyncManifest);
        Assert.Single(result.Document.ScriptSyncManifest!.Sources);
        Assert.Equal("Renamed", result.Document.ScriptSyncManifest.Sources[0].Identifier);
        Assert.Equal("newhash", result.Document.ScriptSyncManifest.Sources[0].ContentHash);
    }

    [Fact]
    public void RemoveScript_RemovesSourceAndProvenance_AndNullsManifestIfEmpty()
    {
        var state = BuildState();
        var afterRegister = Reducer.Reduce(state, new RegisterScriptAction(MakeSource("script_1")));
        var afterProvenance = Reducer.Reduce(afterRegister, new SetProvenanceAction(new ScriptProvenance
        {
            ScriptId = "script_1",
            EntityId = "spr_1",
            EntityType = "Sprite",
            SyncState = ScriptSyncState.Synced,
        }));

        var result = Reducer.Reduce(afterProvenance, new RemoveScriptAction("script_1"));

        Assert.Null(result.Document.ScriptSyncManifest);
        Assert.Equal(afterProvenance.Revision + 1, result.Revision);
    }

    [Fact]
    public void RemoveScript_KeepsManifestWhenOtherSourcesRemain()
    {
        var state = BuildState();
        var s1 = Reducer.Reduce(state, new RegisterScriptAction(MakeSource("script_1")));
        var s2 = Reducer.Reduce(s1, new RegisterScriptAction(MakeSource("script_2")));

        var result = Reducer.Reduce(s2, new RemoveScriptAction("script_1"));

        Assert.NotNull(result.Document.ScriptSyncManifest);
        Assert.Single(result.Document.ScriptSyncManifest!.Sources);
        Assert.Equal("script_2", result.Document.ScriptSyncManifest.Sources[0].ScriptId);
    }

    [Fact]
    public void SetProvenance_CreatesManifestIfNull_AndSetsProvenance()
    {
        var state = BuildState();
        Assert.Null(state.Document.ScriptSyncManifest);

        var result = Reducer.Reduce(state, new SetProvenanceAction(new ScriptProvenance
        {
            ScriptId = "script_1",
            EntityId = "spr_1",
            EntityType = "Sprite",
            SyncState = ScriptSyncState.Synced,
        }));

        Assert.NotNull(result.Document.ScriptSyncManifest);
        Assert.Single(result.Document.ScriptSyncManifest!.Provenance);
        Assert.Equal("spr_1", result.Document.ScriptSyncManifest.Provenance[0].EntityId);
        Assert.Equal(state.Revision + 1, result.Revision);
    }

    [Fact]
    public void MarkEntityModified_ChangesSyncStateToModified()
    {
        var state = BuildState();
        var afterProvenance = Reducer.Reduce(state, new SetProvenanceAction(new ScriptProvenance
        {
            ScriptId = "script_1",
            EntityId = "spr_1",
            EntityType = "Sprite",
            SyncState = ScriptSyncState.Synced,
        }));

        var result = Reducer.Reduce(afterProvenance, new MarkEntityModifiedAction("spr_1"));

        var prov = result.Document.ScriptSyncManifest!.GetProvenanceForEntity("spr_1");
        Assert.NotNull(prov);
        Assert.Equal(ScriptSyncState.Modified, prov!.SyncState);
        Assert.Equal(afterProvenance.Revision + 1, result.Revision);
    }

    [Fact]
    public void MarkEntityModified_NoProvenance_ReturnsStateUnchanged()
    {
        var state = BuildState();

        var result = Reducer.Reduce(state, new MarkEntityModifiedAction("spr_1"));

        Assert.Same(state, result);
        Assert.Equal(state.Revision, result.Revision);
    }

    [Fact]
    public void ClearProvenance_RemovesEntry_AndNullsManifestIfEmpty()
    {
        var state = BuildState();
        var afterProvenance = Reducer.Reduce(state, new SetProvenanceAction(new ScriptProvenance
        {
            ScriptId = "script_1",
            EntityId = "spr_1",
            EntityType = "Sprite",
            SyncState = ScriptSyncState.Synced,
        }));

        var result = Reducer.Reduce(afterProvenance, new ClearProvenanceAction("spr_1"));

        Assert.Null(result.Document.ScriptSyncManifest);
        Assert.Equal(afterProvenance.Revision + 1, result.Revision);
    }

    [Fact]
    public void SetVisualOverride_CreatesCollectionIfNull_AndSetsOverride()
    {
        var state = BuildState();
        Assert.Null(state.Document.VisualOverrides);

        var result = Reducer.Reduce(state, new SetVisualOverrideAction(new VisualOverride
        {
            Id = "ov_1",
            TargetType = OverrideTargetType.Sprite,
            TargetId = "spr_1",
            Visible = false,
            OpacityMultiplier = 0.5f,
        }));

        Assert.NotNull(result.Document.VisualOverrides);
        Assert.Single(result.Document.VisualOverrides!.Overrides);
        Assert.Equal("ov_1", result.Document.VisualOverrides.Overrides[0].Id);
        Assert.Equal(state.Revision + 1, result.Revision);
        Assert.Single(result.UndoStack);
    }

    [Fact]
    public void RemoveVisualOverride_RemovesAndNullsCollectionIfEmpty()
    {
        var state = BuildState();
        var afterSet = Reducer.Reduce(state, new SetVisualOverrideAction(new VisualOverride
        {
            Id = "ov_1",
            TargetType = OverrideTargetType.Sprite,
            TargetId = "spr_1",
        }));

        var result = Reducer.Reduce(afterSet, new RemoveVisualOverrideAction("ov_1"));

        Assert.Null(result.Document.VisualOverrides);
        Assert.Equal(afterSet.Revision + 1, result.Revision);
    }

    [Fact]
    public void SetDiffVisibility_CreatesCollectionIfNull_AndSetsVisibility()
    {
        var state = BuildState();
        Assert.Null(state.Document.VisualOverrides);

        var result = Reducer.Reduce(state, new SetDiffVisibilityAction(new DiffVisibility
        {
            LayerId = "layer_0",
            VisibleDiffs = new HashSet<string> { "Easy" },
        }));

        Assert.NotNull(result.Document.VisualOverrides);
        Assert.Single(result.Document.VisualOverrides!.DiffVisibilities);
        Assert.Equal("layer_0", result.Document.VisualOverrides.DiffVisibilities[0].LayerId);
        Assert.Equal(state.Revision + 1, result.Revision);
    }

    [Fact]
    public void RemoveDiffVisibility_RemovesAndNullsCollectionIfEmpty()
    {
        var state = BuildState();
        var afterSet = Reducer.Reduce(state, new SetDiffVisibilityAction(new DiffVisibility
        {
            LayerId = "layer_0",
            VisibleDiffs = new HashSet<string> { "Easy" },
        }));

        var result = Reducer.Reduce(afterSet, new RemoveDiffVisibilityAction("layer_0"));

        Assert.Null(result.Document.VisualOverrides);
        Assert.Equal(afterSet.Revision + 1, result.Revision);
    }

    [Fact]
    public void AllScriptSyncActions_PushToUndoStack()
    {
        var state = BuildState();

        var result = Reducer.Reduce(state, new RegisterScriptAction(MakeSource()));

        Assert.Single(result.UndoStack);
        Assert.Equal("Register Script", result.UndoStack[0].ActionDescription);
        Assert.Empty(result.RedoStack);
    }

    [Fact]
    public void Undo_RestoresPreviousDocument_WithScriptSync()
    {
        var state = BuildState();
        var afterRegister = Reducer.Reduce(state, new RegisterScriptAction(MakeSource()));

        var afterUndo = Reducer.Reduce(afterRegister, new UndoAction());

        Assert.Null(afterUndo.Document.ScriptSyncManifest);
        Assert.Empty(afterUndo.UndoStack);
        Assert.Single(afterUndo.RedoStack);
    }

    [Fact]
    public void Serialization_RoundTripsScriptSyncManifest()
    {
        var doc = new CompositionDocument();
        doc.ScriptSyncManifest = new ScriptSyncManifest();
        doc.ScriptSyncManifest.AddOrUpdateSource(MakeSource());
        doc.ScriptSyncManifest.SetProvenance(new ScriptProvenance
        {
            ScriptId = "script_1",
            EntityId = "spr_1",
            EntityType = "Sprite",
            SyncState = ScriptSyncState.Synced,
        });

        var json = Serialization.StorybrewCompSerializer.Serialize(doc);
        var result = Serialization.StorybrewCompSerializer.Deserialize(json);

        Assert.NotNull(result.Document.ScriptSyncManifest);
        Assert.Single(result.Document.ScriptSyncManifest!.Sources);
        Assert.Equal("script_1", result.Document.ScriptSyncManifest.Sources[0].ScriptId);
        Assert.Single(result.Document.ScriptSyncManifest.Provenance);
        Assert.Equal("spr_1", result.Document.ScriptSyncManifest.Provenance[0].EntityId);
    }

    [Fact]
    public void Serialization_RoundTripsVisualOverrides()
    {
        var doc = new CompositionDocument();
        doc.VisualOverrides = new VisualOverrideCollection();
        doc.VisualOverrides.SetOverride(new VisualOverride
        {
            Id = "ov_1",
            TargetType = OverrideTargetType.Sprite,
            TargetId = "spr_1",
            Visible = false,
            OpacityMultiplier = 0.5f,
            PositionOffset = new Vector2(10, 20),
            Tint = new Color3(0.1f, 0.2f, 0.3f),
        });
        doc.VisualOverrides.SetDiffVisibility(new DiffVisibility
        {
            LayerId = "layer_0",
            VisibleDiffs = new HashSet<string> { "Easy" },
            HiddenDiffs = new HashSet<string> { "Hard" },
        });

        var json = Serialization.StorybrewCompSerializer.Serialize(doc);
        var result = Serialization.StorybrewCompSerializer.Deserialize(json);

        Assert.NotNull(result.Document.VisualOverrides);
        Assert.Single(result.Document.VisualOverrides!.Overrides);
        var over = result.Document.VisualOverrides.Overrides[0];
        Assert.Equal("ov_1", over.Id);
        Assert.False(over.Visible);
        Assert.Equal(0.5f, over.OpacityMultiplier);
        Assert.Equal(new Vector2(10, 20), over.PositionOffset);
        Assert.Single(result.Document.VisualOverrides.DiffVisibilities);
        var vis = result.Document.VisualOverrides.DiffVisibilities[0];
        Assert.True(vis.IsVisibleOnDiff("Easy"));
        Assert.False(vis.IsVisibleOnDiff("Hard"));
    }

    [Fact]
    public void Serialization_OldFileWithoutNewProperties_LoadsWithNulls()
    {
        var json = "{\"schemaVersion\":2,\"settings\":{},\"variables\":{},\"layers\":[],\"markers\":[],\"rawBlocks\":[],\"commandRecords\":{},\"expandedBlockHistories\":[]}";

        var result = Serialization.StorybrewCompSerializer.Deserialize(json);

        Assert.Null(result.Document.ScriptSyncManifest);
        Assert.Null(result.Document.VisualOverrides);
    }
}
