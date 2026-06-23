using VisualCompositor.Core.Model;
using VisualCompositor.Core.Primitives;

namespace VisualCompositor.Rendering.Worker;

/// <summary>Request for rendering a frame at a specific time.</summary>
public sealed class RenderRequest
{
    public long Revision { get; init; }
    public CompositionDocument Document { get; init; } = new();
    public double Time { get; init; }
    public RenderQuality Quality { get; init; } = RenderQuality.FullPreview;
    public List<string> SelectedLayerIds { get; init; } = new();
    public List<string> SelectedSpriteIds { get; init; } = new();
    public int Width { get; init; } = 1366;
    public int Height { get; init; } = 768;
    public CancellationToken CancellationToken { get; init; }
}
