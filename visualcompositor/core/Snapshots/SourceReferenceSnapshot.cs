namespace VisualCompositor.Core.Snapshots;

/// <summary>Provenance reference to original .osb source location. Embedded-persistent, provenance-only.
/// Not a standalone top-level state; persisted only with its host snapshot.</summary>
public sealed class SourceReferenceSnapshot
{
    /// <summary>Source file path (e.g. the .osb file).</summary>
    public string? SourcePath { get; init; }

    /// <summary>Line number in the source file (1-based).</summary>
    public int? SourceLine { get; init; }

    /// <summary>Raw text of the source line.</summary>
    public string? SourceText { get; init; }

    public SourceReferenceSnapshot DeepClone() => new()
    {
        SourcePath = SourcePath,
        SourceLine = SourceLine,
        SourceText = SourceText,
    };
}
