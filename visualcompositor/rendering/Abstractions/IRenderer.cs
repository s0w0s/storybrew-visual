using VisualCompositor.Core.Primitives;

namespace VisualCompositor.Rendering.Abstractions;

/// <summary>Abstract renderer interface. brewlib's QuadRendererBuffered would implement this on Windows.</summary>
public interface IRenderer
{
    void BeginFrame(int width, int height);
    void DrawSprite(string texturePath, Vector2 position, Vector2 scale, float rotation, float opacity, Color3 color, bool additive, bool flipH, bool flipV);
    void DrawPlaceholder(Vector2 position, Vector2 size, string label);
    void EndFrame();
}

/// <summary>Texture provider. brewlib's TextureContainer would implement this.</summary>
public interface ITextureProvider
{
    bool HasTexture(string texturePath);
    Vector2 GetTextureSize(string texturePath);
}
