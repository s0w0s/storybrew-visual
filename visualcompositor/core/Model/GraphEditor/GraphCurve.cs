using VisualCompositor.Core.Primitives;

namespace VisualCompositor.Core.Model.GraphEditor;

/// <summary>A curve segment between two keyframes, representing the interpolation.
/// Sampling uses the easing function, or the bezier handles when present.</summary>
public sealed class GraphCurve
{
    /// <summary>Start time in milliseconds.</summary>
    public double StartTime { get; set; }

    /// <summary>End time in milliseconds.</summary>
    public double EndTime { get; set; }

    /// <summary>Value at the start of the segment.</summary>
    public float StartValue { get; set; }

    /// <summary>Value at the end of the segment.</summary>
    public float EndValue { get; set; }

    /// <summary>Easing applied to the segment (used when no bezier handles are present).</summary>
    public OsbEasing Easing { get; set; }

    /// <summary>Bezier handles at the start keyframe (null for easing-only segments).</summary>
    public BezierHandles? StartHandles { get; set; }

    /// <summary>Bezier handles at the end keyframe (null for easing-only segments).</summary>
    public BezierHandles? EndHandles { get; set; }

    /// <summary>Evaluate the curve at the given time. Clamps to [StartTime, EndTime].</summary>
    public float SampleAt(double time)
    {
        if (EndTime <= StartTime) return StartValue;
        if (time <= StartTime) return StartValue;
        if (time >= EndTime) return EndValue;

        var t = (float)((time - StartTime) / (EndTime - StartTime));

        // Bezier path: 1D cubic bezier using handle Y components (value dimension),
        // with X (time dimension) assumed uniform.
        if (StartHandles != null)
        {
            var p0 = StartValue;
            var p1 = StartValue + StartHandles.OutHandle.Y;
            var p2 = EndValue + (EndHandles?.InHandle.Y ?? 0f);
            var p3 = EndValue;
            return CubicBezier1D(p0, p1, p2, p3, t);
        }

        var eased = EasingFunctions.EvaluateClamped(Easing, t);
        return StartValue + (EndValue - StartValue) * eased;
    }

    private static float CubicBezier1D(float p0, float p1, float p2, float p3, float t)
    {
        var u = 1f - t;
        var tt = t * t;
        var uu = u * u;
        return uu * u * p0 + 3f * uu * t * p1 + 3f * u * tt * p2 + tt * t * p3;
    }

    /// <summary>Deep clone.</summary>
    public GraphCurve Clone() => new()
    {
        StartTime = StartTime,
        EndTime = EndTime,
        StartValue = StartValue,
        EndValue = EndValue,
        Easing = Easing,
        StartHandles = StartHandles?.Clone(),
        EndHandles = EndHandles?.Clone(),
    };
}
