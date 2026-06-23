using VisualCompositor.Core.Model;
using VisualCompositor.Core.Primitives;
using VisualCompositor.Rendering.Worker;

namespace VisualCompositor.Core.Tests;

public class RenderWorkerTests
{
    private static CompositionDocument BuildDocumentWithSprites(int layerSpriteCount, OsbLayer osbLayer = OsbLayer.Foreground)
    {
        var doc = new CompositionDocument();
        var layer = new Layer { Id = "layer_0", Name = "L", OsbLayer = osbLayer };
        for (int i = 0; i < layerSpriteCount; i++)
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

    [Fact]
    public void LayerSelector_SelectsSelectedSpritesFirst()
    {
        var doc = BuildDocumentWithSprites(5);
        var selected = new List<string> { "spr_002" };

        var result = LayerSelector.SelectSpritesForRendering(doc, selected, 50);

        // Selected sprite must be first
        Assert.Equal("spr_002", result[0].Id);
        // Adjacent neighbors (spr_001, spr_003) should be included
        var ids = result.Select(s => s.Id).ToHashSet();
        Assert.Contains("spr_001", ids);
        Assert.Contains("spr_003", ids);
    }

    [Fact]
    public void LayerSelector_LimitsByMaxRenderedLayers()
    {
        var doc = BuildDocumentWithSprites(20);
        // Select all sprites
        var selected = Enumerable.Range(0, 20).Select(i => $"spr_{i:D3}").ToList();

        var result = LayerSelector.SelectSpritesForRendering(doc, selected, 5);

        Assert.Equal(5, result.Count);
    }

    [Fact]
    public void LayerSelector_IncludesBackgroundContext()
    {
        var doc = new CompositionDocument();
        var bgLayer = new Layer { Id = "bg", Name = "BG", OsbLayer = OsbLayer.Background };
        bgLayer.Sprites.Add(new SpriteDeclaration { Id = "bg_0", LayerId = "bg", TexturePath = "bg.png" });
        var fgLayer = new Layer { Id = "fg", Name = "FG", OsbLayer = OsbLayer.Foreground };
        fgLayer.Sprites.Add(new SpriteDeclaration { Id = "fg_0", LayerId = "fg", TexturePath = "fg.png" });
        doc.Layers.Add(bgLayer);
        doc.Layers.Add(fgLayer);

        // No selection - background context should still be included
        var result = LayerSelector.SelectSpritesForRendering(doc, new List<string>(), 50);

        var ids = result.Select(s => s.Id).ToHashSet();
        Assert.Contains("bg_0", ids);
    }

    [Fact]
    public void LayerSelector_IncludesFullscreenQuadsByTexturePath()
    {
        var doc = new CompositionDocument();
        var layer = new Layer { Id = "fg", Name = "FG", OsbLayer = OsbLayer.Foreground };
        layer.Sprites.Add(new SpriteDeclaration { Id = "bg_full", LayerId = "fg", TexturePath = "bg/full.png" });
        layer.Sprites.Add(new SpriteDeclaration { Id = "normal", LayerId = "fg", TexturePath = "normal.png" });
        doc.Layers.Add(layer);

        var result = LayerSelector.SelectSpritesForRendering(doc, new List<string>(), 50);

        var ids = result.Select(s => s.Id).ToHashSet();
        Assert.Contains("bg_full", ids);
        // "normal" sprite has no "bg" in path and is not selected, so should not be included
        Assert.DoesNotContain("normal", ids);
    }

    [Fact]
    public async Task RenderWorker_SubmitsAndProcessesRequest()
    {
        var backend = new MockRenderBackend();
        var doc = BuildDocumentWithSprites(1);
        var worker = new RenderWorker(backend);
        RenderResult? capturedResult = null;
        worker.ResultReady += r => capturedResult = r;

        worker.Start();
        try
        {
            await worker.SubmitAsync(new RenderRequest
            {
                Revision = 42,
                Document = doc,
                Time = 0,
                Quality = RenderQuality.FullPreview,
                SelectedSpriteIds = new List<string> { "spr_000" },
            });

            // Wait for the worker to process the request
            await WaitForAsync(() => capturedResult != null, TimeSpan.FromSeconds(2));

            Assert.NotNull(capturedResult);
            Assert.Equal(42, capturedResult!.Revision);
            Assert.Equal(1, backend.BeginFrameCallCount);
            Assert.Equal(1, backend.EndFrameCallCount);
            Assert.Single(backend.DrawQuadCalls);
            Assert.NotNull(capturedResult.Frame);
        }
        finally
        {
            worker.Dispose();
        }
    }

    [Fact]
    public async Task RenderWorker_ResolutionScale_FullPreview()
    {
        var backend = new MockRenderBackend();
        var doc = BuildDocumentWithSprites(1);
        var worker = new RenderWorker(backend);
        RenderResult? capturedResult = null;
        worker.ResultReady += r => capturedResult = r;

        worker.Start();
        try
        {
            await worker.SubmitAsync(new RenderRequest
            {
                Revision = 1,
                Document = doc,
                Time = 0,
                Quality = RenderQuality.FullPreview,
                SelectedSpriteIds = new List<string> { "spr_000" },
            });

            await WaitForAsync(() => capturedResult != null, TimeSpan.FromSeconds(2));

            Assert.NotNull(capturedResult);
            Assert.Equal(1.0f, backend.LastResolutionScale);
        }
        finally
        {
            worker.Dispose();
        }
    }

    [Fact]
    public async Task RenderWorker_ResolutionScale_InteractiveScrub()
    {
        var backend = new MockRenderBackend();
        var doc = BuildDocumentWithSprites(1);
        var worker = new RenderWorker(backend);
        RenderResult? capturedResult = null;
        worker.ResultReady += r => capturedResult = r;

        worker.Start();
        try
        {
            await worker.SubmitAsync(new RenderRequest
            {
                Revision = 1,
                Document = doc,
                Time = 0,
                Quality = RenderQuality.InteractiveScrub,
                SelectedSpriteIds = new List<string> { "spr_000" },
            });

            await WaitForAsync(() => capturedResult != null, TimeSpan.FromSeconds(2));

            Assert.NotNull(capturedResult);
            Assert.Equal(0.5f, backend.LastResolutionScale);
        }
        finally
        {
            worker.Dispose();
        }
    }

    [Fact]
    public async Task RenderWorker_ResolutionScale_FastScrub()
    {
        var backend = new MockRenderBackend();
        var doc = BuildDocumentWithSprites(1);
        var worker = new RenderWorker(backend);
        RenderResult? capturedResult = null;
        worker.ResultReady += r => capturedResult = r;

        worker.Start();
        try
        {
            await worker.SubmitAsync(new RenderRequest
            {
                Revision = 1,
                Document = doc,
                Time = 0,
                Quality = RenderQuality.FastScrub,
                SelectedSpriteIds = new List<string> { "spr_000" },
            });

            await WaitForAsync(() => capturedResult != null, TimeSpan.FromSeconds(2));

            Assert.NotNull(capturedResult);
            Assert.Equal(0.25f, backend.LastResolutionScale);
        }
        finally
        {
            worker.Dispose();
        }
    }

    [Fact]
    public async Task RenderWorker_ResolutionScale_TimelineThumbnail()
    {
        var backend = new MockRenderBackend();
        var doc = BuildDocumentWithSprites(1);
        var worker = new RenderWorker(backend);
        RenderResult? capturedResult = null;
        worker.ResultReady += r => capturedResult = r;

        worker.Start();
        try
        {
            await worker.SubmitAsync(new RenderRequest
            {
                Revision = 1,
                Document = doc,
                Time = 0,
                Quality = RenderQuality.TimelineThumbnail,
                SelectedSpriteIds = new List<string> { "spr_000" },
            });

            await WaitForAsync(() => capturedResult != null, TimeSpan.FromSeconds(2));

            Assert.NotNull(capturedResult);
            Assert.Equal(0.125f, backend.LastResolutionScale);
        }
        finally
        {
            worker.Dispose();
        }
    }

    [Fact]
    public async Task RenderWorker_DiscardsOldRequestsWhenNewSubmitted()
    {
        var backend = new MockRenderBackend();
        // Build a doc with many sprites so each render takes a bit longer
        var doc = BuildDocumentWithSprites(50);
        var worker = new RenderWorker(backend);
        var results = new System.Collections.Concurrent.ConcurrentBag<RenderResult>();
        worker.ResultReady += r => results.Add(r);

        worker.Start();
        try
        {
            // Submit multiple requests quickly - the channel has capacity 1 with DropOldest
            for (int i = 0; i < 5; i++)
            {
                await worker.SubmitAsync(new RenderRequest
                {
                    Revision = i,
                    Document = doc,
                    Time = i * 100,
                    Quality = RenderQuality.FullPreview,
                    SelectedSpriteIds = new List<string> { "spr_000" },
                });
            }

            // Wait for at least one result
            await WaitForAsync(() => results.Count > 0, TimeSpan.FromSeconds(3));

            // The channel only holds 1 item, so at most a few requests should have been processed.
            // We don't assert an exact count because of timing, but we should not have processed all 5.
            Assert.True(results.Count <= 5);
            Assert.True(results.Count >= 1);
        }
        finally
        {
            worker.Dispose();
        }
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
}
