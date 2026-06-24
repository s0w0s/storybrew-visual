using VisualCompositor.Core.Primitives;

namespace VisualCompositor.Rendering.Worker;

/// <summary>Result of a render request. UI only accepts if Revision matches current state.</summary>
public sealed class RenderResult
{
    public long Revision { get; init; }
    public double Time { get; init; }
    public bool Succeeded { get; init; }
    public string? ErrorMessage { get; init; }
    public List<RenderedSprite> Sprites { get; init; } = new();
    public List<RenderedPlaceholder> Placeholders { get; init; } = new();
}

public sealed class RenderedSprite
{
    public string TexturePath { get; init; } = string.Empty;
    public Vector2 Position { get; init; }
    public Vector2 Scale { get; init; }
    public float Rotation { get; init; }
    public float Opacity { get; init; }
    public Color3 Color { get; init; }
    public bool Additive { get; init; }
    public bool FlipH { get; init; }
    public bool FlipV { get; init; }
}

public sealed class RenderedPlaceholder
{
    public Vector2 Position { get; init; }
    public Vector2 Size { get; init; }
    public string Label { get; init; } = string.Empty;
}
