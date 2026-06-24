using VisualCompositor.Core.Model;
using VisualCompositor.Core.Primitives;

namespace VisualCompositor.Osb.Export;

/// <summary>Fits a 1D bezier curve (defined by <see cref="BezierHandles"/> and endpoint values)
/// to the nearest osu easing function by sampling both at uniform parameter values and picking
/// the easing with the lowest sum-of-squared-differences. Used by the
/// <see cref="BezierExportMode.FitNearestOsuEasing"/> export mode.</summary>
public static class BezierFitter
{
    /// <summary>Default sampling resolution (32 samples across t in [0,1]).</summary>
    public const int DefaultSampleCount = 32;

    /// <summary>SSD threshold above which no easing is considered a good fit. The bezier curve is
    /// normalized to [0,1] before comparison, so this threshold is in normalized space.
    /// Empirically, a good fit has SSD &lt; 0.05; anything above 0.5 is a poor fit.</summary>
    public const float DefaultFitThreshold = 0.5f;

    /// <summary>Fit the bezier curve to the nearest osu easing.
    /// The bezier is evaluated as a 1D cubic curve using handle Y components:
    /// <c>CubicBezier(startValue, startValue+outHandle.Y, endValue+inHandle.Y, endValue, t)</c>.
    /// Returns <see cref="OsbEasing.None"/> if no easing fits within the threshold (caller should warn).</summary>
    public static OsbEasing FitToNearestEasing(
        BezierHandles handles, float startValue, float endValue,
        int sampleCount = DefaultSampleCount, float threshold = DefaultFitThreshold)
    {
        // Zero handles (no curvature intent) -> linear (None). Note: the cubic bezier formula
        // with zero handles produces smoothstep, but the user intent is "no curve" = linear.
        if (MathF.Abs(handles.OutHandle.Y) < 1e-6f && MathF.Abs(handles.InHandle.Y) < 1e-6f)
            return OsbEasing.None;

        // Flat curve (startValue == endValue) -> None (linear / constant).
        var range = endValue - startValue;
        if (MathF.Abs(range) < 1e-6f)
            return OsbEasing.None;

        // Sample the bezier curve at uniform t values, normalized to [0,1] for comparison.
        var bezierSamples = SampleBezier(handles, startValue, endValue, sampleCount);

        // Normalize bezier samples so that startValue -> 0, endValue -> 1.
        for (var i = 0; i < bezierSamples.Length; i++)
            bezierSamples[i] = (bezierSamples[i] - startValue) / range;

        OsbEasing bestEasing = OsbEasing.None;
        var bestSsd = float.MaxValue;

        foreach (var easing in Enum.GetValues<OsbEasing>())
        {
            var ssd = ComputeSsd(easing, bezierSamples, sampleCount);
            if (ssd < bestSsd)
            {
                bestSsd = ssd;
                bestEasing = easing;
            }
        }

        // If the best fit is still poor, return None so the caller can warn.
        if (bestSsd > threshold)
            return OsbEasing.None;

        return bestEasing;
    }

    private static float[] SampleBezier(BezierHandles handles, float startValue, float endValue, int sampleCount)
    {
        var samples = new float[sampleCount];
        var p0 = startValue;
        var p1 = startValue + handles.OutHandle.Y;
        var p2 = endValue + (handles.InHandle.Y);
        var p3 = endValue;

        for (var i = 0; i < sampleCount; i++)
        {
            var t = (float)i / (sampleCount - 1);
            samples[i] = CubicBezier1D(p0, p1, p2, p3, t);
        }
        return samples;
    }

    private static float ComputeSsd(OsbEasing easing, float[] bezierSamples, int sampleCount)
    {
        var ssd = 0f;
        for (var i = 0; i < sampleCount; i++)
        {
            var t = (float)i / (sampleCount - 1);
            var eased = EasingFunctions.Evaluate(easing, t);
            var diff = bezierSamples[i] - eased;
            ssd += diff * diff;
        }
        return ssd / sampleCount;
    }

    private static float CubicBezier1D(float p0, float p1, float p2, float p3, float t)
    {
        var u = 1f - t;
        var tt = t * t;
        var uu = u * u;
        return uu * u * p0 + 3f * uu * t * p1 + 3f * u * tt * p2 + tt * t * p3;
    }
}
