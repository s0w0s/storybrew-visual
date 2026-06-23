using VisualCompositor.Core.Primitives;

namespace VisualCompositor.Core.Model;

/// <summary>A command inside a Loop/Trigger block. Its Id must equal its CommandRecord.CommandId.</summary>
public sealed class RelativeCommand
{
    /// <summary>Opaque persistent id. Must match the CommandRecord.CommandId for this command.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Command type letter: M, MX, MY, S, V, R, F, C, P.</summary>
    public string CommandType { get; set; } = string.Empty;

    public OsbEasing Easing { get; set; }

    public double StartTime { get; set; }

    public double EndTime { get; set; }

    public string StartValue { get; set; } = string.Empty;

    public string EndValue { get; set; } = string.Empty;

    public string? ParentBlockId { get; set; }

    public RelativeCommand Clone() => new()
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
