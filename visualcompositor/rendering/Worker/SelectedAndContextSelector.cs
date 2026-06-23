using VisualCompositor.Core.Model;
using VisualCompositor.Core.Primitives;

namespace VisualCompositor.Rendering.Worker;

/// <summary>Selects which sprites to render based on selection + context rules (§8).
/// 1. Selected layers' sprites
/// 2. Adjacent visible layers
/// 3. Background context
/// 4. Fullscreen quads
/// 5. Missing placeholders
/// Limited by MaxRenderedLayers.</summary>
public sealed class SelectedAndContextSelector
{
    private readonly int _maxRenderedLayers;

    public SelectedAndContextSelector(int maxRenderedLayers)
    {
        _maxRenderedLayers = maxRenderedLayers;
    }

    public List<SpriteReference> Select(CompositionDocument doc, List<string> selectedLayerIds, List<string> selectedSpriteIds, double time)
    {
        var result = new List<SpriteReference>();
        var seen = new HashSet<string>();

        // 1. Selected sprites first
        foreach (var layerId in selectedLayerIds)
        {
            var layer = doc.Layers.FirstOrDefault(l => l.Id == layerId);
            if (layer == null) continue;
            foreach (var sprite in layer.Sprites)
            {
                if (seen.Add(sprite.Id))
                    result.Add(new SpriteReference { Layer = layer, Sprite = sprite });
            }
        }

        foreach (var spriteId in selectedSpriteIds)
        {
            foreach (var layer in doc.Layers)
            {
                var sprite = layer.Sprites.FirstOrDefault(s => s.Id == spriteId);
                if (sprite != null && seen.Add(sprite.Id))
                    result.Add(new SpriteReference { Layer = layer, Sprite = sprite });
            }
        }

        // 2. Adjacent visible layers (layers adjacent to selected in OsbLayer order)
        if (selectedLayerIds.Count > 0)
        {
            var selectedOsbLayers = doc.Layers
                .Where(l => selectedLayerIds.Contains(l.Id))
                .Select(l => l.OsbLayer)
                .ToHashSet();

            foreach (var osbLayer in Enum.GetValues<OsbLayer>())
            {
                if (selectedOsbLayers.Contains(osbLayer)) continue;
                // Check if adjacent to any selected
                bool isAdjacent = false;
                foreach (var sel in selectedOsbLayers)
                {
                    if (Math.Abs((int)osbLayer - (int)sel) == 1)
                    {
                        isAdjacent = true;
                        break;
                    }
                }
                if (isAdjacent)
                {
                    foreach (var layer in doc.Layers.Where(l => l.OsbLayer == osbLayer))
                    {
                        foreach (var sprite in layer.Sprites)
                        {
                            if (seen.Add(sprite.Id))
                                result.Add(new SpriteReference { Layer = layer, Sprite = sprite });
                        }
                    }
                }
            }
        }

        // 3. Background context (Background layer always included)
        foreach (var layer in doc.Layers.Where(l => l.OsbLayer == OsbLayer.Background))
        {
            foreach (var sprite in layer.Sprites)
            {
                if (seen.Add(sprite.Id))
                    result.Add(new SpriteReference { Layer = layer, Sprite = sprite });
            }
        }

        // 4. Limit by MaxRenderedLayers
        if (result.Count > _maxRenderedLayers)
        {
            result = result.Take(_maxRenderedLayers).ToList();
        }

        return result;
    }
}

public sealed class SpriteReference
{
    public Layer Layer { get; init; } = new();
    public SpriteDeclaration Sprite { get; init; } = new();
}
