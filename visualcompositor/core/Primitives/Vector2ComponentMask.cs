namespace VisualCompositor.Core.Primitives;

/// <summary>Bitmask indicating which Vector2 components are active for a command/keyframe.
/// Used to express MX/MY (single-component) vs M (both components) at the model level.</summary>
[Flags]
public enum Vector2ComponentMask
{
    None = 0,
    X = 1,
    Y = 2,
    Both = X | Y,
}
