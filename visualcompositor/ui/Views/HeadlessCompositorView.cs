using VisualCompositor.Core.Model;
using VisualCompositor.Core.Primitives;
using VisualCompositor.Rendering.Worker;

namespace VisualCompositor.UI.Views;

/// <summary>Headless implementation of ICompositorView for testing and console use.</summary>
public sealed class HeadlessCompositorView : ICompositorView
{
    public double CurrentTime { get; private set; }
    public List<Layer> Layers { get; private set; } = new();
    public RenderResult? LastRenderResult { get; private set; }
    public List<string> Diagnostics { get; private set; } = new();
    public string Status { get; private set; } = "";
    public bool IsDirty { get; private set; }

    public event Action<double>? TimeChanged;
    public event Action<List<string>>? LayerSelectionChanged;
    public event Action<double, Vector2>? ViewportChanged;
    public event Action? OpenOsbRequested;
    public event Action? SaveStorybrewCompRequested;
    public event Action? ExportOsbRequested;
    public event Action? UndoRequested;
    public event Action? RedoRequested;
    public event Action<string>? ExpandBlockRequested;

    public void SetTimeRange(double startTime, double endTime) { }
    public void SetCurrentTime(double time) { CurrentTime = time; }
    public void UpdateLayerList(List<Layer> layers) { Layers = layers; }
    public void UpdatePreview(RenderResult result) { LastRenderResult = result; }
    public void SetViewport(double zoom, Vector2 pan) { }
    public void ShowDiagnostics(List<string> diagnostics) { Diagnostics = diagnostics; }
    public void SetStatus(string status) { Status = status; }
    public void SetDirty(bool isDirty) { IsDirty = isDirty; }

    // Methods to simulate user interaction in tests
    public void SimulateTimeChange(double time) => TimeChanged?.Invoke(time);
    public void SimulateLayerSelection(List<string> layerIds) => LayerSelectionChanged?.Invoke(layerIds);
    public void SimulateUndo() => UndoRequested?.Invoke();
    public void SimulateRedo() => RedoRequested?.Invoke();
    public void SimulateOpenOsb() => OpenOsbRequested?.Invoke();
    public void SimulateSave() => SaveStorybrewCompRequested?.Invoke();
    public void SimulateExport() => ExportOsbRequested?.Invoke();
    public void SimulateExpandBlock(string blockId) => ExpandBlockRequested?.Invoke(blockId);
}
