using VisualCompositor.Core.Model;
using VisualCompositor.Core.Model.Overrides;
using VisualCompositor.Core.Model.ScriptSync;
using VisualCompositor.Core.Primitives;
using VisualCompositor.Core.Serialization;
using VisualCompositor.Core.Validation;
using VisualCompositor.State.Actions;
using VisualCompositor.State.History;

namespace VisualCompositor.State.Reducer;

/// <summary>Pure function (state, action) -> state. Never mutates input state.
/// For document-mutating actions: capture snapshot, apply to clone, validate, push undo + clear redo on success.
/// On failure (target missing or validation error): return original state unchanged (stacks unchanged).</summary>
public static class Reducer
{
    public static EditorState Reduce(EditorState state, EditorAction action)
    {
        return action switch
        {
            SetCurrentTimeAction a => ReduceSetCurrentTime(state, a),
            SetSelectionAction a => ReduceSetSelection(state, a),
            SetViewportAction a => ReduceSetViewport(state, a),
            LoadDocumentAction a => ReduceLoadDocument(state, a),
            EditKeyframeAction a => ReduceEditKeyframe(state, a),
            AddKeyframeAction a => ReduceAddKeyframe(state, a),
            RemoveKeyframeAction a => ReduceRemoveKeyframe(state, a),
            MoveBlockAction a => ReduceMoveBlock(state, a),
            RemoveBlockAction a => ReduceRemoveBlock(state, a),
            ExpandBlockAction a => ReduceExpandBlock(state, a),
            AddParameterSegmentAction a => ReduceAddParameterSegment(state, a),
            RemoveParameterSegmentAction a => ReduceRemoveParameterSegment(state, a),
            RegisterScriptAction a => ReduceRegisterScript(state, a),
            RemoveScriptAction a => ReduceRemoveScript(state, a),
            SetProvenanceAction a => ReduceSetProvenance(state, a),
            ClearProvenanceAction a => ReduceClearProvenance(state, a),
            MarkEntityModifiedAction a => ReduceMarkEntityModified(state, a),
            SetVisualOverrideAction a => ReduceSetVisualOverride(state, a),
            RemoveVisualOverrideAction a => ReduceRemoveVisualOverride(state, a),
            SetDiffVisibilityAction a => ReduceSetDiffVisibility(state, a),
            RemoveDiffVisibilityAction a => ReduceRemoveDiffVisibility(state, a),
            UndoAction => ReduceUndo(state),
            RedoAction => ReduceRedo(state),
            _ => state,
        };
    }

    // ---- Non-mutating actions (no stack changes) ----

    private static EditorState ReduceSetCurrentTime(EditorState state, SetCurrentTimeAction a)
        => NewState(state, currentTime: a.Time);

    private static EditorState ReduceSetSelection(EditorState state, SetSelectionAction a)
        => NewState(state,
            selectedSpriteIds: new List<string>(a.SpriteIds),
            selectedLayerIds: new List<string>(a.LayerIds));

    private static EditorState ReduceSetViewport(EditorState state, SetViewportAction a)
        => NewState(state, viewportZoom: a.Zoom, viewportPan: a.Pan);

    // ---- LoadDocument: clears both stacks ----

    private static EditorState ReduceLoadDocument(EditorState state, LoadDocumentAction a)
    {
        var doc = a.Document.Clone();
        doc.Revision = state.Revision + 1;
        doc.IsDirty = false;
        return NewState(state,
            document: doc,
            revision: state.Revision + 1,
            isDirty: false,
            undoStack: new List<HistoryEntry>(),
            redoStack: new List<HistoryEntry>());
    }

    // ---- Document-mutating actions ----

    private static EditorState ReduceEditKeyframe(EditorState state, EditKeyframeAction a)
        => ApplyMutation(state, a.Description, doc =>
        {
            var sprite = FindSprite(doc, a.LayerId, a.SpriteId);
            if (sprite == null) return null;
            var track = sprite.PropertyTracks.FirstOrDefault(t => t.PropertyName == a.PropertyName);
            if (track == null) return null;
            if (track.ValueType != "Float") return null;
            if (a.KeyframeIndex < 0 || a.KeyframeIndex >= track.FloatKeyframes.Count) return null;
            var kf = track.FloatKeyframes[a.KeyframeIndex];
            bool timeChanged = kf.Time != a.Time;
            kf.Time = a.Time;
            if (float.TryParse(a.ValueJson, out var val))
                kf.Value = val;
            if (timeChanged)
                track.FloatKeyframes.Sort((x, y) => x.Time.CompareTo(y.Time));
            return doc;
        });

    private static EditorState ReduceAddKeyframe(EditorState state, AddKeyframeAction a)
        => ApplyMutation(state, a.Description, doc =>
        {
            var sprite = FindSprite(doc, a.LayerId, a.SpriteId);
            if (sprite == null) return null;
            var track = sprite.PropertyTracks.FirstOrDefault(t => t.PropertyName == a.PropertyName);
            if (track == null)
            {
                track = new PropertyTrack { PropertyName = a.PropertyName, ValueType = "Float" };
                sprite.PropertyTracks.Add(track);
            }
            if (track.ValueType == "Float" && float.TryParse(a.ValueJson, out var val))
            {
                track.FloatKeyframes.Add(new Keyframe<float> { Time = a.Time, Value = val, Easing = a.Easing });
                track.FloatKeyframes.Sort((x, y) => x.Time.CompareTo(y.Time));
            }
            return doc;
        });

    private static EditorState ReduceRemoveKeyframe(EditorState state, RemoveKeyframeAction a)
        => ApplyMutation(state, a.Description, doc =>
        {
            var sprite = FindSprite(doc, a.LayerId, a.SpriteId);
            if (sprite == null) return null;
            var track = sprite.PropertyTracks.FirstOrDefault(t => t.PropertyName == a.PropertyName);
            if (track == null) return null;
            if (track.ValueType != "Float") return null;
            if (a.KeyframeIndex < 0 || a.KeyframeIndex >= track.FloatKeyframes.Count) return null;
            track.FloatKeyframes.RemoveAt(a.KeyframeIndex);
            return doc;
        });

    private static EditorState ReduceMoveBlock(EditorState state, MoveBlockAction a)
        => ApplyMutation(state, a.Description, doc =>
        {
            var (block, container) = FindBlockContainer(doc, a.LayerId, a.BlockId);
            if (block == null || container == null) return null;
            container.Remove(block);
            var idx = Math.Clamp(a.NewIndex, 0, container.Count);
            container.Insert(idx, block);
            return doc;
        });

    private static EditorState ReduceRemoveBlock(EditorState state, RemoveBlockAction a)
        => ApplyMutation(state, a.Description, doc =>
        {
            var (block, container) = FindBlockContainer(doc, a.LayerId, a.BlockId);
            if (block == null || container == null) return null;
            container.Remove(block);
            // Mark the block's command records as Deleted (persisted, not removed from dictionary)
            if (doc.CommandRecords.TryGetValue(block.HeaderCommandId, out var headerRec))
                headerRec.Lifecycle = CommandRecordLifecycle.Deleted;
            var relativeIds = block switch
            {
                LoopBlock lb => lb.RelativeCommands.Select(c => c.Id),
                TriggerBlock tb => tb.RelativeCommands.Select(c => c.Id),
                _ => Enumerable.Empty<string>(),
            };
            foreach (var relId in relativeIds)
            {
                if (doc.CommandRecords.TryGetValue(relId, out var relRec))
                    relRec.Lifecycle = CommandRecordLifecycle.Deleted;
            }
            return doc;
        });

    private static EditorState ReduceExpandBlock(EditorState state, ExpandBlockAction a)
    {
        var (tx, _) = ExpandBlockOperations.CreateTransaction(state.Document, a.LayerId, a.BlockId);
        if (tx == null) return state; // creation failed
        var newState = ExpandBlockOperations.ApplyExpand(state, tx);
        if (newState == null) return state; // apply failed
        return newState;
    }

    private static EditorState ReduceAddParameterSegment(EditorState state, AddParameterSegmentAction a)
        => ApplyMutation(state, a.Description, doc =>
        {
            var sprite = FindSprite(doc, a.LayerId, a.SpriteId);
            if (sprite == null) return null;
            sprite.ParameterTrack ??= new ParameterTrack();
            sprite.ParameterTrack.Segments.Add(new ParameterSegment
            {
                Parameter = a.Parameter,
                StartTime = a.StartTime,
                EndTime = a.EndTime,
                OpenEndedMode = a.OpenEndedMode,
            });
            return doc;
        });

    private static EditorState ReduceRemoveParameterSegment(EditorState state, RemoveParameterSegmentAction a)
        => ApplyMutation(state, a.Description, doc =>
        {
            var sprite = FindSprite(doc, a.LayerId, a.SpriteId);
            if (sprite == null) return null;
            var track = sprite.ParameterTrack;
            if (track == null) return null;
            if (a.SegmentIndex < 0 || a.SegmentIndex >= track.Segments.Count) return null;
            track.Segments.RemoveAt(a.SegmentIndex);
            return doc;
        });

    // ---- Script sync actions ----

    private static EditorState ReduceRegisterScript(EditorState state, RegisterScriptAction a)
        => ApplyMutation(state, a.Description, doc =>
        {
            doc.ScriptSyncManifest ??= new ScriptSyncManifest();
            doc.ScriptSyncManifest.AddOrUpdateSource(a.Source);
            return doc;
        });

    private static EditorState ReduceRemoveScript(EditorState state, RemoveScriptAction a)
        => ApplyMutation(state, a.Description, doc =>
        {
            if (doc.ScriptSyncManifest == null) return null;
            doc.ScriptSyncManifest.RemoveSource(a.ScriptId);
            if (doc.ScriptSyncManifest.IsEmpty)
                doc.ScriptSyncManifest = null;
            return doc;
        });

    private static EditorState ReduceSetProvenance(EditorState state, SetProvenanceAction a)
        => ApplyMutation(state, a.Description, doc =>
        {
            doc.ScriptSyncManifest ??= new ScriptSyncManifest();
            doc.ScriptSyncManifest.SetProvenance(a.Provenance);
            return doc;
        });

    private static EditorState ReduceClearProvenance(EditorState state, ClearProvenanceAction a)
        => ApplyMutation(state, a.Description, doc =>
        {
            if (doc.ScriptSyncManifest == null) return null;
            doc.ScriptSyncManifest.ClearProvenance(a.EntityId);
            if (doc.ScriptSyncManifest.IsEmpty)
                doc.ScriptSyncManifest = null;
            return doc;
        });

    private static EditorState ReduceMarkEntityModified(EditorState state, MarkEntityModifiedAction a)
        => ApplyMutation(state, a.Description, doc =>
        {
            if (doc.ScriptSyncManifest == null) return null;
            var prov = doc.ScriptSyncManifest.GetProvenanceForEntity(a.EntityId);
            if (prov == null) return null;
            prov.SyncState = ScriptSyncState.Modified;
            return doc;
        });

    // ---- Visual override actions ----

    private static EditorState ReduceSetVisualOverride(EditorState state, SetVisualOverrideAction a)
        => ApplyMutation(state, a.Description, doc =>
        {
            doc.VisualOverrides ??= new VisualOverrideCollection();
            doc.VisualOverrides.SetOverride(a.Override);
            return doc;
        });

    private static EditorState ReduceRemoveVisualOverride(EditorState state, RemoveVisualOverrideAction a)
        => ApplyMutation(state, a.Description, doc =>
        {
            if (doc.VisualOverrides == null) return null;
            doc.VisualOverrides.RemoveOverride(a.OverrideId);
            if (doc.VisualOverrides.IsEmpty)
                doc.VisualOverrides = null;
            return doc;
        });

    private static EditorState ReduceSetDiffVisibility(EditorState state, SetDiffVisibilityAction a)
        => ApplyMutation(state, a.Description, doc =>
        {
            doc.VisualOverrides ??= new VisualOverrideCollection();
            doc.VisualOverrides.SetDiffVisibility(a.Visibility);
            return doc;
        });

    private static EditorState ReduceRemoveDiffVisibility(EditorState state, RemoveDiffVisibilityAction a)
        => ApplyMutation(state, a.Description, doc =>
        {
            if (doc.VisualOverrides == null) return null;
            doc.VisualOverrides.RemoveDiffVisibility(a.LayerId);
            if (doc.VisualOverrides.IsEmpty)
                doc.VisualOverrides = null;
            return doc;
        });

    // ---- Undo / Redo ----

    private static EditorState ReduceUndo(EditorState state)
    {
        if (state.UndoStack.Count == 0) return state;

        var entry = state.UndoStack[^1];
        var result = StorybrewCompSerializer.Deserialize(entry.DocumentJson);
        var restoredDoc = result.Document;
        restoredDoc.Revision = state.Revision + 1;
        restoredDoc.IsDirty = true;

        // Push current to redo stack
        var currentJson = StorybrewCompSerializer.Serialize(state.Document);
        var redoEntry = new HistoryEntry
        {
            ActionDescription = entry.ActionDescription,
            DocumentJson = currentJson,
            Revision = state.Revision,
            ExpandTransaction = entry.ExpandTransaction,
        };

        return NewState(state,
            document: restoredDoc,
            revision: state.Revision + 1,
            isDirty: true,
            undoStack: state.UndoStack[..^1],
            redoStack: new List<HistoryEntry>(state.RedoStack) { redoEntry });
    }

    private static EditorState ReduceRedo(EditorState state)
    {
        if (state.RedoStack.Count == 0) return state;

        var entry = state.RedoStack[^1];

        CompositionDocument? newDoc;
        if (entry.ExpandTransaction != null)
        {
            // Replay, don't recompute
            newDoc = ExpandBlockOperations.RedoExpand(state.Document, entry.ExpandTransaction);
            if (newDoc == null) return state; // failed redo: stacks unchanged
        }
        else
        {
            var result = StorybrewCompSerializer.Deserialize(entry.DocumentJson);
            newDoc = result.Document;
        }

        newDoc.Revision = state.Revision + 1;
        newDoc.IsDirty = true;

        // Push current to undo stack
        var currentJson = StorybrewCompSerializer.Serialize(state.Document);
        var undoEntry = new HistoryEntry
        {
            ActionDescription = entry.ActionDescription,
            DocumentJson = currentJson,
            Revision = state.Revision,
            ExpandTransaction = entry.ExpandTransaction,
        };

        return NewState(state,
            document: newDoc,
            revision: state.Revision + 1,
            isDirty: true,
            undoStack: new List<HistoryEntry>(state.UndoStack) { undoEntry },
            redoStack: state.RedoStack[..^1]);
    }

    // ---- Shared mutation helper ----

    /// <summary>Capture snapshot, apply mutation to clone, validate, push undo + clear redo on success.
    /// On failure (mutate returns null or validation errors): return original state unchanged.</summary>
    private static EditorState ApplyMutation(
        EditorState state,
        string description,
        Func<CompositionDocument, CompositionDocument?> mutate)
    {
        var preEditJson = StorybrewCompSerializer.Serialize(state.Document);

        var newDoc = mutate(state.Document.Clone());
        if (newDoc == null) return state; // can't apply

        var diagnostics = ValidationDispatcher.Validate(newDoc, ValidationEntryPoint.Create);
        if (diagnostics.HasErrors) return state; // validation failed

        newDoc.Revision = state.Document.Revision + 1;
        newDoc.IsDirty = true;

        var undoEntry = new HistoryEntry
        {
            ActionDescription = description,
            DocumentJson = preEditJson,
            Revision = state.Revision,
        };

        return NewState(state,
            document: newDoc,
            revision: state.Revision + 1,
            isDirty: true,
            undoStack: new List<HistoryEntry>(state.UndoStack) { undoEntry },
            redoStack: new List<HistoryEntry>());
    }

    // ---- Lookup helpers ----

    private static SpriteDeclaration? FindSprite(CompositionDocument doc, string layerId, string spriteId)
    {
        var layer = doc.Layers.FirstOrDefault(l => l.Id == layerId);
        return layer?.Sprites.FirstOrDefault(s => s.Id == spriteId);
    }

    private static (StoryboardBlock? Block, List<StoryboardBlock>? Container) FindBlockContainer(
        CompositionDocument doc, string layerId, string blockId)
    {
        var layer = doc.Layers.FirstOrDefault(l => l.Id == layerId);
        if (layer == null) return (null, null);

        var block = layer.Blocks.FirstOrDefault(b => b.Id == blockId);
        if (block != null) return (block, layer.Blocks);

        foreach (var sprite in layer.Sprites)
        {
            block = sprite.Blocks.FirstOrDefault(b => b.Id == blockId);
            if (block != null) return (block, sprite.Blocks);
        }
        return (null, null);
    }

    // ---- State copy helper ----

    private static EditorState NewState(
        EditorState src,
        CompositionDocument? document = null,
        long? revision = null,
        List<string>? selectedSpriteIds = null,
        List<string>? selectedLayerIds = null,
        double? currentTime = null,
        double? viewportZoom = null,
        Vector2? viewportPan = null,
        bool? isDirty = null,
        List<HistoryEntry>? undoStack = null,
        List<HistoryEntry>? redoStack = null) => new()
    {
        Document = document ?? src.Document,
        Revision = revision ?? src.Revision,
        SelectedSpriteIds = selectedSpriteIds ?? src.SelectedSpriteIds,
        SelectedLayerIds = selectedLayerIds ?? src.SelectedLayerIds,
        CurrentTime = currentTime ?? src.CurrentTime,
        ViewportZoom = viewportZoom ?? src.ViewportZoom,
        ViewportPan = viewportPan ?? src.ViewportPan,
        IsDirty = isDirty ?? src.IsDirty,
        UndoStack = undoStack ?? src.UndoStack,
        RedoStack = redoStack ?? src.RedoStack,
    };
}
