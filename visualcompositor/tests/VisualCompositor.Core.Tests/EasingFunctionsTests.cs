using VisualCompositor.Core.Primitives;

namespace VisualCompositor.Core.Tests;

/// <summary>Phase 10 (Task 31) tests for the canonical <see cref="EasingFunctions"/> evaluator.
/// Verifies boundary conditions, known values, clamping, and monotonicity for "In" easings.</summary>
public class EasingFunctionsTests
{
    private const float Tolerance = 1e-5f;

    [Fact]
    public void None_IsLinear()
    {
        Assert.Equal(0.0f, EasingFunctions.Evaluate(OsbEasing.None, 0f), Tolerance);
        Assert.Equal(0.25f, EasingFunctions.Evaluate(OsbEasing.None, 0.25f), Tolerance);
        Assert.Equal(0.5f, EasingFunctions.Evaluate(OsbEasing.None, 0.5f), Tolerance);
        Assert.Equal(0.75f, EasingFunctions.Evaluate(OsbEasing.None, 0.75f), Tolerance);
        Assert.Equal(1.0f, EasingFunctions.Evaluate(OsbEasing.None, 1f), Tolerance);
    }

    [Fact]
    public void In_QuadAtHalf_IsQuarter()
    {
        Assert.Equal(0.25f, EasingFunctions.Evaluate(OsbEasing.In, 0.5f), Tolerance);
    }

    [Fact]
    public void Out_QuadAtHalf_IsThreeQuarters()
    {
        Assert.Equal(0.75f, EasingFunctions.Evaluate(OsbEasing.Out, 0.5f), Tolerance);
    }

    [Fact]
    public void InCubic_AtHalf_IsOneEighth()
    {
        Assert.Equal(0.125f, EasingFunctions.Evaluate(OsbEasing.InCubic, 0.5f), Tolerance);
    }

    [Fact]
    public void OutCubic_AtHalf_IsSevenEighths()
    {
        Assert.Equal(0.875f, EasingFunctions.Evaluate(OsbEasing.OutCubic, 0.5f), Tolerance);
    }

    [Fact]
    public void InQuad_AtHalf_IsQuarter()
    {
        Assert.Equal(0.25f, EasingFunctions.Evaluate(OsbEasing.InQuad, 0.5f), Tolerance);
    }

    [Fact]
    public void OutQuad_AtHalf_IsThreeQuarters()
    {
        Assert.Equal(0.75f, EasingFunctions.Evaluate(OsbEasing.OutQuad, 0.5f), Tolerance);
    }

    [Fact]
    public void InSine_Boundaries_AreZeroAndOne()
    {
        Assert.Equal(0f, EasingFunctions.Evaluate(OsbEasing.InSine, 0f), Tolerance);
        Assert.Equal(1f, EasingFunctions.Evaluate(OsbEasing.InSine, 1f), Tolerance);
    }

    [Fact]
    public void OutBounce_Boundaries_AreZeroAndOne()
    {
        Assert.Equal(0f, EasingFunctions.Evaluate(OsbEasing.OutBounce, 0f), Tolerance);
        Assert.Equal(1f, EasingFunctions.Evaluate(OsbEasing.OutBounce, 1f), Tolerance);
    }

    [Fact]
    public void EvaluateClamped_ClampsTOutsideRange()
    {
        // t < 0 clamps to 0 -> result is easing(0) = 0.
        Assert.Equal(0f, EasingFunctions.EvaluateClamped(OsbEasing.InQuad, -0.5f), Tolerance);
        // t > 1 clamps to 1 -> result is easing(1) = 1.
        Assert.Equal(1f, EasingFunctions.EvaluateClamped(OsbEasing.InQuad, 1.5f), Tolerance);
    }

    [Fact]
    public void AllEasings_Boundaries_AreZeroAndOne()
    {
        // Elastic easings have a small oscillation at boundaries (~0.0005) due to the
        // elastic formula; use a looser tolerance to accommodate this known property.
        const float boundaryTolerance = 1e-3f;
        foreach (var easing in Enum.GetValues<OsbEasing>())
        {
            Assert.Equal(0f, EasingFunctions.Evaluate(easing, 0f), boundaryTolerance);
            Assert.Equal(1f, EasingFunctions.Evaluate(easing, 1f), boundaryTolerance);
        }
    }

    [Fact]
    public void InEasings_AreMonotonicallyIncreasing()
    {
        // Only the simple power/sine/expo/circ "In" easings are strictly monotonic.
        // InBack, InBounce, InElastic intentionally overshoot/oscillate and are excluded.
        var inEasings = new[]
        {
            OsbEasing.In, OsbEasing.InQuad, OsbEasing.InCubic, OsbEasing.InQuart,
            OsbEasing.InQuint, OsbEasing.InSine, OsbEasing.InExpo, OsbEasing.InCirc,
        };
        const int steps = 64;
        foreach (var easing in inEasings)
        {
            var prev = EasingFunctions.Evaluate(easing, 0f);
            for (var i = 1; i <= steps; i++)
            {
                var t = (float)i / steps;
                var curr = EasingFunctions.Evaluate(easing, t);
                Assert.True(curr >= prev - 1e-6f,
                    $"{easing} not monotonic at t={t}: prev={prev}, curr={curr}");
                prev = curr;
            }
        }
    }

    [Fact]
    public void InOutQuad_Midpoint_IsHalf()
    {
        Assert.Equal(0.5f, EasingFunctions.Evaluate(OsbEasing.InOutQuad, 0.5f), Tolerance);
    }

    [Fact]
    public void InOutCubic_Midpoint_IsHalf()
    {
        Assert.Equal(0.5f, EasingFunctions.Evaluate(OsbEasing.InOutCubic, 0.5f), Tolerance);
    }

    [Fact]
    public void InOutSine_Midpoint_IsHalf()
    {
        Assert.Equal(0.5f, EasingFunctions.Evaluate(OsbEasing.InOutSine, 0.5f), Tolerance);
    }
}
