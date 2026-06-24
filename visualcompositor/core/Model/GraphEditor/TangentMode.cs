namespace VisualCompositor.Core.Model.GraphEditor;

/// <summary>How a graph editor keyframe's bezier handles are computed and maintained.
/// Controls the auto-tangent behavior when handles are not explicitly edited.</summary>
public enum TangentMode
{
    /// <summary>Handles auto-computed for a smooth curve through neighbors.</summary>
    Auto,

    /// <summary>Straight lines between keyframes; no curve (zero handles).</summary>
    Linear,

    /// <summary>Manually set smooth handles (symmetric in/out).</summary>
    Smooth,

    /// <summary>Independently controlled in/out handles (asymmetric).</summary>
    Broken,

    /// <summary>Step function: hold the value until the next keyframe.</summary>
    Stepped,
}
