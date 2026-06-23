namespace VisualCompositor.Osb.Export;

/// <summary>A single emitted output line, optionally associated with a command id
/// for RawBlock anchor placement.</summary>
internal sealed class OutputLine
{
    public string Text { get; init; } = string.Empty;

    /// <summary>The command id this line corresponds to, if any.
    /// Used by <see cref="RawBlockPlacer"/> to find anchor positions.</summary>
    public string? CommandId { get; init; }

    public OutputLine(string text, string? commandId = null)
    {
        Text = text;
        CommandId = commandId;
    }
}
