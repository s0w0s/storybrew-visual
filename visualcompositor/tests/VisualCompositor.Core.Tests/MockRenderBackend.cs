using VisualCompositor.Core.Primitives;
using VisualCompositor.Rendering.Backend;

namespace VisualCompositor.Core.Tests;

/// <summary>Mock render backend for testing. Records calls and returns configurable results.</summary>
public sealed class MockRenderBackend : IRenderBackend
{
    public int BeginFrameCallCount { get; private set; }
    public float LastResolutionScale { get; private set; }
    public List<float> ResolutionScales { get; } = new();
    public int EndFrameCallCount { get; private set; }
    public List<DrawQuadCall> DrawQuadCalls { get; } = new();
    public List<string> LoadTextureCalls { get; } = new();

    /// <summary>Set to return a placeholder texture for any LoadTextureAsync call.</summary>
    public bool ReturnPlaceholderTexture { get; set; } = true;

    public Task<ITexture?> LoadTextureAsync(string texturePath)
    {
        LoadTextureCalls.Add(texturePath);
        if (ReturnPlaceholderTexture)
        {
            return Task.FromResult<ITexture?>(new MockTexture(64, 64, true));
        }
        return Task.FromResult<ITexture?>(null);
    }

    public void DrawQuad(ITexture? texture, Vector2 position, Vector2 scale, float rotation, float opacity, Color3 color, bool additive)
    {
        DrawQuadCalls.Add(new DrawQuadCall(position, scale, rotation, opacity, color, additive));
    }

    public void BeginFrame(float resolutionScale)
    {
        BeginFrameCallCount++;
        LastResolutionScale = resolutionScale;
        ResolutionScales.Add(resolutionScale);
    }

    public IRenderResult EndFrame()
    {
        EndFrameCallCount++;
        return new MockRenderResult(640, 480);
    }
}

public sealed record DrawQuadCall(Vector2 Position, Vector2 Scale, float Rotation, float Opacity, Color3 Color, bool Additive);

public sealed class MockTexture : ITexture
{
    public int Width { get; }
    public int Height { get; }
    public bool IsPlaceholder { get; }

    public MockTexture(int width, int height, bool isPlaceholder)
    {
        Width = width;
        Height = height;
        IsPlaceholder = isPlaceholder;
    }
}

public sealed class MockRenderResult : IRenderResult
{
    public byte[]? PixelData { get; }
    public int Width { get; }
    public int Height { get; }

    public MockRenderResult(int width, int height)
    {
        Width = width;
        Height = height;
        PixelData = new byte[width * height * 4];
    }
}
