namespace VisualCompositor.Core.Validation;

public static class HistoryValidators
{
    /// <summary>K: Successful edit clears redo stack (atomic). Failed edit/undo/redo does not change stacks.
    /// This validator checks the invariant after an operation: if a new transaction was committed,
    /// the redo stack must be empty. If an operation failed, both stacks must be unchanged.</summary>
    public static void ValidateRedoStackIntegrity(
        bool wasEditCommitted,
        int undoStackCountBefore,
        int redoStackCountBefore,
        int undoStackCountAfter,
        int redoStackCountAfter,
        bool operationSucceeded,
        DiagnosticCollection diagnostics)
    {
        if (operationSucceeded && wasEditCommitted)
        {
            // Successful new edit: redo stack must be cleared, undo stack must have grown
            if (redoStackCountAfter != 0)
            {
                diagnostics.Add(new Diagnostic
                {
                    Code = FailureCodes.REDO_STACK_NOT_CLEARED,
                    Message = $"Successful edit committed but redo stack not cleared (was {redoStackCountBefore}, now {redoStackCountAfter})",
                    Severity = DiagnosticSeverity.HardError,
                    Scope = "transaction-history",
                });
            }
        }
        else if (!operationSucceeded)
        {
            // Failed operation: stacks must be unchanged
            if (undoStackCountAfter != undoStackCountBefore || redoStackCountAfter != redoStackCountBefore)
            {
                diagnostics.Add(new Diagnostic
                {
                    Code = FailureCodes.STACK_MODIFIED_ON_FAILURE,
                    Message = $"Failed operation modified stacks. Undo: {undoStackCountBefore}->{undoStackCountAfter}, Redo: {redoStackCountBefore}->{redoStackCountAfter}",
                    Severity = DiagnosticSeverity.HardError,
                    Scope = "transaction-history",
                });
            }
        }
    }
}
