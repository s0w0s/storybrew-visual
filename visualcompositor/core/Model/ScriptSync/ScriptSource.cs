namespace VisualCompositor.Core.Model.ScriptSync;

/// <summary>Metadata about a storybrew script that generated part of the composition.</summary>
public sealed class ScriptSource
{
    /// <summary>Opaque persistent identifier for the script.</summary>
    public string ScriptId { get; set; } = string.Empty;

    /// <summary>The script's display name / identifier.</summary>
    public string Identifier { get; set; } = string.Empty;

    /// <summary>Relative path to the .cs script file.</summary>
    public string ScriptPath { get; set; } = string.Empty;

    /// <summary>Hash of the script content at last sync (for change detection).</summary>
    public string ContentHash { get; set; } = string.Empty;

    /// <summary>Time of the last synchronization.</summary>
    public DateTimeOffset LastSyncTime { get; set; }

    /// <summary>Script configurable field values (name -> value).</summary>
    public Dictionary<string, string> Parameters { get; set; } = new();

    public ScriptSource Clone() => new()
    {
        ScriptId = ScriptId,
        Identifier = Identifier,
        ScriptPath = ScriptPath,
        ContentHash = ContentHash,
        LastSyncTime = LastSyncTime,
        Parameters = new Dictionary<string, string>(Parameters),
    };
}
