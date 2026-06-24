using VisualCompositor.Core.Primitives;

namespace VisualCompositor.Core.Snapshots;

/// <summary>Snapshot of a relative command inside a Loop/Trigger block.</summary>
public sealed class RelativeCommandSnapshot
{
    public string Id { get; init; } = string.Empty;

    /// <summary>Command type letter: M, MX, MY, S, V, R, F, C, P.</summary>
    public string CommandType { get; init; } = string.Empty;

    public OsbEasing Easing { get; init; }

    public double StartTime { get; init; }

    public double EndTime { get; init; }

    /// <summary>Start value as string (parsed per command type).</summary>
    public string StartValue { get; init; } = string.Empty;

    /// <summary>End value as string.</summary>
    public string EndValue { get; init; } = string.Empty;

    public string? ParentBlockId { get; init; }

    public RelativeCommandSnapshot DeepClone() => new()
    {
        Id = Id,
        CommandType = CommandType,
        Easing = Easing,
        StartTime = StartTime,
        EndTime = EndTime,
        StartValue = StartValue,
        EndValue = EndValue,
        ParentBlockId = ParentBlockId,
    };
}
