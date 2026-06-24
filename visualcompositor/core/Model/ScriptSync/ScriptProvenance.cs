namespace VisualCompositor.Core.Model.ScriptSync;

/// <summary>Links a sprite or block to its generating script.</summary>
public sealed class ScriptProvenance
{
    /// <summary>The id of the script that generated this entity (references <see cref="ScriptSource.ScriptId"/>).</summary>
    public string ScriptId { get; set; } = string.Empty;

    /// <summary>The id of the sprite or block.</summary>
    public string EntityId { get; set; } = string.Empty;

    /// <summary>The kind of entity: "Sprite" or "Block".</summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>Current synchronization state of the entity.</summary>
    public ScriptSyncState SyncState { get; set; } = ScriptSyncState.Synced;

    public ScriptProvenance Clone() => new()
    {
        ScriptId = ScriptId,
        EntityId = EntityId,
        EntityType = EntityType,
        SyncState = SyncState,
    };
}
