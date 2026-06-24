namespace VisualCompositor.Core.Model.ScriptSync;

/// <summary>Synchronization state of an entity relative to its generating script.</summary>
public enum ScriptSyncState
{
    /// <summary>In sync with the generating script.</summary>
    Synced,

    /// <summary>Manually edited after the last sync.</summary>
    Modified,

    /// <summary>The script no longer generates this entity.</summary>
    Orphaned,

    /// <summary>Created manually, no script source.</summary>
    Manual,
}
