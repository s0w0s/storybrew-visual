namespace VisualCompositor.Core.Primitives;

/// <summary>Canonical easing function evaluation for all <see cref="OsbEasing"/> values.
/// Used by both the .osb export compiler (bezier fitting) and the graph editor (curve sampling).
/// The Rendering layer's <c>CommandSampler</c> keeps its own implementation for sampling
/// performance reasons; this is the canonical reference for export-time evaluation.</summary>
public static class EasingFunctions
{
    private const float Pi = (float)Math.PI;
    private const float HalfPi = (float)(Math.PI / 2.0);
    private const float TwoPi = (float)(2.0 * Math.PI);
    private const float BackConstant = 1.70158f;

    /// <summary>Evaluate the easing function at parameter <paramref name="t"/> in [0,1].
    /// Behavior outside [0,1] is easing-dependent; use <see cref="EvaluateClamped"/> to clamp.</summary>
    public static float Evaluate(OsbEasing easing, float t)
    {
        switch (easing)
        {
            case OsbEasing.None: return t;
            case OsbEasing.Out: return 1f - (1f - t) * (1f - t);
            case OsbEasing.In: return t * t;
            case OsbEasing.InQuad: return t * t;
            case OsbEasing.OutQuad: return 1f - (1f - t) * (1f - t);
            case OsbEasing.InOutQuad: return t < 0.5f ? 2f * t * t : 1f - MathF.Pow(-2f * t + 2f, 2f) / 2f;
            case OsbEasing.InCubic: return t * t * t;
            case OsbEasing.OutCubic: return 1f - MathF.Pow(1f - t, 3f);
            case OsbEasing.InOutCubic: return t < 0.5f ? 4f * t * t * t : 1f - MathF.Pow(-2f * t + 2f, 3f) / 2f;
            case OsbEasing.InQuart: return t * t * t * t;
            case OsbEasing.OutQuart: return 1f - MathF.Pow(1f - t, 4f);
            case OsbEasing.InOutQuart: return t < 0.5f ? 8f * t * t * t * t : 1f - MathF.Pow(-2f * t + 2f, 4f) / 2f;
            case OsbEasing.InQuint: return t * t * t * t * t;
            case OsbEasing.OutQuint: return 1f - MathF.Pow(1f - t, 5f);
            case OsbEasing.InOutQuint: return t < 0.5f ? 16f * t * t * t * t * t : 1f - MathF.Pow(-2f * t + 2f, 5f) / 2f;
            case OsbEasing.InSine: return 1f - MathF.Cos(t * HalfPi);
            case OsbEasing.OutSine: return MathF.Sin(t * HalfPi);
            case OsbEasing.InOutSine: return -(MathF.Cos(Pi * t) - 1f) / 2f;
            case OsbEasing.InExpo: return t == 0f ? 0f : MathF.Pow(2f, 10f * t - 10f);
            case OsbEasing.OutExpo: return t == 1f ? 1f : 1f - MathF.Pow(2f, -10f * t);
            case OsbEasing.InOutExpo:
                if (t == 0f) return 0f;
                if (t == 1f) return 1f;
                return t < 0.5f
                    ? MathF.Pow(2f, 20f * t - 10f) / 2f
                    : (2f - MathF.Pow(2f, -20f * t + 10f)) / 2f;
            case OsbEasing.InCirc: return 1f - MathF.Sqrt(1f - t * t);
            case OsbEasing.OutCirc: return MathF.Sqrt(1f - (t - 1f) * (t - 1f));
            case OsbEasing.InOutCirc:
                return t < 0.5f
                    ? (1f - MathF.Sqrt(1f - (2f * t) * (2f * t))) / 2f
                    : (MathF.Sqrt(1f - (-2f * t + 2f) * (-2f * t + 2f)) + 1f) / 2f;
            case OsbEasing.InElastic: return Reverse(ElasticOut, t);
            case OsbEasing.OutElastic: return ElasticOut(t);
            case OsbEasing.OutElasticHalf: return MathF.Pow(2f, -10f * t) * MathF.Sin((0.5f * t - 0.075f) * TwoPi / 0.3f) + 1f;
            case OsbEasing.OutElasticQuarter: return MathF.Pow(2f, -10f * t) * MathF.Sin((0.25f * t - 0.075f) * TwoPi / 0.3f) + 1f;
            case OsbEasing.InOutElastic: return ToInOut(ElasticIn, t);
            case OsbEasing.InBack: return BackIn(t);
            case OsbEasing.OutBack: return Reverse(BackIn, t);
            case OsbEasing.InOutBack: return ToInOut(BackInOutFn, t);
            case OsbEasing.InBounce: return Reverse(BounceOut, t);
            case OsbEasing.OutBounce: return BounceOut(t);
            case OsbEasing.InOutBounce: return ToInOut(BounceIn, t);
            default: return t;
        }
    }

    /// <summary>Same as <see cref="Evaluate"/> but clamps <paramref name="t"/> to [0,1] first.</summary>
    public static float EvaluateClamped(OsbEasing easing, float t)
    {
        if (t < 0f) t = 0f;
        else if (t > 1f) t = 1f;
        return Evaluate(easing, t);
    }

    // ---- Helpers mirroring the common EasingFunctions patterns ----

    private static float Reverse(Func<float, float> function, float value) => 1f - function(1f - value);
    private static float ToInOut(Func<float, float> function, float value)
        => 0.5f * (value < 0.5f ? function(2f * value) : (2f - function(2f - 2f * value)));

    private static float ElasticOut(float x)
        => MathF.Pow(2f, -10f * x) * MathF.Sin((x - 0.075f) * TwoPi / 0.3f) + 1f;

    private static float ElasticIn(float x) => Reverse(ElasticOut, x);

    private static float BackIn(float x)
        => x * x * ((BackConstant + 1f) * x - BackConstant);

    private static float BackInOutFn(float x)
    {
        var c2 = BackConstant * 1.525f;
        return x * x * ((c2 + 1f) * x - c2);
    }

    private static float BounceOut(float x)
    {
        const float n = 2.75f;
        const float d = 7.5625f;
        if (x < 1f / n) return d * x * x;
        if (x < 2f / n) return d * (x -= (1.5f / n)) * x + 0.75f;
        if (x < 2.5f / n) return d * (x -= (2.25f / n)) * x + 0.9375f;
        return d * (x -= (2.625f / n)) * x + 0.984375f;
    }

    private static float BounceIn(float x) => Reverse(BounceOut, x);
}
