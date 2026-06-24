using VisualCompositor.Core.Primitives;

namespace VisualCompositor.Core.Model;

/// <summary>A sprite or animation declaration on a layer (the "Sprite" or "Animation" line in .osb).</summary>
public sealed class SpriteDeclaration
{
    public string Id { get; set; } = string.Empty;

    public string LayerId { get; set; } = string.Empty;

    /// <summary>"Sprite" or "Animation".</summary>
    public string DeclarationType { get; set; } = "Sprite";

    public OsbLayer OsbLayer { get; set; }

    public OsbOrigin Origin { get; set; } = OsbOrigin.Centre;

    public string TexturePath { get; set; } = string.Empty;

    public Vector2 InitialPosition { get; set; }

    /// <summary>For Animation only.</summary>
    public int FrameCount { get; set; }

    /// <summary>For Animation only.</summary>
    public double FrameDelay { get; set; }

    /// <summary>For Animation only.</summary>
    public OsbLoopType LoopType { get; set; } = OsbLoopType.LoopForever;

    /// <summary>Property tracks for this sprite (Position, Scale, Rotation, Opacity, Color).</summary>
    public List<PropertyTrack> PropertyTracks { get; set; } = new();

    /// <summary>Parameter track for P commands.</summary>
    public ParameterTrack? ParameterTrack { get; set; }

    /// <summary>Blocks (Loop/Trigger) belonging to this sprite.</summary>
    public List<StoryboardBlock> Blocks { get; set; } = new();

    public SpriteDeclaration Clone() => new()
    {
        Id = Id,
        LayerId = LayerId,
        DeclarationType = DeclarationType,
        OsbLayer = OsbLayer,
        Origin = Origin,
        TexturePath = TexturePath,
        InitialPosition = InitialPosition,
        FrameCount = FrameCount,
        FrameDelay = FrameDelay,
        LoopType = LoopType,
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
