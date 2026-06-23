using VisualCompositor.Core.Model;
using VisualCompositor.Core.Primitives;
using VisualCompositor.Core.Serialization;
using VisualCompositor.Osb.Import;
using VisualCompositor.Osb.Parser;
using VisualCompositor.Rendering.Worker;
using VisualCompositor.State;
using VisualCompositor.State.Actions;
using VisualCompositor.State.Reducer;
using VisualCompositor.UI.Views;

namespace VisualCompositor.UI.Controller;

/// <summary>UI controller/presenter. Wires State + Rendering + Import/Export.
/// Dispatches actions to the Reducer, submits render requests, handles results.</summary>
public sealed class CompositorController
{
    private readonly ICompositorView _view;
    private readonly RenderWorker _renderWorker;
    private EditorState _state = EditorState.Initial;

    public EditorState State => _state;
    public CompositionDocument Document => _state.Document;

    public CompositorController(ICompositorView view, RenderWorker renderWorker)
    {
        _view = view;
        _renderWorker = renderWorker;

        // Wire view events
        _view.TimeChanged += OnTimeChanged;
        _view.LayerSelectionChanged += OnLayerSelectionChanged;
        _view.OpenOsbRequested += OnOpenOsb;
        _view.SaveStorybrewCompRequested += OnSaveStorybrewComp;
        _view.ExportOsbRequested += OnExportOsb;
        _view.UndoRequested += OnUndo;
        _view.RedoRequested += OnRedo;
        _view.ExpandBlockRequested += OnExpandBlock;

        // Wire render worker
        _renderWorker.ResultAvailable += OnRenderResult;
    }

    private void OnTimeChanged(double time)
    {
        _state = Reducer.Reduce(_state, new SetCurrentTimeAction(time));
        _view.SetCurrentTime(time);
        SubmitRenderRequest();
    }

    private void OnLayerSelectionChanged(List<string> layerIds)
    {
        _state = Reducer.Reduce(_state, new SetSelectionAction(new List<string>(), layerIds));
        SubmitRenderRequest();
    }

    public void OpenOsb(string osbText)
    {
        var parsed = new OsbParser().Parse(osbText);
        var importResult = new ImportMapper().Map(parsed);
        _state = Reducer.Reduce(_state, new LoadDocumentAction(importResult.Document));

        // Update view
        _view.UpdateLayerList(_state.Document.Layers);
        _view.ShowDiagnostics(importResult.Diagnostics.ConvertAll(d => d.ToString()));
        _view.SetStatus($"Imported .osb with {_state.Document.Layers.Count} layers");
        _view.SetDirty(true);

        SubmitRenderRequest();
    }

    private void OnOpenOsb()
    {
        // In a real implementation, this would open a file dialog
        // The actual file reading is handled by the caller via OpenOsb(string)
    }

    public void SaveStorybrewComp(string filePath)
    {
        var json = StorybrewCompSerializer.Serialize(_state.Document);
        System.IO.File.WriteAllText(filePath, json);
        _view.SetStatus($"Saved to {filePath}");
        _view.SetDirty(false);
    }

    private void OnSaveStorybrewComp()
    {
        // File dialog would be handled by the view implementation
    }

    public string ExportOsb()
    {
        // The export compiler (Phase 7) would produce the .osb text
        // For MVP, we return a placeholder
        var osbText = "// .osb export not yet implemented (Phase 7)";
        _view.SetStatus("Exported .osb (placeholder)");
        return osbText;
    }

    private void OnExportOsb()
    {
        ExportOsb();
    }

    private void OnUndo()
    {
        _state = Reducer.Reduce(_state, new UndoAction());
        _view.UpdateLayerList(_state.Document.Layers);
        _view.SetStatus(_state.UndoStack.Count > 0 ? "Undo" : "Nothing to undo");
        SubmitRenderRequest();
    }

    private void OnRedo()
    {
        _state = Reducer.Reduce(_state, new RedoAction());
        _view.UpdateLayerList(_state.Document.Layers);
        _view.SetStatus(_state.RedoStack.Count > 0 ? "Redo" : "Nothing to redo");
        SubmitRenderRequest();
    }

    private void OnExpandBlock(string blockId)
    {
        _state = Reducer.Reduce(_state, new ExpandBlockAction("", blockId));
        _view.UpdateLayerList(_state.Document.Layers);
        SubmitRenderRequest();
    }

    private void SubmitRenderRequest()
    {
        var request = new RenderRequest
        {
            Revision = _state.Revision,
            Document = _state.Document,
            Time = _state.CurrentTime,
            Quality = RenderQuality.FullPreview,
            SelectedLayerIds = _state.SelectedLayerIds,
            SelectedSpriteIds = _state.SelectedSpriteIds,
        };
        _renderWorker.Submit(request);
    }

    private void OnRenderResult(RenderResult result)
    {
        // UI only accepts results where Revision matches current state
        if (result.Revision != _state.Revision)
            return;

        _view.UpdatePreview(result);
    }

    public void Initialize()
    {
        _view.SetTimeRange(0, 100000);
        _view.SetCurrentTime(0);
        _view.UpdateLayerList(_state.Document.Layers);
        _view.SetStatus("Ready");
        _view.SetDirty(false);
    }
}
