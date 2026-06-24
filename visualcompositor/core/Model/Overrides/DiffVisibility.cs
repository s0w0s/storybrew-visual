namespace VisualCompositor.Core.Model.Overrides;

/// <summary>Per-difficulty visibility settings for a layer.</summary>
public sealed class DiffVisibility
{
    /// <summary>The id of the layer these settings apply to.</summary>
    public string LayerId { get; set; } = string.Empty;

    /// <summary>Difficulty names where the layer is visible. Empty = visible on all diffs (unless hidden).</summary>
    public HashSet<string> VisibleDiffs { get; set; } = new();

    /// <summary>Difficulty names where the layer is hidden. Empty = no explicit hides.</summary>
    public HashSet<string> HiddenDiffs { get; set; } = new();

    /// <summary>Whether the layer is visible on the given difficulty.
    /// Visible if not in <see cref="HiddenDiffs"/> AND (<see cref="VisibleDiffs"/> is empty OR contains the diff name).
    /// Hidden wins when both sets contain the diff name.</summary>
    public bool IsVisibleOnDiff(string diffName)
    {
        if (HiddenDiffs.Contains(diffName)) return false;
        if (VisibleDiffs.Count == 0) return true;
        return VisibleDiffs.Contains(diffName);
    }

    public DiffVisibility Clone() => new()
    {
        LayerId = LayerId,
        VisibleDiffs = new HashSet<string>(VisibleDiffs),
        HiddenDiffs = new HashSet<string>(HiddenDiffs),
    };
}
