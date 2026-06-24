using VisualCompositor.Core.Primitives;

namespace VisualCompositor.Core.Model.GraphEditor;

/// <summary>A track in the graph editor (one per property component, e.g., PositionX, PositionY,
/// Scale, Rotation, Opacity). Keyframes are kept sorted by Time.</summary>
public sealed class GraphTrack
{
    /// <summary>Stable identifier for the track (e.g., "spr_1.Position.X").</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Human-readable name (e.g., "Position X", "Opacity").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Keyframes sorted by Time.</summary>
    public List<GraphKeyframe> Keyframes { get; set; } = new();

    /// <summary>Get the curve segment between keyframe[index] and keyframe[index+1].
    /// Returns null if index is out of range or there is no next keyframe.</summary>
    public GraphCurve? GetCurve(int index)
    {
        if (index < 0 || index >= Keyframes.Count - 1) return null;
        var start = Keyframes[index];
        var end = Keyframes[index + 1];
        return new GraphCurve
        {
            StartTime = start.Time,
            EndTime = end.Time,
            StartValue = start.Value,
            EndValue = end.Value,
            Easing = start.Easing,
            StartHandles = start.Handles?.Clone(),
            EndHandles = end.Handles?.Clone(),
        };
    }

    /// <summary>Sample the track value at the given time. Before the first keyframe returns the
    /// first value; after the last keyframe returns the last value.</summary>
    public float SampleAt(double time)
    {
        if (Keyframes.Count == 0) return 0f;
        if (Keyframes.Count == 1 || time <= Keyframes[0].Time) return Keyframes[0].Value;
        if (time >= Keyframes[^1].Time) return Keyframes[^1].Value;

        for (var i = 0; i < Keyframes.Count - 1; i++)
        {
            var k1 = Keyframes[i];
            var k2 = Keyframes[i + 1];
            if (time >= k1.Time && time <= k2.Time)
            {
                var curve = GetCurve(i)!;
                return curve.SampleAt(time);
            }
        }
        return Keyframes[^1].Value;
    }

    /// <summary>Add a keyframe and re-sort by Time.</summary>
    public void AddKeyframe(GraphKeyframe keyframe)
    {
        Keyframes.Add(keyframe);
        Keyframes.Sort((a, b) => a.Time.CompareTo(b.Time));
    }

    /// <summary>Remove the keyframe at the given index.</summary>
    public void RemoveKeyframeAt(int index)
    {
        if (index < 0 || index >= Keyframes.Count) return;
        Keyframes.RemoveAt(index);
    }

    /// <summary>Replace the keyframe at the given index. Re-sorts by Time in case Time changed.</summary>
    public void UpdateKeyframe(int index, GraphKeyframe keyframe)
    {
        if (index < 0 || index >= Keyframes.Count) return;
        Keyframes[index] = keyframe;
        Keyframes.Sort((a, b) => a.Time.CompareTo(b.Time));
    }

    /// <summary>Deep clone.</summary>
    public GraphTrack Clone() => new()
    {
        Id = Id,
        Name = Name,
        Keyframes = Keyframes.Select(k => k.Clone()).ToList(),
    };
}
