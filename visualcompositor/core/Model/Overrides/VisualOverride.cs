using VisualCompositor.Core.Primitives;

namespace VisualCompositor.Core.Model.Overrides;

/// <summary>A non-destructive visual override applied to a layer or sprite during editing/preview.
/// Does NOT modify underlying commands — only affects display.</summary>
public sealed class VisualOverride
{
    /// <summary>Unique identifier for this override.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Whether the override targets a layer or a sprite.</summary>
    public OverrideTargetType TargetType { get; set; }

    /// <summary>The id of the target layer or sprite.</summary>
    public string TargetId { get; set; } = string.Empty;

    /// <summary>Visibility override. Null = no override.</summary>
    public bool? Visible { get; set; }

    /// <summary>Opacity multiplier override (clamped 0-1). Null = no override.</summary>
    public float? OpacityMultiplier { get; set; }

    /// <summary>Position offset override. Null = no override.</summary>
    public Vector2? PositionOffset { get; set; }

    /// <summary>Scale multiplier override. Null = no override.</summary>
    public Vector2? ScaleMultiplier { get; set; }

    /// <summary>Rotation offset override (radians). Null = no override.</summary>
    public float? RotationOffset { get; set; }

    /// <summary>Tint color override. Null = no override.</summary>
    public Color3? Tint { get; set; }

    public VisualOverride Clone() => new()
    {
        Id = Id,
        TargetType = TargetType,
        TargetId = TargetId,
        Visible = Visible,
        OpacityMultiplier = OpacityMultiplier.HasValue
            ? Math.Clamp(OpacityMultiplier.Value, 0f, 1f)
            : null,
        PositionOffset = PositionOffset,
        ScaleMultiplier = ScaleMultiplier,
        RotationOffset = RotationOffset,
        Tint = Tint,
    };
}
