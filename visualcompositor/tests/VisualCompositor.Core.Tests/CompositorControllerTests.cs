using VisualCompositor.Core.Model;
using VisualCompositor.Core.Primitives;
using VisualCompositor.Rendering.Worker;
using VisualCompositor.State;
using VisualCompositor.UI.Controller;
using VisualCompositor.UI.Views;

namespace VisualCompositor.Core.Tests;

public class CompositorControllerTests : IDisposable
{
    private readonly HeadlessCompositorView _view = new();
    private readonly RenderWorker _worker = new();
    private readonly CompositorController _controller;

    public CompositorControllerTests()
    {
        _controller = new CompositorController(_view, _worker);
    }

    public void Dispose()
    {
        _worker.Dispose();
    }

    private const string SimpleOsb = """
[Events]
Sprite,3,4,"bg.jpg",320,240
 F,0,0,1000,1,0
""";

    private const string OsbWithLoop = """
[Events]
Sprite,3,4,"bg.jpg",320,240
 L,1000,3
  F,0,0,500,0,1
""";

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
    public void Controller_Initialize_SetsUpViewWithEmptyDocument()
    {
        _controller.Initialize();

        Assert.Equal(0, _view.CurrentTime);
        Assert.Empty(_view.Layers);
        Assert.Equal("Ready", _view.Status);
        Assert.False(_view.IsDirty);
    }

    [Fact]
    public void OpenOsb_LoadsDocumentAndUpdatesView()
    {
        _controller.OpenOsb(SimpleOsb);

        Assert.NotEmpty(_view.Layers);
        Assert.Single(_controller.Document.Layers);
        // Background layer (index 3 in OSB -> Foreground)
        var layer = _controller.Document.Layers[0];
        Assert.Single(layer.Sprites);
        Assert.Equal("bg.jpg", layer.Sprites[0].TexturePath);
        Assert.True(_view.IsDirty);
        Assert.Contains("Imported", _view.Status);
    }

    [Fact]
    public void OpenOsb_WithLoopBlock_ImportsSuccessfully()
    {
        _controller.OpenOsb(OsbWithLoop);

        Assert.NotEmpty(_view.Layers);
        var sprite = _controller.Document.Layers[0].Sprites[0];
        Assert.Single(sprite.Blocks);
        Assert.IsType<LoopBlock>(sprite.Blocks[0]);
    }

    [Fact]
    public void SimulateTimeChange_DispatchesActionAndUpdatesState()
    {
        _controller.Initialize();
        Assert.Equal(0, _controller.State.CurrentTime);

        _view.SimulateTimeChange(5000);

        Assert.Equal(5000, _controller.State.CurrentTime);
        Assert.Equal(5000, _view.CurrentTime);
    }

    [Fact]
    public void SimulateUndo_WithEmptyStack_SetsNothingToUndoStatus()
    {
        _controller.Initialize();

        _view.SimulateUndo();

        Assert.Equal("Nothing to undo", _view.Status);
        Assert.Empty(_controller.State.UndoStack);
        Assert.Empty(_controller.State.RedoStack);
    }

    [Fact]
    public void SimulateRedo_WithEmptyStack_SetsNothingToRedoStatus()
    {
        _controller.Initialize();

        _view.SimulateRedo();

        Assert.Equal("Nothing to redo", _view.Status);
        Assert.Empty(_controller.State.UndoStack);
        Assert.Empty(_controller.State.RedoStack);
    }

    [Fact]
    public async Task OnRenderResult_MatchingRevision_AcceptsResult()
    {
        _controller.OpenOsb(SimpleOsb);

        // Wait for the render result to arrive
        await WaitForAsync(() => _view.LastRenderResult != null, TimeSpan.FromSeconds(2));

        Assert.NotNull(_view.LastRenderResult);
        Assert.Equal(_controller.State.Revision, _view.LastRenderResult!.Revision);
        Assert.True(_view.LastRenderResult.Succeeded);
    }

    [Fact]
    public async Task OnRenderResult_StaleRevision_IgnoresResult()
    {
        _controller.OpenOsb(SimpleOsb);

        // Wait for the matching render result
        await WaitForAsync(() => _view.LastRenderResult != null, TimeSpan.FromSeconds(2));
        Assert.NotNull(_view.LastRenderResult);
        var matchingRevision = _view.LastRenderResult!.Revision;

        // Directly submit a request to the worker with a stale (non-matching) revision.
        // The controller's OnRenderResult handler should ignore this result because
        // its Revision does not match the controller's current State.Revision.
        _worker.Submit(new RenderRequest
        {
            Revision = 99999,
            Document = _controller.Document,
            Time = 0,
            SelectedLayerIds = new List<string>(),
            SelectedSpriteIds = new List<string>(),
        });

        // Wait for the stale result to be processed by the worker
        await WaitForAsync(() => _view.LastRenderResult != null && _view.LastRenderResult.Revision == 99999,
            TimeSpan.FromSeconds(2));

        // The stale result should have been ignored: LastRenderResult should still
        // hold the matching result, not the stale one.
        Assert.NotNull(_view.LastRenderResult);
        Assert.Equal(matchingRevision, _view.LastRenderResult!.Revision);
        Assert.NotEqual(99999, _view.LastRenderResult.Revision);
    }

    [Fact]
    public void SaveStorybrewComp_WritesFile()
    {
        _controller.OpenOsb(SimpleOsb);

        var tempPath = Path.Combine(Path.GetTempPath(), $"compositor_test_{Guid.NewGuid():N}.storybrewcomp");
        try
        {
            _controller.SaveStorybrewComp(tempPath);

            Assert.True(File.Exists(tempPath));
            var content = File.ReadAllText(tempPath);
            Assert.False(string.IsNullOrEmpty(content));
            // The saved JSON should contain the texture path from the imported document
            Assert.Contains("bg.jpg", content);
            Assert.False(_view.IsDirty);
            Assert.Contains("Saved", _view.Status);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    [Fact]
    public void SimulateLayerSelection_UpdatesState()
    {
        _controller.OpenOsb(SimpleOsb);
        var layerId = _controller.Document.Layers[0].Id;

        _view.SimulateLayerSelection(new List<string> { layerId });

        Assert.Single(_controller.State.SelectedLayerIds);
        Assert.Equal(layerId, _controller.State.SelectedLayerIds[0]);
    }

    [Fact]
    public void ExportOsb_ReturnsPlaceholderText()
    {
        _controller.Initialize();
        var osbText = _controller.ExportOsb();

        Assert.False(string.IsNullOrEmpty(osbText));
        Assert.Contains("not yet implemented", osbText);
        Assert.Contains("Exported", _view.Status);
    }
}
