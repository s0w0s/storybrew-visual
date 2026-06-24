using VisualCompositor.Core.Model;
using VisualCompositor.Core.Primitives;
using VisualCompositor.Rendering.Worker;

namespace VisualCompositor.UI.Views;

/// <summary>Abstract view interface. WinForms/Avalonia implementation would fulfill this.</summary>
public interface ICompositorView
{
    // Timeline
    void SetTimeRange(double startTime, double endTime);
    void SetCurrentTime(double time);
    event Action<double>? TimeChanged;

    // Layer list
    void UpdateLayerList(List<Layer> layers);
    event Action<List<string>>? LayerSelectionChanged;

    // Preview canvas
    void UpdatePreview(RenderResult result);
    void SetViewport(double zoom, Vector2 pan);
    event Action<double, Vector2>? ViewportChanged;

    // Diagnostics
    void ShowDiagnostics(List<string> diagnostics);

    // Menu actions
    event Action? OpenOsbRequested;
    event Action? SaveStorybrewCompRequested;
    event Action? ExportOsbRequested;
    event Action? UndoRequested;
    event Action? RedoRequested;
    event Action<string>? ExpandBlockRequested;

    // Status
    void SetStatus(string status);
    void SetDirty(bool isDirty);
}
