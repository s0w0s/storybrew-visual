namespace VisualCompositor.Core.Model.Overrides;

/// <summary>Collection of visual overrides and per-difficulty visibility settings, stored in <see cref="CompositionDocument"/>.</summary>
public sealed class VisualOverrideCollection
{
    /// <summary>Visual overrides keyed by their target.</summary>
    public List<VisualOverride> Overrides { get; set; } = new();

    /// <summary>Per-difficulty visibility settings for layers.</summary>
    public List<DiffVisibility> DiffVisibilities { get; set; } = new();

    /// <summary>Gets the override for the given target type and id, or null.</summary>
    public VisualOverride? GetOverride(OverrideTargetType targetType, string targetId)
        => Overrides.FirstOrDefault(o => o.TargetType == targetType && o.TargetId == targetId);

    /// <summary>Sets an override, replacing any existing override for the same target.</summary>
    public void SetOverride(VisualOverride over)
    {
        var existing = Overrides.FirstOrDefault(o =>
            o.TargetType == over.TargetType && o.TargetId == over.TargetId);
        if (existing != null)
        {
            existing.Id = over.Id;
            existing.Visible = over.Visible;
            existing.OpacityMultiplier = over.OpacityMultiplier;
            existing.PositionOffset = over.PositionOffset;
            existing.ScaleMultiplier = over.ScaleMultiplier;
            existing.RotationOffset = over.RotationOffset;
            existing.Tint = over.Tint;
        }
        else
        {
            Overrides.Add(over.Clone());
        }
    }

    /// <summary>Removes the override with the given id.</summary>
    public void RemoveOverride(string id)
        => Overrides.RemoveAll(o => o.Id == id);

    /// <summary>Gets the per-difficulty visibility settings for the given layer, or null.</summary>
    public DiffVisibility? GetDiffVisibility(string layerId)
        => DiffVisibilities.FirstOrDefault(d => d.LayerId == layerId);

    /// <summary>Sets the per-difficulty visibility for a layer, replacing any existing entry.</summary>
    public void SetDiffVisibility(DiffVisibility visibility)
    {
        var existing = DiffVisibilities.FirstOrDefault(d => d.LayerId == visibility.LayerId);
        if (existing != null)
        {
            existing.VisibleDiffs = new HashSet<string>(visibility.VisibleDiffs);
            existing.HiddenDiffs = new HashSet<string>(visibility.HiddenDiffs);
        }
        else
        {
            DiffVisibilities.Add(visibility.Clone());
        }
    }

    /// <summary>Removes the per-difficulty visibility settings for the given layer.</summary>
    public void RemoveDiffVisibility(string layerId)
        => DiffVisibilities.RemoveAll(d => d.LayerId == layerId);

    /// <summary>Whether the collection is empty (no overrides and no diff visibilities).</summary>
    public bool IsEmpty => Overrides.Count == 0 && DiffVisibilities.Count == 0;

    public VisualOverrideCollection Clone() => new()
    {
        Overrides = Overrides.ConvertAll(o => o.Clone()),
        DiffVisibilities = DiffVisibilities.ConvertAll(d => d.Clone()),
    };
}
