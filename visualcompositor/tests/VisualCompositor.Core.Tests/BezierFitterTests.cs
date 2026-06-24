using VisualCompositor.Core.Model;
using VisualCompositor.Core.Primitives;
using VisualCompositor.Osb.Export;

namespace VisualCompositor.Core.Tests;

/// <summary>Phase 10 (Task 31) tests for <see cref="BezierFitter"/>.
/// Verifies that bezier curves approximating ease-in/out are fitted correctly,
/// linear (zero handles) returns None, and extreme curves return None (no fit).</summary>
public class BezierFitterTests
{
    [Fact]
    public void FitToNearestEasing_EaseInBezier_ReturnsInEasing()
    {
        // Cubic bezier that exactly matches ease-in (t^2):
        // p0=0, p1=0 (OutHandle.Y=0), p2=1/3 (InHandle.Y=-2/3), p3=1.
        var handles = new BezierHandles
        {
            InHandle = new Vector2(-1, -2f / 3f),
            OutHandle = new Vector2(1, 0f),
        };
        var result = BezierFitter.FitToNearestEasing(handles, 0f, 1f);
        // In and InQuad are identical (t^2); the fitter returns the first match (In).
        Assert.True(result == OsbEasing.In || result == OsbEasing.InQuad,
            $"Expected In or InQuad, got {result}");
    }

    [Fact]
    public void FitToNearestEasing_EaseOutBezier_ReturnsOutEasing()
    {
        // Cubic bezier that exactly matches ease-out (1-(1-t)^2 = 2t-t^2):
        // p0=0, p1=2/3 (OutHandle.Y=2/3), p2=1 (InHandle.Y=0), p3=1.
        var handles = new BezierHandles
        {
            InHandle = new Vector2(-1, 0f),
            OutHandle = new Vector2(1, 2f / 3f),
        };
        var result = BezierFitter.FitToNearestEasing(handles, 0f, 1f);
        Assert.True(result == OsbEasing.Out || result == OsbEasing.OutQuad,
            $"Expected Out or OutQuad, got {result}");
    }

    [Fact]
    public void FitToNearestEasing_ZeroHandles_ReturnsNone()
    {
        var handles = new BezierHandles
        {
            InHandle = Vector2.Zero,
            OutHandle = Vector2.Zero,
        };
        var result = BezierFitter.FitToNearestEasing(handles, 0f, 1f);
        Assert.Equal(OsbEasing.None, result);
    }

    [Fact]
    public void FitToNearestEasing_ExtremeBezier_ReturnsNone()
    {
        // Extreme handles that create a wild curve (overshoots far beyond [0,1]).
        var handles = new BezierHandles
        {
            InHandle = new Vector2(-1, -5f),
            OutHandle = new Vector2(1, 5f),
        };
        var result = BezierFitter.FitToNearestEasing(handles, 0f, 1f);
        Assert.Equal(OsbEasing.None, result);
    }

    [Fact]
    public void FitToNearestEasing_FlatCurve_ReturnsNone()
    {
        var handles = new BezierHandles
        {
            InHandle = new Vector2(-1, 0.5f),
            OutHandle = new Vector2(1, 0.5f),
        };
        // startValue == endValue -> flat curve -> None.
        var result = BezierFitter.FitToNearestEasing(handles, 0f, 0f);
        Assert.Equal(OsbEasing.None, result);
    }
}
