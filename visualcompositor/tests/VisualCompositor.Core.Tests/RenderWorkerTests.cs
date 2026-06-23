using VisualCompositor.Core.Model;
using VisualCompositor.Core.Primitives;
using VisualCompositor.Rendering.Abstractions;
using VisualCompositor.Rendering.Worker;

namespace VisualCompositor.Core.Tests;

/// <summary>Mock IRenderer that records calls for testing.</summary>
internal sealed class MockRenderer : IRenderer
{
    public int BeginFrameCallCount { get; private set; }
    public (int width, int height) LastFrameSize { get; private set; }
    public int EndFrameCallCount { get; private set; }
    public List<MockSpriteDraw> SpriteDraws { get; } = new();
    public List<MockPlaceholderDraw> PlaceholderDraws { get; } = new();

    public void BeginFrame(int width, int height)
    {
        BeginFrameCallCount++;
        LastFrameSize = (width, height);
    }

    public void DrawSprite(string texturePath, Vector2 position, Vector2 scale, float rotation, float opacity, Color3 color, bool additive, bool flipH, bool flipV)
    {
        SpriteDraws.Add(new MockSpriteDraw(texturePath, position, scale, rotation, opacity, color, additive, flipH, flipV));
    }

    public void DrawPlaceholder(Vector2 position, Vector2 size, string label)
    {
        PlaceholderDraws.Add(new MockPlaceholderDraw(position, size, label));
    }

    public void EndFrame()
    {
        EndFrameCallCount++;
    }
}

internal sealed record MockSpriteDraw(string TexturePath, Vector2 Position, Vector2 Scale, float Rotation, float Opacity, Color3 Color, bool Additive, bool FlipH, bool FlipV);

internal sealed record MockPlaceholderDraw(Vector2 Position, Vector2 Size, string Label);

/// <summary>Mock IRenderer that introduces a delay to simulate slow rendering.</summary>
internal sealed class SlowMockRenderer : IRenderer
{
    private readonly TimeSpan _delay;

    public SlowMockRenderer(TimeSpan delay) => _delay = delay;

    public void BeginFrame(int width, int height) => Thread.Sleep(_delay);
    public void DrawSprite(string texturePath, Vector2 position, Vector2 scale, float rotation, float opacity, Color3 color, bool additive, bool flipH, bool flipV) { }
    public void DrawPlaceholder(Vector2 position, Vector2 size, string label) { }
    public void EndFrame() { }
}

/// <summary>Mock ITextureProvider that can be configured to report textures as missing.</summary>
internal sealed class MockTextureProvider : ITextureProvider
{
    private readonly HashSet<string> _missingPaths = new();

    public void MarkMissing(string path) => _missingPaths.Add(path);

    public bool HasTexture(string texturePath) => !_missingPaths.Contains(texturePath);

    public Vector2 GetTextureSize(string texturePath) => new(64, 64);
}

public class RenderWorkerTests
{
    private static CompositionDocument BuildDocumentWithSprites(int count, OsbLayer osbLayer = OsbLayer.Foreground)
    {
        var doc = new CompositionDocument();
        var layer = new Layer { Id = "layer_0", Name = "L", OsbLayer = osbLayer };
        for (int i = 0; i < count; i++)
        {
            layer.Sprites.Add(new SpriteDeclaration
            {
                Id = $"spr_{i:D3}",
                LayerId = "layer_0",
                TexturePath = $"sprite_{i}.png",
                InitialPosition = new Vector2(i * 10, 0),
            });
        }
        doc.Layers.Add(layer);
        return doc;
    }

    private static async Task WaitForAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
                return;
            await Task.Delay(10);
        }
    }

    [Fact]
    public async Task RenderWorker_SubmitsAndReceivesResultWithMatchingRevision()
    {
        var doc = BuildDocumentWithSprites(1);
        var worker = new RenderWorker();
        RenderResult? capturedResult = null;
        worker.ResultAvailable += r => capturedResult = r;

        try
        {
            worker.Submit(new RenderRequest
            {
                Revision = 42,
                Document = doc,
                Time = 0,
                SelectedSpriteIds = new List<string> { "spr_000" },
            });

            await WaitForAsync(() => capturedResult != null, TimeSpan.FromSeconds(2));

            Assert.NotNull(capturedResult);
            Assert.Equal(42, capturedResult!.Revision);
            Assert.True(capturedResult.Succeeded);
            Assert.Single(capturedResult.Sprites);
            Assert.Equal("sprite_0.png", capturedResult.Sprites[0].TexturePath);
        }
        finally
        {
            worker.Dispose();
        }
    }

    [Fact]
    public async Task RenderWorker_DiscardsStaleRequests_OnlyLatestResultMatters()
    {
        var doc = BuildDocumentWithSprites(1);
        // Use a slow renderer so requests queue up and draining is observable.
        var renderer = new SlowMockRenderer(TimeSpan.FromMilliseconds(50));
        var worker = new RenderWorker(renderer, null);
        var results = new System.Collections.Concurrent.ConcurrentBag<RenderResult>();
        worker.ResultAvailable += r => results.Add(r);

        try
        {
            // Submit multiple requests quickly; the channel is drained on each Submit,
            // so only the latest pending request should survive in the channel.
            for (int i = 0; i < 10; i++)
            {
                worker.Submit(new RenderRequest
                {
                    Revision = i,
                    Document = doc,
                    Time = i * 100,
                    SelectedSpriteIds = new List<string> { "spr_000" },
                });
            }

            // Wait for at least one result
            await WaitForAsync(() => results.Count > 0, TimeSpan.FromSeconds(5));

            // Give a brief moment for any in-flight to finish
            await Task.Delay(300);

            // With the slow renderer, the first request is being processed while the
            // remaining 9 are submitted. Each Submit drains the channel, so at most
            // a couple requests should have been processed (the in-flight one + the latest).
            Assert.True(results.Count <= 3, $"Expected at most 3 results, got {results.Count}");
            // At least one result must have been produced
            Assert.NotEmpty(results);
            // All results should have valid revisions
            Assert.All(results, r => Assert.InRange(r.Revision, 0, 9));
        }
        finally
        {
            worker.Dispose();
        }
    }

    [Fact]
    public async Task RenderWorker_QualityPresets_AffectResolution()
    {
        var doc = BuildDocumentWithSprites(1);
        var renderer = new MockRenderer();
        var worker = new RenderWorker(renderer, null);
        RenderResult? capturedResult = null;
        worker.ResultAvailable += r => capturedResult = r;

        try
        {
            worker.Submit(new RenderRequest
            {
                Revision = 1,
                Document = doc,
                Time = 0,
                Quality = RenderQuality.FastScrub,
                Width = 1366,
                Height = 768,
                SelectedSpriteIds = new List<string> { "spr_000" },
            });

            await WaitForAsync(() => capturedResult != null, TimeSpan.FromSeconds(2));

            Assert.NotNull(capturedResult);
            // FastScrub => quarter resolution
            Assert.Equal((1366 / 4, 768 / 4), renderer.LastFrameSize);
        }
        finally
        {
            worker.Dispose();
        }
    }

    [Fact]
    public async Task RenderResult_IncludesPlaceholderForMissingTexture()
    {
        var doc = BuildDocumentWithSprites(1);
        var textureProvider = new MockTextureProvider();
        textureProvider.MarkMissing("sprite_0.png");

        var worker = new RenderWorker(renderer: null, textureProvider);
        RenderResult? capturedResult = null;
        worker.ResultAvailable += r => capturedResult = r;

        try
        {
            worker.Submit(new RenderRequest
            {
                Revision = 1,
                Document = doc,
                Time = 0,
                SelectedSpriteIds = new List<string> { "spr_000" },
            });

            await WaitForAsync(() => capturedResult != null, TimeSpan.FromSeconds(2));

            Assert.NotNull(capturedResult);
            Assert.True(capturedResult!.Succeeded);
            Assert.Empty(capturedResult.Sprites);
            Assert.Single(capturedResult.Placeholders);
            Assert.Equal("sprite_0.png", capturedResult.Placeholders[0].Label);
        }
        finally
        {
            worker.Dispose();
        }
    }

    [Fact]
    public async Task RenderWorker_DrawsToRendererWhenProvided()
    {
        var doc = BuildDocumentWithSprites(2);
        var renderer = new MockRenderer();
        var worker = new RenderWorker(renderer, null);
        RenderResult? capturedResult = null;
        worker.ResultAvailable += r => capturedResult = r;

        try
        {
            worker.Submit(new RenderRequest
            {
                Revision = 1,
                Document = doc,
                Time = 0,
                SelectedSpriteIds = new List<string> { "spr_000", "spr_001" },
            });

            await WaitForAsync(() => capturedResult != null, TimeSpan.FromSeconds(2));

            Assert.NotNull(capturedResult);
            Assert.Equal(1, renderer.BeginFrameCallCount);
            Assert.Equal(1, renderer.EndFrameCallCount);
            Assert.Equal(2, renderer.SpriteDraws.Count);
        }
        finally
        {
            worker.Dispose();
        }
    }

    [Fact]
    public void SelectedAndContextSelector_SelectsSelectedSpritesFirst()
    {
        var doc = BuildDocumentWithSprites(5);
        var selector = new SelectedAndContextSelector(50);

        var result = selector.Select(doc, new List<string>(), new List<string> { "spr_002" }, 0);

        // Selected sprite must be first
        Assert.Equal("spr_002", result[0].Sprite.Id);
    }

    [Fact]
    public void SelectedAndContextSelector_IncludesBackgroundContext()
    {
        var doc = new CompositionDocument();
        var bgLayer = new Layer { Id = "bg", Name = "BG", OsbLayer = OsbLayer.Background };
        bgLayer.Sprites.Add(new SpriteDeclaration { Id = "bg_0", LayerId = "bg", TexturePath = "bg.png" });
        var fgLayer = new Layer { Id = "fg", Name = "FG", OsbLayer = OsbLayer.Foreground };
        fgLayer.Sprites.Add(new SpriteDeclaration { Id = "fg_0", LayerId = "fg", TexturePath = "fg.png" });
        doc.Layers.Add(bgLayer);
        doc.Layers.Add(fgLayer);

        var selector = new SelectedAndContextSelector(50);
        // No selection - background context should still be included
        var result = selector.Select(doc, new List<string>(), new List<string>(), 0);

        var ids = result.Select(r => r.Sprite.Id).ToHashSet();
        Assert.Contains("bg_0", ids);
    }

    [Fact]
    public void SelectedAndContextSelector_RespectsMaxRenderedLayers()
    {
        var doc = BuildDocumentWithSprites(20);
        var selector = new SelectedAndContextSelector(5);

        // Select all sprites via layer id
        var result = selector.Select(doc, new List<string> { "layer_0" }, new List<string>(), 0);

        Assert.Equal(5, result.Count);
    }

    [Fact]
    public void SelectedAndContextSelector_SelectsByLayerId()
    {
        var doc = new CompositionDocument();
        var layer = new Layer { Id = "lyr", Name = "L", OsbLayer = OsbLayer.Foreground };
        layer.Sprites.Add(new SpriteDeclaration { Id = "s1", LayerId = "lyr", TexturePath = "a.png" });
        layer.Sprites.Add(new SpriteDeclaration { Id = "s2", LayerId = "lyr", TexturePath = "b.png" });
        doc.Layers.Add(layer);

        var selector = new SelectedAndContextSelector(50);
        var result = selector.Select(doc, new List<string> { "lyr" }, new List<string>(), 0);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, r => r.Sprite.Id == "s1");
        Assert.Contains(result, r => r.Sprite.Id == "s2");
    }
}


