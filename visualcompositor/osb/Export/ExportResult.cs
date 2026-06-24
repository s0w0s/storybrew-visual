using VisualCompositor.Core.Validation;

namespace VisualCompositor.Osb.Export;

/// <summary>Result of an .osb export operation.</summary>
public sealed class ExportResult
{
    /// <summary>The exported .osb text. Empty when <see cref="Succeeded"/> is false.</summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>Diagnostics produced during export (validation + export-time warnings).</summary>
    public List<Diagnostic> Diagnostics { get; init; } = new();

    /// <summary>True if the export produced text; false if pre-export validation found hard errors.</summary>
    public bool Succeeded { get; init; }
}
