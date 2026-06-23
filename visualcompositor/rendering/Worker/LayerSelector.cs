using VisualCompositor.Core.Model;
using VisualCompositor.Core.Primitives;

namespace VisualCompositor.Rendering.Worker;

/// <summary>Selects sprites for rendering using the SelectedAndContext rule.
/// 1. Selected sprites
/// 2. Adjacent visible sprites (same layer, near in z-order)
/// 3. Background context sprites
/// 4. Fullscreen quads
/// 5. Missing texture placeholders
/// Limited by MaxRenderedLayers.</summary>
public static class LayerSelector
{
    public static List<SpriteDeclaration> SelectSpritesForRendering(
        CompositionDocument document,
        List<string> selectedSpriteIds,
        int maxRenderedLayers)
    {
        var result = new List<SpriteDeclaration>();
        var selected = new HashSet<string>(selectedSpriteIds);

        // Collect all sprites from all layers
        var allSprites = new List<(SpriteDeclaration sprite, Layer layer)>();
        foreach (var layer in document.Layers)
        {
            foreach (var sprite in layer.Sprites)
            {
                allSprites.Add((sprite, layer));
            }
        }

        // 1. Selected sprites first
        foreach (var (sprite, _) in allSprites)
        {
            if (selected.Contains(sprite.Id))
                result.Add(sprite);
        }

        // 2. Adjacent visible sprites (same layer, neighbors in z-order)
        foreach (var (sprite, layer) in allSprites)
        {
            if (selected.Contains(sprite.Id))
                continue;
            var layerSprites = layer.Sprites;
            var idx = layerSprites.IndexOf(sprite);
            // Check if adjacent to a selected sprite
            if (idx > 0 && selected.Contains(layerSprites[idx - 1].Id))
                result.Add(sprite);
            else if (idx < layerSprites.Count - 1 && selected.Contains(layerSprites[idx + 1].Id))
                result.Add(sprite);
        }

        // 3. Background context (Background layer sprites)
        foreach (var layer in document.Layers.Where(l => l.OsbLayer == OsbLayer.Background))
        {
            foreach (var sprite in layer.Sprites)
            {
                if (!result.Contains(sprite))
                    result.Add(sprite);
            }
        }

        // 4. Fullscreen quads (large sprites that cover the screen)
        foreach (var (sprite, _) in allSprites)
        {
            if (result.Contains(sprite))
                continue;
            // Heuristic: if texture path contains "bg" or "background", treat as fullscreen
            if (!string.IsNullOrEmpty(sprite.TexturePath) &&
                sprite.TexturePath.Contains("bg", StringComparison.OrdinalIgnoreCase))
            {
                result.Add(sprite);
            }
        }

        // 5. Missing placeholders are handled by the backend (LoadTextureAsync returns null)

        // Limit by MaxRenderedLayers
        if (result.Count > maxRenderedLayers)
            result = result.Take(maxRenderedLayers).ToList();

        return result;
    }
}
