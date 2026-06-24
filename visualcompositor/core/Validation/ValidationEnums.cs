namespace VisualCompositor.Core.Validation;

public enum ValidationEntryPoint
{
    Create,
    DeserializeLoad,
    UndoRestore,
    RedoRestore,
    PreOsbExport,
    PreStorybrewCompSave,
    DebugIntegrityScan,
}

public enum ValidationScope
{
    DocumentState,
    TransactionHistory,
    Serialization,
}
