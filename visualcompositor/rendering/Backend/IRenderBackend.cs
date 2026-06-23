using VisualCompositor.Core.Primitives;

namespace VisualCompositor.Rendering.Backend;

/// <summary>Abstract rendering backend. The actual implementation uses brewlib on Windows.</summary>
public interface IRenderBackend
{
    /// <summary>Load a texture by path. Returns null if missing (placeholder will be rendered).</summary>
    Task<ITexture?> LoadTextureAsync(string texturePath);

    /// <summary>Render a quad with the given transform.</summary>
    void DrawQuad(ITexture? texture, Vector2 position, Vector2 scale, float rotation, float opacity, Color3 color, bool additive);

    /// <summary>Begin a render frame at the given resolution scale.</summary>
    void BeginFrame(float resolutionScale);

    /// <summary>End the render frame and return the rendered result.</summary>
    IRenderResult EndFrame();
}

public interface ITexture
{
    int Width { get; }
    int Height { get; }
    bool IsPlaceholder { get; }
}

public interface IRenderResult
{
    byte[]? PixelData { get; }
    int Width { get; }
    int Height { get; }
}
