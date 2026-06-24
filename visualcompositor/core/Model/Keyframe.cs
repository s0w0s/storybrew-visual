using VisualCompositor.Core.Primitives;

namespace VisualCompositor.Core.Model;

/// <summary>A single keyframe on a property track.</summary>
public sealed class Keyframe<T>
{
    public double Time { get; set; }
    public T Value { get; set; } = default!;
    public OsbEasing Easing { get; set; }

    /// <summary>Optional bezier handles for curve editing (null for linear/osu easing).</summary>
    public BezierHandles? Handles { get; set; }

    public Keyframe<T> Clone() => new()
    {
        Time = Time,
        Value = Value,
        Easing = Easing,
        Handles = Handles?.Clone(),
    };
}

/// <summary>Bezier curve handles for a keyframe.</summary>
public sealed class BezierHandles
{
    public Vector2 InHandle { get; set; }
    public Vector2 OutHandle { get; set; }

    public BezierHandles Clone() => new() { InHandle = InHandle, OutHandle = OutHandle };
}
