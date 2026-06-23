using VisualCompositor.Core.Primitives;

namespace VisualCompositor.Core.Model;

/// <summary>A property track holding keyframes for a specific property.
/// For Vector2 properties (Position, Scale), ComponentMask indicates which components are active.</summary>
public sealed class PropertyTrack
{
    /// <summary>Property name: "Position", "Scale", "Rotation", "Opacity", "Color".</summary>
    public string PropertyName { get; set; } = string.Empty;

    /// <summary>Component mask for Vector2 properties. None for scalar properties.</summary>
    public Vector2ComponentMask ComponentMask { get; set; } = Vector2ComponentMask.Both;

    /// <summary>Keyframes for float values (Rotation, Opacity, or single component of Vector2).</summary>
    public List<Keyframe<float>> FloatKeyframes { get; set; } = new();

    /// <summary>Keyframes for Vector2 values (Position, Scale).</summary>
    public List<Keyframe<Vector2>> Vector2Keyframes { get; set; } = new();

    /// <summary>Keyframes for Color3 values (Color).</summary>
    public List<Keyframe<Color3>> ColorKeyframes { get; set; } = new();

    /// <summary>The value type of this track: "Float", "Vector2", or "Color".</summary>
    public string ValueType { get; set; } = "Float";

    public PropertyTrack Clone() => new()
    {
        PropertyName = PropertyName,
        ComponentMask = ComponentMask,
        FloatKeyframes = FloatKeyframes.ConvertAll(k => k.Clone()),
        Vector2Keyframes = Vector2Keyframes.ConvertAll(k => k.Clone()),
        ColorKeyframes = ColorKeyframes.ConvertAll(k => k.Clone()),
        ValueType = ValueType,
    };
}
