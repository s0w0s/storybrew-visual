using VisualCompositor.Core.Model;
using VisualCompositor.Core.Snapshots;

namespace VisualCompositor.Core.Validation;

/// <summary>Dispatches validators based on entry point and scope.
/// Per §14:
/// - pre-osb-export: only document-state validators (E/G/H/I/J)
/// - pre-storybrewcomp-save: document + transaction + serialization validators
/// - create/deserialize-load/undo-restore/redo-restore: all relevant validators
/// - debug-integrity-scan: all validators</summary>
public static class ValidationDispatcher
{
    /// <summary>Validate a CompositionDocument at the given entry point.</summary>
    public static DiagnosticCollection Validate(CompositionDocument document, ValidationEntryPoint entryPoint)
    {
        var diagnostics = new DiagnosticCollection();
        var scopes = GetScopesForEntryPoint(entryPoint);

        if (scopes.Contains(ValidationScope.DocumentState))
        {
            // E: DerivedCommandIds partition
            DocumentValidators.ValidateDerivedCommandPartition(document, diagnostics);
            // G: RawBlock anchor target existence
            DocumentValidators.ValidateRawBlockAnchorTargetExistence(document, diagnostics);
            // H: RelativeCommand record identity
            DocumentValidators.ValidateRelativeCommandRecordIdentity(document, diagnostics);
            // I: HeaderCommand identity
            DocumentValidators.ValidateHeaderCommandIdentity(document, diagnostics);
            // J: Opaque persistent ids
            DocumentValidators.ValidateOpaquePersistentIds(document, diagnostics);
        }

        if (scopes.Contains(ValidationScope.Serialization))
        {
            SerializationValidators.ValidateRoundTripFidelity(document, diagnostics);
        }

        // TransactionHistory scope validators for documents check document-level transaction invariants
        // (the transaction-specific validators A-D, F, L are called on ExpandBlockTransaction directly)

        return diagnostics;
    }

    /// <summary>Validate an ExpandBlockTransaction at the given entry point.
    /// Called during create/undo-restore/redo-restore.</summary>
    public static DiagnosticCollection ValidateTransaction(ExpandBlockTransaction transaction, ValidationEntryPoint entryPoint)
    {
        var diagnostics = new DiagnosticCollection();
        var scopes = GetScopesForEntryPoint(entryPoint);

        if (scopes.Contains(ValidationScope.TransactionHistory))
        {
            // A: Generated command set consistency
            TransactionValidators.ValidateGeneratedCommandSetConsistency(transaction, diagnostics);
            // B: Generated command uniqueness
            TransactionValidators.ValidateGeneratedCommandUniqueness(transaction, diagnostics);
            // C: Original block record completeness
            TransactionValidators.ValidateOriginalBlockRecordCompleteness(transaction, diagnostics);
            // D: Redo after record completeness
            TransactionValidators.ValidateRedoAfterRecordCompleteness(transaction, diagnostics);
            // F: RawBlock anchor snapshot set consistency
            TransactionValidators.ValidateRawBlockAnchorSnapshotSetConsistency(transaction, diagnostics);
            // L: InsertionAnchor layer consistency
            TransactionValidators.ValidateInsertionAnchorLayerConsistency(transaction, diagnostics);
        }

        return diagnostics;
    }

    /// <summary>Validate redo stack integrity (K).</summary>
    public static DiagnosticCollection ValidateHistory(
        bool wasEditCommitted,
        int undoStackCountBefore,
        int redoStackCountBefore,
        int undoStackCountAfter,
        int redoStackCountAfter,
        bool operationSucceeded,
        ValidationEntryPoint entryPoint)
    {
        var diagnostics = new DiagnosticCollection();
        var scopes = GetScopesForEntryPoint(entryPoint);

        if (scopes.Contains(ValidationScope.TransactionHistory))
        {
            HistoryValidators.ValidateRedoStackIntegrity(
                wasEditCommitted, undoStackCountBefore, redoStackCountBefore,
                undoStackCountAfter, redoStackCountAfter, operationSucceeded, diagnostics);
        }

        return diagnostics;
    }

    /// <summary>Get the validation scopes that apply to an entry point.
    /// Per §14:
    /// - pre-osb-export: document-state only
    /// - pre-storybrewcomp-save: document-state + transaction-history + serialization
    /// - create/deserialize-load/undo-restore/redo-restore: document-state + transaction-history
    /// - debug-integrity-scan: all scopes</summary>
    public static HashSet<ValidationScope> GetScopesForEntryPoint(ValidationEntryPoint entryPoint)
    {
        return entryPoint switch
        {
            ValidationEntryPoint.PreOsbExport => new HashSet<ValidationScope> { ValidationScope.DocumentState },
            ValidationEntryPoint.PreStorybrewCompSave => new HashSet<ValidationScope>
            {
                ValidationScope.DocumentState,
                ValidationScope.TransactionHistory,
                ValidationScope.Serialization,
            },
            ValidationEntryPoint.Create => new HashSet<ValidationScope>
            {
                ValidationScope.DocumentState,
                ValidationScope.TransactionHistory,
            },
            ValidationEntryPoint.DeserializeLoad => new HashSet<ValidationScope>
            {
                ValidationScope.DocumentState,
                ValidationScope.Serialization,
            },
            ValidationEntryPoint.UndoRestore => new HashSet<ValidationScope>
            {
                ValidationScope.DocumentState,
                ValidationScope.TransactionHistory,
            },
            ValidationEntryPoint.RedoRestore => new HashSet<ValidationScope>
            {
                ValidationScope.DocumentState,
                ValidationScope.TransactionHistory,
            },
            ValidationEntryPoint.DebugIntegrityScan => new HashSet<ValidationScope>
            {
                ValidationScope.DocumentState,
                ValidationScope.TransactionHistory,
                ValidationScope.Serialization,
            },
            _ => new HashSet<ValidationScope>(),
        };
    }
}
