using VisualCompositor.Core.Model;
using VisualCompositor.Core.Primitives;

namespace VisualCompositor.Core.Validation;

public static class DocumentValidators
{
    /// <summary>E: HeaderCommandRecord.DerivedCommandIds == disjoint union of non-header split records' DerivedCommandIds.
    /// Also checks each generated id has exactly one relative source.</summary>
    public static void ValidateDerivedCommandPartition(CompositionDocument doc, DiagnosticCollection diagnostics)
    {
        foreach (var layer in doc.Layers)
        {
            ValidateDerivedCommandPartitionForBlocks(layer.Blocks, layer.Id, doc.CommandRecords, diagnostics);
            foreach (var sprite in layer.Sprites)
            {
                ValidateDerivedCommandPartitionForBlocks(sprite.Blocks, layer.Id, doc.CommandRecords, diagnostics);
            }
        }
    }

    private static void ValidateDerivedCommandPartitionForBlocks(List<StoryboardBlock> blocks, string layerId, Dictionary<string, CommandRecord> records, DiagnosticCollection diagnostics)
    {
        foreach (var block in blocks)
        {
            if (!records.TryGetValue(block.HeaderCommandId, out var headerRecord))
                continue; // Validator I handles missing header record

            var headerDerived = new HashSet<string>(headerRecord.DerivedCommandIds);
            var nonHeaderUnion = new HashSet<string>();
            var relativeIds = block switch
            {
                LoopBlock lb => lb.RelativeCommands.Select(c => c.Id),
                TriggerBlock tb => tb.RelativeCommands.Select(c => c.Id),
                _ => Enumerable.Empty<string>(),
            };

            foreach (var relId in relativeIds)
            {
                if (records.TryGetValue(relId, out var relRecord))
                {
                    foreach (var derived in relRecord.DerivedCommandIds)
                    {
                        if (nonHeaderUnion.Contains(derived))
                        {
                            diagnostics.Add(new Diagnostic
                            {
                                Code = FailureCodes.DERIVED_COMMAND_OVERLAP,
                                Message = $"Derived command id '{derived}' appears in multiple non-header records",
                                Severity = DiagnosticSeverity.HardError,
                                Scope = "document-state",
                            });
                        }
                        nonHeaderUnion.Add(derived);
                    }
                }
            }

            // Check header-only generated ids (only allowed with EXPAND_DERIVED_SOURCE_AMBIGUOUS warning)
            var headerOnly = headerDerived.Except(nonHeaderUnion).ToList();
            if (headerOnly.Count > 0)
            {
                diagnostics.Add(new Diagnostic
                {
                    Code = FailureCodes.EXPAND_DERIVED_SOURCE_AMBIGUOUS,
                    Message = $"Header-only derived ids (no relative source): [{string.Join(", ", headerOnly)}]",
                    Severity = DiagnosticSeverity.Warning,
                    Scope = "document-state",
                });
            }

            // Check partition: headerDerived must equal nonHeaderUnion
            var inUnionNotHeader = nonHeaderUnion.Except(headerDerived).ToList();
            // inHeaderNotUnion already warned as ambiguous above, only error if inUnionNotHeader
            if (inUnionNotHeader.Count > 0)
            {
                diagnostics.Add(new Diagnostic
                {
                    Code = FailureCodes.DERIVED_COMMAND_PARTITION_MISMATCH,
                    Message = $"Non-header derived ids not in header: [{string.Join(", ", inUnionNotHeader)}]",
                    Severity = DiagnosticSeverity.HardError,
                    Scope = "document-state",
                });
            }
        }
    }

    /// <summary>G: RawBlock anchor valid combinations + AfterCommand target exists in commandRecords</summary>
    public static void ValidateRawBlockAnchorTargetExistence(CompositionDocument doc, DiagnosticCollection diagnostics)
    {
        foreach (var rawBlock in doc.RawBlocks)
        {
            // Validate combination
            if (rawBlock.AnchorKind == RawBlockAnchorKind.LayerStart)
            {
                if (rawBlock.AnchorCommandId != null)
                {
                    diagnostics.Add(new Diagnostic
                    {
                        Code = FailureCodes.RAW_BLOCK_ANCHOR_INVALID_COMBINATION,
                        Message = $"RawBlock '{rawBlock.Id}' has LayerStart anchor but AnchorCommandId is not null",
                        Severity = DiagnosticSeverity.HardError,
                        Scope = "document-state",
                    });
                }
            }
            else if (rawBlock.AnchorKind == RawBlockAnchorKind.AfterCommand)
            {
                if (rawBlock.AnchorCommandId == null)
                {
                    diagnostics.Add(new Diagnostic
                    {
                        Code = FailureCodes.RAW_BLOCK_ANCHOR_INVALID_COMBINATION,
                        Message = $"RawBlock '{rawBlock.Id}' has AfterCommand anchor but AnchorCommandId is null",
                        Severity = DiagnosticSeverity.HardError,
                        Scope = "document-state",
                    });
                }
                else if (!doc.CommandRecords.ContainsKey(rawBlock.AnchorCommandId))
                {
                    diagnostics.Add(new Diagnostic
                    {
                        Code = FailureCodes.RAW_BLOCK_ANCHOR_TARGET_MISSING,
                        Message = $"RawBlock '{rawBlock.Id}' AfterCommand anchor target '{rawBlock.AnchorCommandId}' not found in commandRecords",
                        Severity = DiagnosticSeverity.HardError,
                        Scope = "document-state",
                    });
                }
            }
        }
    }

    /// <summary>H: RelativeCommand.Id exists in records and == CommandRecord.CommandId</summary>
    public static void ValidateRelativeCommandRecordIdentity(CompositionDocument doc, DiagnosticCollection diagnostics)
    {
        foreach (var layer in doc.Layers)
        {
            ValidateRelativeCommandsForBlocks(layer.Blocks, doc.CommandRecords, diagnostics);
            foreach (var sprite in layer.Sprites)
            {
                ValidateRelativeCommandsForBlocks(sprite.Blocks, doc.CommandRecords, diagnostics);
            }
        }
    }

    private static void ValidateRelativeCommandsForBlocks(List<StoryboardBlock> blocks, Dictionary<string, CommandRecord> records, DiagnosticCollection diagnostics)
    {
        foreach (var block in blocks)
        {
            var relativeCommands = block switch
            {
                LoopBlock lb => lb.RelativeCommands,
                TriggerBlock tb => tb.RelativeCommands,
                _ => new List<RelativeCommand>(),
            };

            foreach (var relCmd in relativeCommands)
            {
                if (!records.TryGetValue(relCmd.Id, out var record))
                {
                    diagnostics.Add(new Diagnostic
                    {
                        Code = FailureCodes.RELATIVE_COMMAND_RECORD_MISSING,
                        Message = $"RelativeCommand.Id '{relCmd.Id}' not found in commandRecords (block {block.Id})",
                        Severity = DiagnosticSeverity.HardError,
                        Scope = "document-state",
                    });
                }
                else if (record.CommandId != relCmd.Id)
                {
                    diagnostics.Add(new Diagnostic
                    {
                        Code = FailureCodes.RELATIVE_COMMAND_ID_MISMATCH,
                        Message = $"RelativeCommand.Id '{relCmd.Id}' != CommandRecord.CommandId '{record.CommandId}'",
                        Severity = DiagnosticSeverity.HardError,
                        Scope = "document-state",
                    });
                }
            }
        }
    }

    /// <summary>I: Block.HeaderCommandId record exists and parent matches</summary>
    public static void ValidateHeaderCommandIdentity(CompositionDocument doc, DiagnosticCollection diagnostics)
    {
        foreach (var layer in doc.Layers)
        {
            ValidateHeaderForBlocks(layer.Blocks, layer.Id, doc.CommandRecords, diagnostics);
            foreach (var sprite in layer.Sprites)
            {
                ValidateHeaderForBlocks(sprite.Blocks, layer.Id, doc.CommandRecords, diagnostics);
            }
        }
    }

    private static void ValidateHeaderForBlocks(List<StoryboardBlock> blocks, string layerId, Dictionary<string, CommandRecord> records, DiagnosticCollection diagnostics)
    {
        foreach (var block in blocks)
        {
            if (!records.TryGetValue(block.HeaderCommandId, out var record))
            {
                diagnostics.Add(new Diagnostic
                {
                    Code = FailureCodes.HEADER_COMMAND_RECORD_MISSING,
                    Message = $"Block '{block.Id}' HeaderCommandId '{block.HeaderCommandId}' not found in commandRecords",
                    Severity = DiagnosticSeverity.HardError,
                    Scope = "document-state",
                });
            }
            else
            {
                // Parent must match: record.ParentBlockId == block.Id, record.ParentLayerId == layerId
                if (record.ParentBlockId != block.Id)
                {
                    diagnostics.Add(new Diagnostic
                    {
                        Code = FailureCodes.HEADER_COMMAND_PARENT_MISMATCH,
                        Message = $"Block '{block.Id}' header record ParentBlockId '{record.ParentBlockId}' != block.Id",
                        Severity = DiagnosticSeverity.HardError,
                        Scope = "document-state",
                    });
                }
                if (record.ParentLayerId != layerId)
                {
                    diagnostics.Add(new Diagnostic
                    {
                        Code = FailureCodes.HEADER_COMMAND_PARENT_MISMATCH,
                        Message = $"Block '{block.Id}' header record ParentLayerId '{record.ParentLayerId}' != layer '{layerId}'",
                        Severity = DiagnosticSeverity.HardError,
                        Scope = "document-state",
                    });
                }
            }
        }
    }

    /// <summary>J: ids non-empty, unique, references valid. No hash-content validation.</summary>
    public static void ValidateOpaquePersistentIds(CompositionDocument doc, DiagnosticCollection diagnostics)
    {
        var allIds = new HashSet<string>();

        // Collect all ids from layers, sprites, blocks, commands, rawBlocks
        foreach (var layer in doc.Layers)
        {
            CheckId(layer.Id, allIds, diagnostics, "Layer");
            foreach (var sprite in layer.Sprites)
            {
                CheckId(sprite.Id, allIds, diagnostics, "Sprite");
                foreach (var block in sprite.Blocks)
                {
                    CheckId(block.Id, allIds, diagnostics, "Block");
                    CheckId(block.HeaderCommandId, allIds, diagnostics, "HeaderCommand");
                }
            }
            foreach (var block in layer.Blocks)
            {
                CheckId(block.Id, allIds, diagnostics, "Block");
                CheckId(block.HeaderCommandId, allIds, diagnostics, "HeaderCommand");
            }
        }

        foreach (var rawBlock in doc.RawBlocks)
        {
            CheckId(rawBlock.Id, allIds, diagnostics, "RawBlock");
        }

        foreach (var kvp in doc.CommandRecords)
        {
            if (string.IsNullOrEmpty(kvp.Key))
            {
                diagnostics.Add(new Diagnostic
                {
                    Code = FailureCodes.ID_EMPTY,
                    Message = "CommandRecord has empty key",
                    Severity = DiagnosticSeverity.HardError,
                    Scope = "document-state",
                });
            }
        }

        // Check expanded block history references
        foreach (var history in doc.ExpandedBlockHistories)
        {
            CheckId(history.ExpandedBlockId, allIds, diagnostics, "ExpandedBlockHistory");
        }
    }

    private static void CheckId(string id, HashSet<string> allIds, DiagnosticCollection diagnostics, string entityType)
    {
        if (string.IsNullOrEmpty(id))
        {
            diagnostics.Add(new Diagnostic
            {
                Code = FailureCodes.ID_EMPTY,
                Message = $"{entityType} has empty id",
                Severity = DiagnosticSeverity.HardError,
                Scope = "document-state",
            });
            return;
        }

        if (!allIds.Add(id))
        {
            diagnostics.Add(new Diagnostic
            {
                Code = FailureCodes.ID_DUPLICATE,
                Message = $"Duplicate id '{id}' on {entityType}",
                Severity = DiagnosticSeverity.HardError,
                Scope = "document-state",
            });
        }
    }
}
