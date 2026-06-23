using VisualCompositor.Core.Model;
using VisualCompositor.Core.Primitives;

namespace VisualCompositor.Rendering.Worker;

/// <summary>A render request. The worker processes these asynchronously.</summary>
public sealed class RenderRequest
{
    public long Revision { get; init; }
    public CompositionDocument Document { get; init; } = new();
    public double Time { get; init; }
    public RenderQuality Quality { get; init; } = RenderQuality.FullPreview;
    public List<string> SelectedSpriteIds { get; init; } = new();
    public int MaxRenderedLayers { get; init; } = 50;
    public CancellationToken CancellationToken { get; init; }
}

public sealed class RenderResult
{
    public long Revision { get; init; }
    public double Time { get; init; }
    public Backend.IRenderResult? Frame { get; init; }
    public List<RenderDiagnostic> Diagnostics { get; init; } = new();
    public TimeSpan RenderTime { get; init; }
}

public sealed class RenderDiagnostic
{
    public string Code { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string? SpriteId { get; init; }
}
