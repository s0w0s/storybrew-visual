using VisualCompositor.Core.Primitives;

namespace VisualCompositor.Core.Model.GraphEditor;

/// <summary>Computes automatic bezier handles for graph editor keyframes.
/// Handle magnitude uses 1/3 of the distance to the neighbor as a heuristic (standard
/// Catmull-Rom-to-Bezier conversion factor).</summary>
public static class AutoTangent
{
    /// <summary>Compute smooth auto-tangent handles for a keyframe based on its neighbors.
    /// Returns null if either neighbor is missing (endpoint keyframe).</summary>
    public static BezierHandles? ComputeAutoTangent(
        double prevTime, float prevValue,
        double time, float value,
        double nextTime, float nextValue)
    {
        // Need both neighbors for an auto-tangent.
        if (double.IsNaN(prevTime) || double.IsNaN(nextTime))
            return null;

        var dtPrev = time - prevTime;
        var dtNext = nextTime - time;
        if (dtPrev <= 0 || dtNext <= 0) return null;

        // Slope through neighbors (Catmull-Rom tangent), scaled to each side.
        var slope = (nextValue - prevValue) / (float)((nextTime - prevTime));
        var outMag = (float)(dtNext / 3.0);
        var inMag = (float)(dtPrev / 3.0);

        return new BezierHandles
        {
            // InHandle points backward in time (negative X), OutHandle forward (positive X).
            InHandle = new Vector2(-inMag, -slope * inMag),
            OutHandle = new Vector2(outMag, slope * outMag),
        };
    }

    /// <summary>Compute symmetric smooth handles (in and out mirror each other).
    /// Returns null if either neighbor is missing.</summary>
    public static BezierHandles? ComputeSmoothTangent(
        double prevTime, float prevValue,
        double time, float value,
        double nextTime, float nextValue)
    {
        if (double.IsNaN(prevTime) || double.IsNaN(nextTime))
            return null;

        var dtPrev = time - prevTime;
        var dtNext = nextTime - time;
        if (dtPrev <= 0 || dtNext <= 0) return null;

        // Symmetric: average the in/out magnitudes so both sides match.
        var avgMag = (float)((dtPrev + dtNext) / 6.0);
        var slope = (nextValue - prevValue) / (float)((nextTime - prevTime));

        return new BezierHandles
        {
            InHandle = new Vector2(-avgMag, -slope * avgMag),
            OutHandle = new Vector2(avgMag, slope * avgMag),
        };
    }

    /// <summary>Returns zero handles (straight line / linear interpolation).</summary>
    public static BezierHandles MakeLinear() => new()
    {
        InHandle = Vector2.Zero,
        OutHandle = Vector2.Zero,
    };

    /// <summary>Returns handles that create a step function: OutHandle far right (hold value),
    /// InHandle far left. The sampler treats a stepped segment by holding StartValue until EndTime.</summary>
    public static BezierHandles MakeStepped() => new()
    {
        // X components large enough to push the curve flat; Y zero so value doesn't change.
        InHandle = new Vector2(-1000f, 0f),
        OutHandle = new Vector2(1000f, 0f),
    };
}
