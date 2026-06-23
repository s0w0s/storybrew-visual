using VisualCompositor.Core.Model;
using VisualCompositor.Core.Primitives;
using VisualCompositor.Core.Serialization;
using VisualCompositor.Core.Snapshots;
using VisualCompositor.Core.Validation;

namespace VisualCompositor.State.History;

/// <summary>Operations for ExpandBlockTransaction create/apply/undo/redo.
/// Per design §10/§11/§12 + Freeze Patch §1.</summary>
public static class ExpandBlockOperations
{
    /// <summary>Build an ExpandBlockTransaction from a LoopBlock to expand.
    /// Validates with TransactionValidators (A/B/C/D/F/L). Returns null + diagnostics on failure.</summary>
    public static (ExpandBlockTransaction? Transaction, DiagnosticCollection Diagnostics) CreateTransaction(
        CompositionDocument document, string layerId, string blockId)
    {
        var diagnostics = new DiagnosticCollection();

        var layer = document.Layers.FirstOrDefault(l => l.Id == layerId);
        if (layer == null)
        {
            diagnostics.Add(new Diagnostic
            {
                Code = FailureCodes.UNDO_LAYER_MISSING,
                Message = $"Layer '{layerId}' not found",
                Severity = DiagnosticSeverity.HardError,
                Scope = "transaction",
            });
            return (null, diagnostics);
        }

        var (block, container, sprite) = FindBlock(document, layerId, blockId);
        if (block is not LoopBlock loopBlock)
        {
            diagnostics.Add(new Diagnostic
            {
                Code = FailureCodes.ID_REFERENCE_INVALID,
                Message = $"Block '{blockId}' not found or is not a LoopBlock (MVP supports Loop only)",
                Severity = DiagnosticSeverity.HardError,
                Scope = "transaction",
            });
            return (null, diagnostics);
        }

        var transactionId = PersistentIdGenerator.GenerateTransactionId();

        // Original block snapshot
        var originalSnapshot = SnapshotLoopBlock(loopBlock);

        // Compute iteration duration = max end time of relative commands
        double iterationDuration = 0;
        foreach (var relCmd in loopBlock.RelativeCommands)
        {
            if (relCmd.EndTime > iterationDuration)
                iterationDuration = relCmd.EndTime;
        }

        // Generate commands: one per (relative command x iteration)
        var generatedCommands = new List<GeneratedCommandSnapshot>();
        var generatedIdsByRelative = new Dictionary<string, List<string>>();
        for (int i = 0; i < loopBlock.LoopCount; i++)
        {
            double iterOffset = loopBlock.StartTime + i * iterationDuration;
            foreach (var relCmd in loopBlock.RelativeCommands)
            {
                var genId = PersistentIdGenerator.GenerateNewCommandId();
                var gen = new GeneratedCommandSnapshot
                {
                    CommandId = genId,
                    LayerId = layerId,
                    CommandType = relCmd.CommandType,
                    Easing = relCmd.Easing,
                    StartTime = iterOffset + relCmd.StartTime,
                    EndTime = iterOffset + relCmd.EndTime,
                    StartValue = relCmd.StartValue,
                    EndValue = relCmd.EndValue,
                    ComponentMask = ComponentMaskFor(relCmd.CommandType),
                    SourceRelativeCommandId = relCmd.Id,
                };
                generatedCommands.Add(gen);
                if (!generatedIdsByRelative.ContainsKey(relCmd.Id))
                    generatedIdsByRelative[relCmd.Id] = new List<string>();
                generatedIdsByRelative[relCmd.Id].Add(genId);
            }
        }

        var generatedCommandIds = generatedCommands.Select(g => g.CommandId).ToList();

        // AffectedCommandRecordsBefore: snapshot header + relative records as-is
        var beforeRecords = new List<CommandRecordSnapshot>();
        AddRecordSnapshot(document, loopBlock.HeaderCommandId, beforeRecords);
        foreach (var relCmd in loopBlock.RelativeCommands)
            AddRecordSnapshot(document, relCmd.Id, beforeRecords);

        // AffectedCommandRecordsAfter: header (Split, all derived) + relative (Split, respective derived) + generated (Created)
        var afterRecords = new List<CommandRecordSnapshot>();
        // Header after
        if (document.CommandRecords.TryGetValue(loopBlock.HeaderCommandId, out var headerRec))
        {
            afterRecords.Add(new CommandRecordSnapshot
            {
                CommandId = headerRec.CommandId,
                Lifecycle = CommandRecordLifecycle.Split,
                DerivedCommandIds = new List<string>(generatedCommandIds),
                ParentBlockId = headerRec.ParentBlockId,
                ParentLayerId = headerRec.ParentLayerId,
                SourceReference = headerRec.SourceReference?.DeepClone(),
            });
        }
        // Relative after
        foreach (var relCmd in loopBlock.RelativeCommands)
        {
            if (document.CommandRecords.TryGetValue(relCmd.Id, out var relRec))
            {
                var derived = generatedIdsByRelative.TryGetValue(relCmd.Id, out var d) ? d : new List<string>();
                afterRecords.Add(new CommandRecordSnapshot
                {
                    CommandId = relRec.CommandId,
                    Lifecycle = CommandRecordLifecycle.Split,
                    DerivedCommandIds = new List<string>(derived),
                    ParentBlockId = relRec.ParentBlockId,
                    ParentLayerId = relRec.ParentLayerId,
                    SourceReference = relRec.SourceReference?.DeepClone(),
                });
            }
        }
        // Generated after
        foreach (var gen in generatedCommands)
        {
            afterRecords.Add(new CommandRecordSnapshot
            {
                CommandId = gen.CommandId,
                Lifecycle = CommandRecordLifecycle.Created,
                DerivedCommandIds = new List<string>(),
                ParentBlockId = null,
                ParentLayerId = layerId,
            });
        }

        // RawBlockAnchorsBefore/After: raw blocks anchored to header command
        var anchorsBefore = new List<RawBlockAnchorSnapshot>();
        var anchorsAfter = new List<RawBlockAnchorSnapshot>();
        var firstGenId = generatedCommandIds.FirstOrDefault() ?? loopBlock.HeaderCommandId;
        foreach (var raw in document.RawBlocks)
        {
            if (raw.AnchorKind == RawBlockAnchorKind.AfterCommand &&
                raw.AnchorCommandId == loopBlock.HeaderCommandId)
            {
                anchorsBefore.Add(new RawBlockAnchorSnapshot
                {
                    RawBlockId = raw.Id,
                    AnchorKind = raw.AnchorKind,
                    AnchorCommandId = raw.AnchorCommandId,
                    LayerId = raw.LayerId,
                });
                anchorsAfter.Add(new RawBlockAnchorSnapshot
                {
                    RawBlockId = raw.Id,
                    AnchorKind = raw.AnchorKind,
                    AnchorCommandId = firstGenId,
                    LayerId = raw.LayerId,
                });
            }
        }

        // InsertionAnchor: position based on current location
        var index = container.IndexOf(loopBlock);
        var insertionAnchor = new BlockInsertionAnchor
        {
            LayerId = layerId,
            PreviousSiblingBlockId = index > 0 ? container[index - 1].Id : null,
            NextSiblingBlockId = index < container.Count - 1 ? container[index + 1].Id : null,
            OriginalIndexFallback = index,
        };

        var tx = new ExpandBlockTransaction
        {
            TransactionId = transactionId,
            LayerId = layerId,
            OriginalBlockSnapshot = originalSnapshot,
            InsertionAnchor = insertionAnchor,
            CreatedHistory = new ExpandedBlockHistoryRef
            {
                ExpandedBlockId = loopBlock.Id,
                TransactionId = transactionId,
            },
            GeneratedCommandIds = generatedCommandIds,
            GeneratedCommandsAfter = generatedCommands,
            AffectedCommandRecordsBefore = beforeRecords,
            AffectedCommandRecordsAfter = afterRecords,
            RawBlockAnchorsBefore = anchorsBefore,
            RawBlockAnchorsAfter = anchorsAfter,
        };

        // Validate A/B/C/D/F/L
        var txDiag = ValidationDispatcher.ValidateTransaction(tx, ValidationEntryPoint.Create);
        if (txDiag.HasErrors)
            return (null, txDiag);

        return (tx, diagnostics);
    }

    /// <summary>Apply the expansion to the document. Pushes to undo stack with the transaction, clears redo.
    /// Returns null on failure (validation error or block not found).</summary>
    public static EditorState? ApplyExpand(EditorState state, ExpandBlockTransaction tx)
    {
        var preExpandJson = StorybrewCompSerializer.Serialize(state.Document);

        var doc = state.Document.Clone();

        var (block, container, sprite) = FindBlock(doc, tx.LayerId, tx.OriginalBlockSnapshot.BlockId);
        if (block == null) return null;
        container.Remove(block);

        // Update command records: header -> Split, relative -> Split, generated -> Created
        if (doc.CommandRecords.TryGetValue(tx.OriginalBlockSnapshot.HeaderCommandId, out var headerRec))
        {
            headerRec.Lifecycle = CommandRecordLifecycle.Split;
            headerRec.DerivedCommandIds = new List<string>(tx.GeneratedCommandIds);
        }
        foreach (var relSnap in tx.OriginalBlockSnapshot.RelativeCommands)
        {
            if (doc.CommandRecords.TryGetValue(relSnap.Id, out var relRec))
            {
                relRec.Lifecycle = CommandRecordLifecycle.Split;
                relRec.DerivedCommandIds = tx.GeneratedCommandsAfter
                    .Where(g => g.SourceRelativeCommandId == relSnap.Id)
                    .Select(g => g.CommandId).ToList();
            }
        }
        foreach (var gen in tx.GeneratedCommandsAfter)
        {
            doc.CommandRecords[gen.CommandId] = new CommandRecord
            {
                CommandId = gen.CommandId,
                Lifecycle = CommandRecordLifecycle.Created,
                ParentLayerId = tx.LayerId,
            };
        }

        // Add generated keyframes (expand to keyframes)
        AddGeneratedKeyframes(doc, tx, sprite);

        // Re-anchor raw blocks from header command to first generated command
        var firstGenId = tx.GeneratedCommandIds.FirstOrDefault();
        foreach (var raw in doc.RawBlocks)
        {
            if (raw.AnchorKind == RawBlockAnchorKind.AfterCommand &&
                raw.AnchorCommandId == tx.OriginalBlockSnapshot.HeaderCommandId &&
                firstGenId != null)
            {
                raw.AnchorCommandId = firstGenId;
            }
        }

        // Add history entry
        doc.ExpandedBlockHistories.Add(new ExpandedBlockHistory
        {
            ExpandedBlockId = tx.OriginalBlockSnapshot.BlockId,
            Snapshot = tx.OriginalBlockSnapshot.DeepClone(),
            TransactionId = tx.TransactionId,
            Timestamp = DateTimeOffset.UtcNow,
        });

        doc.IsDirty = true;
        doc.Revision = state.Document.Revision + 1;

        // Validate result
        var docDiag = ValidationDispatcher.Validate(doc, ValidationEntryPoint.Create);
        if (docDiag.HasErrors) return null;

        var undoEntry = new HistoryEntry
        {
            ActionDescription = "Expand Block",
            DocumentJson = preExpandJson,
            Revision = state.Revision,
            ExpandTransaction = tx,
        };

        return new EditorState
        {
            Document = doc,
            Revision = state.Revision + 1,
            SelectedSpriteIds = new List<string>(state.SelectedSpriteIds),
            SelectedLayerIds = new List<string>(state.SelectedLayerIds),
            CurrentTime = state.CurrentTime,
            ViewportZoom = state.ViewportZoom,
            ViewportPan = state.ViewportPan,
            IsDirty = true,
            UndoStack = new List<HistoryEntry>(state.UndoStack) { undoEntry },
            RedoStack = new List<HistoryEntry>(),
        };
    }

    /// <summary>Undo an expand per §11 undo flow. Returns null on failure.</summary>
    public static CompositionDocument? UndoExpand(CompositionDocument document, ExpandBlockTransaction tx)
    {
        var work = document.Clone();

        // 2. Validate transaction
        var txDiag = ValidationDispatcher.ValidateTransaction(tx, ValidationEntryPoint.UndoRestore);
        if (txDiag.HasErrors) return null;

        // 3-4. Remove generated command records
        foreach (var genId in tx.GeneratedCommandIds)
            work.CommandRecords.Remove(genId);

        // Remove generated keyframes
        RemoveGeneratedKeyframes(work, tx);

        // 5. Restore AffectedCommandRecordsBefore
        foreach (var beforeRec in tx.AffectedCommandRecordsBefore)
            work.CommandRecords[beforeRec.CommandId] = RestoreRecord(beforeRec);

        // 6. Restore RawBlockAnchorsBefore
        foreach (var anchor in tx.RawBlockAnchorsBefore)
        {
            var raw = work.RawBlocks.FirstOrDefault(r => r.Id == anchor.RawBlockId);
            if (raw != null)
            {
                raw.AnchorKind = anchor.AnchorKind;
                raw.AnchorCommandId = anchor.AnchorCommandId;
                raw.LayerId = anchor.LayerId;
            }
        }

        // 7. Recreate OriginalBlockSnapshot
        var block = RecreateBlock(tx.OriginalBlockSnapshot);
        if (block == null) return null;

        // 8. Insert block by InsertionAnchor (LayerId missing = abort)
        if (!InsertBlock(work, tx.InsertionAnchor, block)) return null;

        // 9. Remove CreatedHistory
        work.ExpandedBlockHistories.RemoveAll(h => h.TransactionId == tx.TransactionId);

        // 10. Post-undo validation
        var docDiag = ValidationDispatcher.Validate(work, ValidationEntryPoint.UndoRestore);
        if (docDiag.HasErrors) return null;

        work.IsDirty = true;
        return work;
    }

    /// <summary>Redo an expand per §11 redo flow (replay, NOT recompute). Returns null on failure.</summary>
    public static CompositionDocument? RedoExpand(CompositionDocument document, ExpandBlockTransaction tx)
    {
        var work = document.Clone();

        // 2. Validate transaction
        var txDiag = ValidationDispatcher.ValidateTransaction(tx, ValidationEntryPoint.RedoRestore);
        if (txDiag.HasErrors) return null;

        // 3. Remove original block from active tree
        var (block, container, sprite) = FindBlock(work, tx.LayerId, tx.OriginalBlockSnapshot.BlockId);
        if (block == null) return null;
        container.Remove(block);

        // 4-5. Restore AffectedCommandRecordsAfter (replay, not recompute — uses existing ids)
        foreach (var afterRec in tx.AffectedCommandRecordsAfter)
            work.CommandRecords[afterRec.CommandId] = RestoreRecord(afterRec);

        // Replay generated keyframes from GeneratedCommandsAfter (NOT recompute)
        AddGeneratedKeyframes(work, tx, sprite);

        // 6. Restore RawBlockAnchorsAfter
        foreach (var anchor in tx.RawBlockAnchorsAfter)
        {
            var raw = work.RawBlocks.FirstOrDefault(r => r.Id == anchor.RawBlockId);
            if (raw != null)
            {
                raw.AnchorKind = anchor.AnchorKind;
                raw.AnchorCommandId = anchor.AnchorCommandId;
                raw.LayerId = anchor.LayerId;
            }
        }

        // 7. Add CreatedHistory
        work.ExpandedBlockHistories.Add(new ExpandedBlockHistory
        {
            ExpandedBlockId = tx.OriginalBlockSnapshot.BlockId,
            Snapshot = tx.OriginalBlockSnapshot.DeepClone(),
            TransactionId = tx.TransactionId,
            Timestamp = DateTimeOffset.UtcNow,
        });

        // 8. Post-redo validation
        var docDiag = ValidationDispatcher.Validate(work, ValidationEntryPoint.RedoRestore);
        if (docDiag.HasErrors) return null;

        work.IsDirty = true;
        return work;
    }

    // ---- Helpers ----

    private static (StoryboardBlock? Block, List<StoryboardBlock> Container, SpriteDeclaration? Sprite) FindBlock(
        CompositionDocument doc, string layerId, string blockId)
    {
        var layer = doc.Layers.FirstOrDefault(l => l.Id == layerId);
        if (layer == null) return (null, new List<StoryboardBlock>(), null);

        for (int i = 0; i < layer.Blocks.Count; i++)
        {
            if (layer.Blocks[i].Id == blockId)
                return (layer.Blocks[i], layer.Blocks, null);
        }
        foreach (var sprite in layer.Sprites)
        {
            for (int i = 0; i < sprite.Blocks.Count; i++)
            {
                if (sprite.Blocks[i].Id == blockId)
                    return (sprite.Blocks[i], sprite.Blocks, sprite);
            }
        }
        return (null, new List<StoryboardBlock>(), null);
    }

    private static StoryboardBlockSnapshot SnapshotLoopBlock(LoopBlock lb) => new()
    {
        BlockId = lb.Id,
        LayerId = lb.LayerId,
        HeaderCommandId = lb.HeaderCommandId,
        EditAccess = lb.EditAccess,
        MaterializationState = lb.MaterializationState,
        BlockType = "Loop",
        StartTime = lb.StartTime,
        EndTimeOrCount = lb.LoopCount,
        RelativeCommands = lb.RelativeCommands.ConvertAll(r => new RelativeCommandSnapshot
        {
            Id = r.Id,
            CommandType = r.CommandType,
            Easing = r.Easing,
            StartTime = r.StartTime,
            EndTime = r.EndTime,
            StartValue = r.StartValue,
            EndValue = r.EndValue,
            ParentBlockId = r.ParentBlockId,
        }),
    };

    private static void AddRecordSnapshot(CompositionDocument doc, string commandId, List<CommandRecordSnapshot> list)
    {
        if (doc.CommandRecords.TryGetValue(commandId, out var rec))
        {
            list.Add(new CommandRecordSnapshot
            {
                CommandId = rec.CommandId,
                Lifecycle = rec.Lifecycle,
                DerivedCommandIds = new List<string>(rec.DerivedCommandIds),
                ParentBlockId = rec.ParentBlockId,
                ParentLayerId = rec.ParentLayerId,
                SourceReference = rec.SourceReference?.DeepClone(),
            });
        }
    }

    private static CommandRecord RestoreRecord(CommandRecordSnapshot snap) => new()
    {
        CommandId = snap.CommandId,
        Lifecycle = snap.Lifecycle,
        DerivedCommandIds = new List<string>(snap.DerivedCommandIds),
        ParentBlockId = snap.ParentBlockId,
        ParentLayerId = snap.ParentLayerId,
        SourceReference = snap.SourceReference?.DeepClone(),
    };

    private static StoryboardBlock? RecreateBlock(StoryboardBlockSnapshot snap)
    {
        if (snap.BlockType == "Loop")
        {
            return new LoopBlock
            {
                Id = snap.BlockId,
                LayerId = snap.LayerId,
                HeaderCommandId = snap.HeaderCommandId,
                EditAccess = snap.EditAccess,
                MaterializationState = snap.MaterializationState,
                StartTime = snap.StartTime,
                LoopCount = (int)snap.EndTimeOrCount,
                RelativeCommands = snap.RelativeCommands.ConvertAll(r => new RelativeCommand
                {
                    Id = r.Id,
                    CommandType = r.CommandType,
                    Easing = r.Easing,
                    StartTime = r.StartTime,
                    EndTime = r.EndTime,
                    StartValue = r.StartValue,
                    EndValue = r.EndValue,
                    ParentBlockId = r.ParentBlockId,
                }),
            };
        }
        if (snap.BlockType == "Trigger")
        {
            return new TriggerBlock
            {
                Id = snap.BlockId,
                LayerId = snap.LayerId,
                HeaderCommandId = snap.HeaderCommandId,
                EditAccess = snap.EditAccess,
                MaterializationState = snap.MaterializationState,
                TriggerName = snap.TriggerName ?? string.Empty,
                StartTime = snap.StartTime,
                EndTime = snap.EndTimeOrCount,
                Group = snap.TriggerGroup ?? 0,
                RelativeCommands = snap.RelativeCommands.ConvertAll(r => new RelativeCommand
                {
                    Id = r.Id,
                    CommandType = r.CommandType,
                    Easing = r.Easing,
                    StartTime = r.StartTime,
                    EndTime = r.EndTime,
                    StartValue = r.StartValue,
                    EndValue = r.EndValue,
                    ParentBlockId = r.ParentBlockId,
                }),
            };
        }
        return null;
    }

    private static bool InsertBlock(CompositionDocument doc, BlockInsertionAnchor anchor, StoryboardBlock block)
    {
        var layer = doc.Layers.FirstOrDefault(l => l.Id == anchor.LayerId);
        if (layer == null) return false; // LayerId missing = abort

        var container = FindContainerForInsertion(layer, anchor);

        // PreviousSibling -> insert after
        if (!string.IsNullOrEmpty(anchor.PreviousSiblingBlockId))
        {
            var idx = container.FindIndex(b => b.Id == anchor.PreviousSiblingBlockId);
            if (idx >= 0) { container.Insert(idx + 1, block); return true; }
        }
        // NextSibling -> insert before
        if (!string.IsNullOrEmpty(anchor.NextSiblingBlockId))
        {
            var idx = container.FindIndex(b => b.Id == anchor.NextSiblingBlockId);
            if (idx >= 0) { container.Insert(idx, block); return true; }
        }
        // OriginalIndexFallback clamp
        var fallbackIdx = Math.Clamp(anchor.OriginalIndexFallback, 0, container.Count);
        container.Insert(fallbackIdx, block);
        return true;
    }

    private static List<StoryboardBlock> FindContainerForInsertion(Layer layer, BlockInsertionAnchor anchor)
    {
        bool HasSibling(List<StoryboardBlock> blocks) =>
            (!string.IsNullOrEmpty(anchor.PreviousSiblingBlockId) && blocks.Any(b => b.Id == anchor.PreviousSiblingBlockId)) ||
            (!string.IsNullOrEmpty(anchor.NextSiblingBlockId) && blocks.Any(b => b.Id == anchor.NextSiblingBlockId));

        if (HasSibling(layer.Blocks))
            return layer.Blocks;
        foreach (var sprite in layer.Sprites)
        {
            if (HasSibling(sprite.Blocks))
                return sprite.Blocks;
        }
        return layer.Blocks;
    }

    private static Vector2ComponentMask ComponentMaskFor(string commandType) => commandType switch
    {
        "M" => Vector2ComponentMask.Both,
        "MX" => Vector2ComponentMask.X,
        "MY" => Vector2ComponentMask.Y,
        "S" => Vector2ComponentMask.Both,
        "V" => Vector2ComponentMask.Both,
        _ => Vector2ComponentMask.None,
    };

    private static void AddGeneratedKeyframes(CompositionDocument doc, ExpandBlockTransaction tx, SpriteDeclaration? sprite)
    {
        var layer = doc.Layers.FirstOrDefault(l => l.Id == tx.LayerId);
        if (layer == null) return;

        var tracks = sprite != null ? sprite.PropertyTracks : layer.PropertyTracks;

        foreach (var gen in tx.GeneratedCommandsAfter)
        {
            if (gen.CommandType == "F")
            {
                var track = GetOrCreateTrack(tracks, "Opacity", "Float");
                if (float.TryParse(gen.StartValue, out var val))
                    track.FloatKeyframes.Add(new Keyframe<float> { Time = gen.StartTime, Value = val, Easing = gen.Easing });
            }
            else if (gen.CommandType == "R")
            {
                var track = GetOrCreateTrack(tracks, "Rotation", "Float");
                if (float.TryParse(gen.StartValue, out var val))
                    track.FloatKeyframes.Add(new Keyframe<float> { Time = gen.StartTime, Value = val, Easing = gen.Easing });
            }
        }
    }

    private static void RemoveGeneratedKeyframes(CompositionDocument doc, ExpandBlockTransaction tx)
    {
        var layer = doc.Layers.FirstOrDefault(l => l.Id == tx.LayerId);
        if (layer == null) return;

        var genTimesByType = tx.GeneratedCommandsAfter
            .Where(g => g.CommandType == "F" || g.CommandType == "R")
            .Select(g => g.StartTime)
            .ToHashSet();

        void RemoveFromTracks(List<PropertyTrack> tracks)
        {
            foreach (var track in tracks)
            {
                if ((track.PropertyName == "Opacity" || track.PropertyName == "Rotation") && track.ValueType == "Float")
                    track.FloatKeyframes.RemoveAll(k => genTimesByType.Contains(k.Time));
            }
        }

        RemoveFromTracks(layer.PropertyTracks);
        foreach (var sprite in layer.Sprites)
            RemoveFromTracks(sprite.PropertyTracks);
    }

    private static PropertyTrack GetOrCreateTrack(List<PropertyTrack> tracks, string name, string valueType)
    {
        var track = tracks.FirstOrDefault(t => t.PropertyName == name);
        if (track == null)
        {
            track = new PropertyTrack { PropertyName = name, ValueType = valueType };
            tracks.Add(track);
        }
        return track;
    }
}
