using VisualCompositor.Core.Model;
using VisualCompositor.Core.Primitives;
using VisualCompositor.Core.Validation;

namespace VisualCompositor.Osb.Export;

/// <summary>Compiles a <see cref="CompositionDocument"/> back into .osb text per design document §7.
/// Entry point for Phase 7 export. Handles SubTask 27.6: pre-osb-export validation, layer ordering,
/// variable section emission, and RawBlock orphan warning handling.</summary>
public sealed class OsbExportCompiler
{
    /// <summary>Compile a <see cref="CompositionDocument"/> into .osb text.
    /// Runs pre-export validation (document-state validators E/G/H/I/J) first; if hard errors are
    /// found, returns diagnostics with <see cref="ExportResult.Succeeded"/> == false and empty text.</summary>
    public ExportResult Compile(CompositionDocument document, ExportOptions? options = null)
    {
        options ??= new ExportOptions();
        var diagnostics = new List<Diagnostic>();

        // SubTask 27.6: pre-osb-export validation (document-state validators E/G/H/I/J).
        var validation = ValidationDispatcher.Validate(document, ValidationEntryPoint.PreOsbExport);
        diagnostics.AddRange(validation.Diagnostics);

        if (validation.HasErrors)
        {
            return new ExportResult
            {
                Text = string.Empty,
                Diagnostics = diagnostics,
                Succeeded = false,
            };
        }

        var lines = new List<string>();
        EmitVariablesSection(lines, document);
        EmitEventsSection(lines, document, options, diagnostics);

        return new ExportResult
        {
            Text = string.Join("\n", lines),
            Diagnostics = diagnostics,
            Succeeded = true,
        };
    }

    // ---------- [Variables] ----------

    private static void EmitVariablesSection(List<string> lines, CompositionDocument document)
    {
        if (document.Variables.Count == 0)
            return;
        lines.Add("[Variables]");
        // Sort by key for deterministic output (Dictionary does not guarantee order).
        foreach (var kvp in document.Variables.OrderBy(k => k.Key, StringComparer.Ordinal))
            lines.Add($"{kvp.Key}={kvp.Value}");
    }

    // ---------- [Events] ----------

    private static void EmitEventsSection(List<string> lines, CompositionDocument document,
        ExportOptions options, List<Diagnostic> diagnostics)
    {
        lines.Add("[Events]");

        // Build a map of LayerId -> Layer for RawBlock grouping.
        var layerById = new Dictionary<string, Layer>(StringComparer.Ordinal);
        foreach (var layer in document.Layers)
            layerById[layer.Id] = layer;

        // Group RawBlocks by LayerId; track orphans (LayerId not matching any layer).
        var rawBlocksByLayer = new Dictionary<string, List<RawBlock>>(StringComparer.Ordinal);
        var orphanRawBlocks = new List<RawBlock>();
        foreach (var raw in document.RawBlocks)
        {
            if (layerById.ContainsKey(raw.LayerId))
            {
                if (!rawBlocksByLayer.TryGetValue(raw.LayerId, out var list))
                {
                    list = new List<RawBlock>();
                    rawBlocksByLayer[raw.LayerId] = list;
                }
                list.Add(raw);
            }
            else
            {
                orphanRawBlocks.Add(raw);
            }
        }

        // Emit orphan warnings for RawBlocks whose layer doesn't exist (design §6, line 184).
        foreach (var orphan in orphanRawBlocks)
        {
            diagnostics.Add(new Diagnostic
            {
                Code = FailureCodes.RAW_ANCHOR_ORPHANED,
                Message = $"RawBlock '{orphan.Id}' references layer '{orphan.LayerId}' which does not exist in the document.",
                Severity = DiagnosticSeverity.Warning,
                Scope = "export",
            });
        }

        var bezier = new BezierExporter(options, diagnostics);
        var spriteExporter = new SpriteExporter(bezier, options);
        var placer = new RawBlockPlacer(document.CommandRecords, diagnostics);

        // Iterate layers in OsbLayer enum order (Background=0..Overlay=4),
        // preserving document order within each OsbLayer.
        foreach (var osbLayer in Enum.GetValues<OsbLayer>())
        {
            foreach (var layer in document.Layers.Where(l => l.OsbLayer == osbLayer))
                EmitLayer(lines, layer, spriteExporter, placer, rawBlocksByLayer);
        }
    }

    private static void EmitLayer(List<string> lines, Layer layer,
        SpriteExporter spriteExporter, RawBlockPlacer placer,
        Dictionary<string, List<RawBlock>> rawBlocksByLayer)
    {
        // Collect all output lines for this layer (sprites in document order).
        var outputLines = new List<OutputLine>();
        foreach (var sprite in layer.Sprites)
            outputLines.AddRange(spriteExporter.Export(sprite));

        // Get this layer's RawBlocks (in document order).
        rawBlocksByLayer.TryGetValue(layer.Id, out var rawBlocks);

        // Interleave RawBlocks according to their anchors.
        var placed = placer.Place(outputLines, rawBlocks ?? new List<RawBlock>());
        lines.AddRange(placed);
    }
}
