using VisualCompositor.Core.Primitives;

namespace VisualCompositor.Core.Model;

/// <summary>A segment on the ParameterTrack representing a P command (Additive/FlipH/FlipV).</summary>
public sealed class ParameterSegment
{
    public ParameterType Parameter { get; set; }

    public double StartTime { get; set; }

    /// <summary>End time. null means OSB empty end time (open-ended).</summary>
    public double? EndTime { get; set; }

    /// <summary>How the open-ended segment extends. .osb import maps empty end time to UntilLayerEnd.</summary>
    public OpenEndedMode OpenEndedMode { get; set; } = OpenEndedMode.UntilLayerEnd;

    public ParameterSegment Clone() => new()
    {
        Parameter = Parameter,
        StartTime = StartTime,
        EndTime = EndTime,
        OpenEndedMode = OpenEndedMode,
    };
}
