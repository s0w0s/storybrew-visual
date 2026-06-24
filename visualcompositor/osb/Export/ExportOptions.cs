using VisualCompositor.Core.Primitives;

namespace VisualCompositor.Osb.Export;

/// <summary>Options controlling .osb export behavior (design document §7).</summary>
public sealed class ExportOptions
{
    /// <summary>How bezier curves with handles are exported. Default <see cref="BezierExportMode.WarnOnly"/>.</summary>
    public BezierExportMode BezierExportMode { get; init; } = BezierExportMode.WarnOnly;

    /// <summary>Number of subdivision segments used when <see cref="BezierExportMode"/> is
    /// <see cref="BezierExportMode.BakeToSegments"/>. Default 16.</summary>
    public int BezierBakeSegments { get; init; } = 16;

    /// <summary>Whether to apply variable substitution on export. Default false (MVP: emit literal values).</summary>
    public bool VariableSubstitution { get; init; } = false;

    /// <summary>Whether to emit the optional trigger group even when it is zero. Default true.</summary>
    public bool AlwaysEmitTriggerGroup { get; init; } = true;
}
