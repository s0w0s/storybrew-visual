using System.Globalization;
using VisualCompositor.Core.Primitives;

namespace VisualCompositor.Osb.Export;

/// <summary>Number and value formatting helpers for .osb text emission.
/// All formatting uses <see cref="CultureInfo.InvariantCulture"/> for deterministic output.</summary>
internal static class OsbFormat
{
    /// <summary>Format a time value as an integer millisecond string (rounded to nearest).</summary>
    public static string FormatTime(double time)
    {
        var rounded = Math.Round(time, MidpointRounding.AwayFromZero);
        return ((long)rounded).ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>Format an easing enum as its integer value.</summary>
    public static string FormatEasing(OsbEasing easing) =>
        ((int)easing).ToString(CultureInfo.InvariantCulture);

    /// <summary>Format a float without trailing zeros but with enough precision for round-trip.</summary>
    public static string FormatFloat(float value)
    {
        // Round to 6 decimal places to suppress floating-point noise, then strip trailing zeros.
        var rounded = Math.Round(value, 6, MidpointRounding.ToEven);
        if (rounded == 0)
            return "0";
        var s = rounded.ToString("0.######", CultureInfo.InvariantCulture);
        return s;
    }

    /// <summary>Format a double without trailing zeros but with enough precision for round-trip.</summary>
    public static string FormatDouble(double value)
    {
        var rounded = Math.Round(value, 6, MidpointRounding.ToEven);
        if (rounded == 0)
            return "0";
        return rounded.ToString("0.######", CultureInfo.InvariantCulture);
    }

    /// <summary>Format a color component (float in [0,1]) as an integer in [0,255].</summary>
    public static string FormatColorComponent(float value)
    {
        var intValue = (int)Math.Round(value * 255f, MidpointRounding.AwayFromZero);
        if (intValue < 0) intValue = 0;
        if (intValue > 255) intValue = 255;
        return intValue.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>Format a Vector2 as "x,y".</summary>
    public static string FormatVector2(Vector2 v) =>
        FormatFloat(v.X) + "," + FormatFloat(v.Y);

    /// <summary>Format a Color3 as "r,g,b" with integer components in [0,255].</summary>
    public static string FormatColor(Color3 c) =>
        FormatColorComponent(c.R) + "," + FormatColorComponent(c.G) + "," + FormatColorComponent(c.B);

    /// <summary>Convert an <see cref="OsbLayer"/> to its .osb string name.</summary>
    public static string LayerName(OsbLayer layer) => layer.ToString();

    /// <summary>Convert an <see cref="OsbOrigin"/> to its .osb string name.</summary>
    public static string OriginName(OsbOrigin origin) => origin.ToString();

    /// <summary>Convert an <see cref="OsbLoopType"/> to its .osb string name.</summary>
    public static string LoopTypeName(OsbLoopType loopType) => loopType.ToString();

    /// <summary>Convert a <see cref="ParameterType"/> to its .osb single-letter code.
    /// Returns empty string for <see cref="ParameterType.None"/>.</summary>
    public static string ParameterLetter(ParameterType parameter) => parameter switch
    {
        ParameterType.FlipHorizontal => "H",
        ParameterType.FlipVertical => "V",
        ParameterType.AdditiveBlending => "A",
        _ => string.Empty,
    };
}
