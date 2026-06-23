using VisualCompositor.Core.Snapshots;

namespace VisualCompositor.Core.Validation;

public static class TransactionValidators
{
    /// <summary>A: Set(GeneratedCommandIds) == Set(GeneratedCommandsAfter.CommandId)</summary>
    public static void ValidateGeneratedCommandSetConsistency(ExpandBlockTransaction tx, DiagnosticCollection diagnostics)
    {
        var idsSet = new HashSet<string>(tx.GeneratedCommandIds);
        var afterSet = new HashSet<string>(tx.GeneratedCommandsAfter.Select(c => c.CommandId));

        if (!idsSet.SetEquals(afterSet))
        {
            diagnostics.Add(new Diagnostic
            {
                Code = FailureCodes.EXPAND_TRANSACTION_GENERATED_SET_MISMATCH,
                Message = $"GeneratedCommandIds set ({idsSet.Count}) does not match GeneratedCommandsAfter.CommandId set ({afterSet.Count})",
                Severity = DiagnosticSeverity.HardError,
                Scope = "transaction",
            });
        }
    }

    /// <summary>B: ids no duplicates, snapshot CommandId no duplicates and non-empty</summary>
    public static void ValidateGeneratedCommandUniqueness(ExpandBlockTransaction tx, DiagnosticCollection diagnostics)
    {
        // Check GeneratedCommandIds for duplicates
        var idCounts = tx.GeneratedCommandIds.GroupBy(id => id).Where(g => g.Count() > 1).ToList();
        foreach (var dup in idCounts)
        {
            diagnostics.Add(new Diagnostic
            {
                Code = FailureCodes.EXPAND_TRANSACTION_GENERATED_ID_DUPLICATE,
                Message = $"Duplicate GeneratedCommandId: {dup.Key} ({dup.Count()} times)",
                Severity = DiagnosticSeverity.HardError,
                Scope = "transaction",
            });
        }

        // Check GeneratedCommandsAfter for empty CommandId
        foreach (var snapshot in tx.GeneratedCommandsAfter)
        {
            if (string.IsNullOrEmpty(snapshot.CommandId))
            {
                diagnostics.Add(new Diagnostic
                {
                    Code = FailureCodes.EXPAND_TRANSACTION_GENERATED_SNAPSHOT_ID_EMPTY,
                    Message = "GeneratedCommandSnapshot has empty CommandId",
                    Severity = DiagnosticSeverity.HardError,
                    Scope = "transaction",
                });
            }
        }

        // Check GeneratedCommandsAfter for duplicate CommandId
        var snapshotIdCounts = tx.GeneratedCommandsAfter
            .Where(s => !string.IsNullOrEmpty(s.CommandId))
            .GroupBy(s => s.CommandId)
            .Where(g => g.Count() > 1)
            .ToList();
        foreach (var dup in snapshotIdCounts)
        {
            diagnostics.Add(new Diagnostic
            {
                Code = FailureCodes.EXPAND_TRANSACTION_GENERATED_SNAPSHOT_ID_DUPLICATE,
                Message = $"Duplicate GeneratedCommandSnapshot.CommandId: {dup.Key} ({dup.Count()} times)",
                Severity = DiagnosticSeverity.HardError,
                Scope = "transaction",
            });
        }

        // Check every GeneratedCommandsAfter.CommandId is in GeneratedCommandIds
        var idsSet = new HashSet<string>(tx.GeneratedCommandIds);
        foreach (var snapshot in tx.GeneratedCommandsAfter.Where(s => !string.IsNullOrEmpty(s.CommandId)))
        {
            if (!idsSet.Contains(snapshot.CommandId))
            {
                diagnostics.Add(new Diagnostic
                {
                    Code = FailureCodes.EXPAND_TRANSACTION_GENERATED_SNAPSHOT_NOT_IN_SET,
                    Message = $"GeneratedCommandSnapshot.CommandId '{snapshot.CommandId}' not in GeneratedCommandIds",
                    Severity = DiagnosticSeverity.HardError,
                    Scope = "transaction",
                });
            }
        }
    }

    /// <summary>C: OriginalBlockSnapshot header/relative ids all exist in AffectedCommandRecordsBefore</summary>
    public static void ValidateOriginalBlockRecordCompleteness(ExpandBlockTransaction tx, DiagnosticCollection diagnostics)
    {
        var beforeIds = new HashSet<string>(tx.AffectedCommandRecordsBefore.Select(r => r.CommandId));
        var snapshot = tx.OriginalBlockSnapshot;

        // Header command id must be in before records
        if (!string.IsNullOrEmpty(snapshot.HeaderCommandId) && !beforeIds.Contains(snapshot.HeaderCommandId))
        {
            diagnostics.Add(new Diagnostic
            {
                Code = FailureCodes.EXPAND_ORIGINAL_BLOCK_HEADER_RECORD_MISSING,
                Message = $"OriginalBlockSnapshot.HeaderCommandId '{snapshot.HeaderCommandId}' not found in AffectedCommandRecordsBefore",
                Severity = DiagnosticSeverity.HardError,
                Scope = "transaction",
            });
        }

        // Each relative command id must be in before records
        foreach (var relCmd in snapshot.RelativeCommands)
        {
            if (!string.IsNullOrEmpty(relCmd.Id) && !beforeIds.Contains(relCmd.Id))
            {
                diagnostics.Add(new Diagnostic
                {
                    Code = FailureCodes.EXPAND_ORIGINAL_BLOCK_RELATIVE_RECORD_MISSING,
                    Message = $"OriginalBlockSnapshot relative command id '{relCmd.Id}' not found in AffectedCommandRecordsBefore",
                    Severity = DiagnosticSeverity.HardError,
                    Scope = "transaction",
                });
            }
        }
    }

    /// <summary>D: AffectedCommandRecordsAfter contains generated records and source split records</summary>
    public static void ValidateRedoAfterRecordCompleteness(ExpandBlockTransaction tx, DiagnosticCollection diagnostics)
    {
        var afterIds = new HashSet<string>(tx.AffectedCommandRecordsAfter.Select(r => r.CommandId));

        // All generated command ids must have a record in after
        foreach (var genId in tx.GeneratedCommandIds)
        {
            if (!afterIds.Contains(genId))
            {
                diagnostics.Add(new Diagnostic
                {
                    Code = FailureCodes.EXPAND_REDO_AFTER_GENERATED_RECORD_MISSING,
                    Message = $"Generated command id '{genId}' has no record in AffectedCommandRecordsAfter",
                    Severity = DiagnosticSeverity.HardError,
                    Scope = "transaction",
                });
            }
        }

        // All source split records (from before that are Modified/Split) must be in after
        var sourceSplitIds = tx.AffectedCommandRecordsBefore
            .Where(r => r.Lifecycle == Core.Primitives.CommandRecordLifecycle.Split ||
                        r.Lifecycle == Core.Primitives.CommandRecordLifecycle.Modified)
            .Select(r => r.CommandId)
            .ToList();
        foreach (var splitId in sourceSplitIds)
        {
            if (!afterIds.Contains(splitId))
            {
                diagnostics.Add(new Diagnostic
                {
                    Code = FailureCodes.EXPAND_REDO_AFTER_SOURCE_SPLIT_RECORD_MISSING,
                    Message = $"Source split/modified record '{splitId}' not found in AffectedCommandRecordsAfter",
                    Severity = DiagnosticSeverity.HardError,
                    Scope = "transaction",
                });
            }
        }
    }

    /// <summary>F: RawBlockAnchorsBefore.RawBlockId set == RawBlockAnchorsAfter.RawBlockId set</summary>
    public static void ValidateRawBlockAnchorSnapshotSetConsistency(ExpandBlockTransaction tx, DiagnosticCollection diagnostics)
    {
        var beforeSet = new HashSet<string>(tx.RawBlockAnchorsBefore.Select(a => a.RawBlockId));
        var afterSet = new HashSet<string>(tx.RawBlockAnchorsAfter.Select(a => a.RawBlockId));

        if (!beforeSet.SetEquals(afterSet))
        {
            var missing = beforeSet.Except(afterSet).ToList();
            var extra = afterSet.Except(beforeSet).ToList();
            diagnostics.Add(new Diagnostic
            {
                Code = FailureCodes.RAW_BLOCK_ANCHOR_SET_MISMATCH,
                Message = $"RawBlock anchor set mismatch. Missing in after: [{string.Join(", ", missing)}], Extra in after: [{string.Join(", ", extra)}]",
                Severity = DiagnosticSeverity.HardError,
                Scope = "transaction",
            });
        }
    }

    /// <summary>L: InsertionAnchor.LayerId == transaction.LayerId</summary>
    public static void ValidateInsertionAnchorLayerConsistency(ExpandBlockTransaction tx, DiagnosticCollection diagnostics)
    {
        if (tx.InsertionAnchor.LayerId != tx.LayerId)
        {
            diagnostics.Add(new Diagnostic
            {
                Code = FailureCodes.INSERTION_ANCHOR_LAYER_MISMATCH,
                Message = $"InsertionAnchor.LayerId '{tx.InsertionAnchor.LayerId}' != transaction.LayerId '{tx.LayerId}'",
                Severity = DiagnosticSeverity.HardError,
                Scope = "transaction",
            });
        }
    }
}
