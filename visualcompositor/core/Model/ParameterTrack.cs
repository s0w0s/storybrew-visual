namespace VisualCompositor.Core.Model;

/// <summary>Track for P commands (Additive/FlipH/FlipV). Does NOT use regular keyframe tracks.</summary>
public sealed class ParameterTrack
{
    public List<ParameterSegment> Segments { get; set; } = new();

    public ParameterTrack Clone() => new()
    {
        Segments = Segments.ConvertAll(s => s.Clone()),
    };
}
