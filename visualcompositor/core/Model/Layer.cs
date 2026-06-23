using VisualCompositor.Core.Primitives;

namespace VisualCompositor.Core.Model;

/// <summary>A storyboard layer (Background, Fail, Pass, Foreground, Overlay).</summary>
public sealed class Layer
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public OsbLayer OsbLayer { get; set; }

    /// <summary>Whether this layer is difficulty-specific.</summary>
    public bool DiffSpecific { get; set; }

    /// <summary>Sprite/Animation declarations on this layer.</summary>
    public List<SpriteDeclaration> Sprites { get; set; } = new();

    /// <summary>Layer-level property tracks (rare, for layer transforms).</summary>
    public List<PropertyTrack> PropertyTracks { get; set; } = new();

    /// <summary>Layer-level parameter track.</summary>
    public ParameterTrack? ParameterTrack { get; set; }

    /// <summary>Blocks directly on this layer (not inside a sprite).</summary>
    public List<StoryboardBlock> Blocks { get; set; } = new();

    public Layer Clone() => new()
    {
        Id = Id,
        Name = Name,
        OsbLayer = OsbLayer,
        DiffSpecific = DiffSpecific,
        Sprites = Sprites.ConvertAll(s => s.Clone()),
        PropertyTracks = PropertyTracks.ConvertAll(t => t.Clone()),
        ParameterTrack = ParameterTrack?.Clone(),
        Blocks = Blocks.ConvertAll(b => b switch
        {
            LoopBlock lb => (StoryboardBlock)lb.Clone(),
            TriggerBlock tb => tb.Clone(),
            _ => throw new InvalidOperationException($"Unknown block type: {b.GetType()}"),
        }),
    };
}
