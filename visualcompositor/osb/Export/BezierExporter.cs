using VisualCompositor.Core.Model;
using VisualCompositor.Core.Primitives;
using VisualCompositor.Core.Validation;

namespace VisualCompositor.Osb.Export;

/// <summary>Handles bezier curve export per design document §7.
/// Bezier curves (keyframes with non-null <see cref="BezierHandles"/>) cannot be directly
/// expressed in .osb. The export mode determines how they are handled:
/// <list type="bullet">
/// <item><see cref="BezierExportMode.WarnOnly"/>: emit a warning and export as linear (ignore handles).</item>
/// <item><see cref="BezierExportMode.FitNearestOsuEasing"/>: emit a warning and keep the existing easing (MVP: no fitting).</item>
/// <item><see cref="BezierExportMode.BakeToSegments"/>: subdivide the bezier into linear segments.</item>
/// </list>
/// Default mode is <see cref="BezierExportMode.WarnOnly"/>; the exporter never silently drops
/// inexpressible curves.</summary>
internal sealed class BezierExporter
{
    private readonly ExportOptions _options;
    private readonly List<Diagnostic> _diagnostics;

    public BezierExporter(ExportOptions options, List<Diagnostic> diagnostics)
    {
        _options = options;
        _diagnostics = diagnostics;
    }

    /// <summary>Process a list of float keyframes, returning the exported keyframe list.
    /// For <see cref="BezierExportMode.BakeToSegments"/>, intermediate keyframes are inserted
    /// for any segment whose start keyframe has bezier handles.</summary>
    public List<Keyframe<float>> ProcessFloat(List<Keyframe<float>> keyframes)
    {
        WarnKeyframes(keyframes, "float");
        return _options.BezierExportMode == BezierExportMode.BakeToSegments
            ? BakeFloat(keyframes)
            : keyframes;
    }

    /// <summary>Process a list of Vector2 keyframes.</summary>
    public List<Keyframe<Vector2>> ProcessVector2(List<Keyframe<Vector2>> keyframes)
    {
        WarnKeyframes(keyframes, "vector2");
        return _options.BezierExportMode == BezierExportMode.BakeToSegments
            ? BakeVector2(keyframes)
            : keyframes;
    }

    /// <summary>Process a list of Color3 keyframes. Color bezier baking is not supported in MVP.</summary>
    public List<Keyframe<Color3>> ProcessColor(List<Keyframe<Color3>> keyframes)
    {
        WarnKeyframes(keyframes, "color");
        // No baking for color in MVP.
        return keyframes;
    }

    private void WarnKeyframes(List<Keyframe<float>> keyframes, string context)
    {
        foreach (var kf in keyframes)
            if (kf.Handles != null)
                WarnBezier(kf.Time, context);
    }

    private void WarnKeyframes(List<Keyframe<Vector2>> keyframes, string context)
    {
        foreach (var kf in keyframes)
            if (kf.Handles != null)
                WarnBezier(kf.Time, context);
    }

    private void WarnKeyframes(List<Keyframe<Color3>> keyframes, string context)
    {
        foreach (var kf in keyframes)
            if (kf.Handles != null)
                WarnBezier(kf.Time, context);
    }

    /// <summary>Emit a BEZIER_CURVE_NOT_EXPRESSIBLE warning for a keyframe at the given time.</summary>
    private void WarnBezier(double time, string context)
    {
        _diagnostics.Add(new Diagnostic
        {
            Code = "BEZIER_CURVE_NOT_EXPRESSIBLE",
            Message = $"Bezier curve at time {OsbFormat.FormatTime(time)} ({context}) cannot be expressed in .osb; " +
                      $"mode={_options.BezierExportMode}.",
            Severity = DiagnosticSeverity.Warning,
            Scope = "export",
        });
    }

    /// <summary>Bake Vector2 keyframe segments with bezier handles into uniform linear subdivisions.</summary>
    private List<Keyframe<Vector2>> BakeVector2(List<Keyframe<Vector2>> keyframes)
    {
        if (keyframes.Count < 2)
            return keyframes;

        var result = new List<Keyframe<Vector2>>(keyframes.Count);
        for (var i = 0; i < keyframes.Count; i++)
        {
            var current = keyframes[i];
            result.Add(current);

            if (current.Handles != null && i < keyframes.Count - 1)
            {
                var next = keyframes[i + 1];
                var baked = BakeSegmentVector2(current, next, _options.BezierBakeSegments);
                result.AddRange(baked);
            }
        }
        return result;
    }

    /// <summary>Bake float keyframe segments with bezier handles into uniform linear subdivisions.
    /// Uses the handle Y component as the value dimension.</summary>
    private List<Keyframe<float>> BakeFloat(List<Keyframe<float>> keyframes)
    {
        if (keyframes.Count < 2)
            return keyframes;

        var result = new List<Keyframe<float>>(keyframes.Count);
        for (var i = 0; i < keyframes.Count; i++)
        {
            var current = keyframes[i];
            result.Add(current);

            if (current.Handles != null && i < keyframes.Count - 1)
            {
                var next = keyframes[i + 1];
                var baked = BakeSegmentFloat(current, next, _options.BezierBakeSegments);
                result.AddRange(baked);
            }
        }
        return result;
    }

    /// <summary>Bake a single Vector2 bezier segment into intermediate keyframes (excluding endpoints).</summary>
    private static List<Keyframe<Vector2>> BakeSegmentVector2(
        Keyframe<Vector2> start, Keyframe<Vector2> end, int segments)
    {
        var result = new List<Keyframe<Vector2>>(segments - 1);
        if (segments < 2)
            return result;

        // Cubic bezier control points:
        // P0 = start.Value
        // P1 = start.Value + start.Handles.OutHandle
        // P2 = end.Value + (end.Handles?.InHandle ?? Vector2.Zero)
        // P3 = end.Value
        var p0 = start.Value;
        var outHandle = start.Handles!.OutHandle;
        var p1 = new Vector2(p0.X + outHandle.X, p0.Y + outHandle.Y);
        var inHandle = end.Handles?.InHandle ?? Vector2.Zero;
        var p2 = new Vector2(end.Value.X + inHandle.X, end.Value.Y + inHandle.Y);
        var p3 = end.Value;

        var startTime = start.Time;
        var endTime = end.Time;

        for (var i = 1; i < segments; i++)
        {
            var t = (double)i / segments;
            var time = startTime + (endTime - startTime) * t;
            var value = CubicBezier(p0, p1, p2, p3, (float)t);
            result.Add(new Keyframe<Vector2>
            {
                Time = time,
                Value = value,
                Easing = OsbEasing.None,
                Handles = null,
            });
        }
        return result;
    }

    /// <summary>Bake a single float bezier segment into intermediate keyframes (excluding endpoints).</summary>
    private static List<Keyframe<float>> BakeSegmentFloat(
        Keyframe<float> start, Keyframe<float> end, int segments)
    {
        var result = new List<Keyframe<float>>(segments - 1);
        if (segments < 2)
            return result;

        // Treat the float value as a 1D cubic bezier using handle Y components.
        var p0 = start.Value;
        var p1 = start.Value + start.Handles!.OutHandle.Y;
        var p2 = end.Value + (end.Handles?.InHandle.Y ?? 0f);
        var p3 = end.Value;

        var startTime = start.Time;
        var endTime = end.Time;

        for (var i = 1; i < segments; i++)
        {
            var t = (double)i / segments;
            var time = startTime + (endTime - startTime) * t;
            var value = CubicBezierFloat(p0, p1, p2, p3, (float)t);
            result.Add(new Keyframe<float>
            {
                Time = time,
                Value = value,
                Easing = OsbEasing.None,
                Handles = null,
            });
        }
        return result;
    }

    private static Vector2 CubicBezier(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
    {
        var u = 1f - t;
        var tt = t * t;
        var uu = u * u;
        var uuu = uu * u;
        var ttt = tt * t;

        // B(t) = (1-t)^3 * P0 + 3*(1-t)^2*t * P1 + 3*(1-t)*t^2 * P2 + t^3 * P3
        var x = uuu * p0.X + 3 * uu * t * p1.X + 3 * u * tt * p2.X + ttt * p3.X;
        var y = uuu * p0.Y + 3 * uu * t * p1.Y + 3 * u * tt * p2.Y + ttt * p3.Y;
        return new Vector2(x, y);
    }

    private static float CubicBezierFloat(float p0, float p1, float p2, float p3, float t)
    {
        var u = 1f - t;
        var tt = t * t;
        var uu = u * u;
        var uuu = uu * u;
        var ttt = tt * t;

        return uuu * p0 + 3 * uu * t * p1 + 3 * u * tt * p2 + ttt * p3;
    }
}
