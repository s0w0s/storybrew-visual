using VisualCompositor.Core.Primitives;

namespace VisualCompositor.Core.Model.GraphEditor;

/// <summary>Extended keyframe metadata for the graph editor. Wraps a keyframe with editor-specific
/// info (tangent mode, lock state). The graph editor works in 1D per property component
/// (e.g., PositionX, PositionY, Opacity).</summary>
public sealed class GraphKeyframe
{
    /// <summary>Time in milliseconds.</summary>
    public double Time { get; set; }

    /// <summary>Scalar value at this keyframe (one component of a property).</summary>
    public float Value { get; set; }

    /// <summary>Osu easing applied to the segment starting at this keyframe.</summary>
    public OsbEasing Easing { get; set; }

    /// <summary>Optional bezier handles for curve editing (null for linear/osu easing).</summary>
    public BezierHandles? Handles { get; set; }

    /// <summary>How the bezier handles are computed/maintained. Default <see cref="TangentMode.Auto"/>.</summary>
    public TangentMode TangentMode { get; set; } = TangentMode.Auto;

    /// <summary>When true, the keyframe cannot be edited (locked in the UI).</summary>
    public bool Locked { get; set; }

    /// <summary>Deep clone.</summary>
    public GraphKeyframe Clone() => new()
    {
        Time = Time,
        Value = Value,
        Easing = Easing,
        Handles = Handles?.Clone(),
        TangentMode = TangentMode,
        Locked = Locked,
    };
}
