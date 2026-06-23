using VisualCompositor.Core.Validation;
using Xunit;

namespace VisualCompositor.Core.Tests;

public class HistoryValidatorsTests
{
    // K: RedoStackIntegrity
    [Fact]
    public void K_SuccessfulEditClearsRedoStack_Passes()
    {
        var diagnostics = new DiagnosticCollection();
        HistoryValidators.ValidateRedoStackIntegrity(
            wasEditCommitted: true,
            undoStackCountBefore: 2,
            redoStackCountBefore: 3,
            undoStackCountAfter: 3,
            redoStackCountAfter: 0,
            operationSucceeded: true,
            diagnostics);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void K_SuccessfulEditRedoNotCleared_Fails()
    {
        var diagnostics = new DiagnosticCollection();
        HistoryValidators.ValidateRedoStackIntegrity(
            wasEditCommitted: true,
            undoStackCountBefore: 2,
            redoStackCountBefore: 3,
            undoStackCountAfter: 3,
            redoStackCountAfter: 2, // redo not cleared!
            operationSucceeded: true,
            diagnostics);
        Assert.True(diagnostics.HasErrors);
        Assert.Contains(diagnostics.Diagnostics, d => d.Code == FailureCodes.REDO_STACK_NOT_CLEARED);
    }

    [Fact]
    public void K_FailedOperationStacksUnchanged_Passes()
    {
        var diagnostics = new DiagnosticCollection();
        HistoryValidators.ValidateRedoStackIntegrity(
            wasEditCommitted: false,
            undoStackCountBefore: 2,
            redoStackCountBefore: 3,
            undoStackCountAfter: 2,
            redoStackCountAfter: 3,
            operationSucceeded: false,
            diagnostics);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void K_FailedOperationStacksChanged_Fails()
    {
        var diagnostics = new DiagnosticCollection();
        HistoryValidators.ValidateRedoStackIntegrity(
            wasEditCommitted: false,
            undoStackCountBefore: 2,
            redoStackCountBefore: 3,
            undoStackCountAfter: 3, // undo stack changed on failure!
            redoStackCountAfter: 3,
            operationSucceeded: false,
            diagnostics);
        Assert.True(diagnostics.HasErrors);
        Assert.Contains(diagnostics.Diagnostics, d => d.Code == FailureCodes.STACK_MODIFIED_ON_FAILURE);
    }
}
