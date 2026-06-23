using VisualCompositor.Core.Model;
using VisualCompositor.Core.Primitives;

namespace VisualCompositor.Rendering.Sampling;

/// <summary>Samples sprite transform at a given time from its property tracks.</summary>
public static class SpriteSampler
{
    public static SpriteTransform SampleAtTime(SpriteDeclaration sprite, double time)
    {
        var transform = new SpriteTransform
        {
            Position = sprite.InitialPosition,
            Scale = Vector2.One,
            Rotation = 0f,
            Opacity = 1f,
            Color = Color3.White,
            Additive = false,
        };

        foreach (var track in sprite.PropertyTracks)
        {
            switch (track.PropertyName)
            {
                case "Position":
                    if (track.ValueType == "Vector2")
                        transform.Position = SampleVector2(track.Vector2Keyframes, time, transform.Position);
                    else if (track.ValueType == "Float")
                    {
                        // Single component (MX or MY)
                        var val = SampleFloat(track.FloatKeyframes, time,
                            track.ComponentMask == Vector2ComponentMask.X ? transform.Position.X : transform.Position.Y);
                        transform.Position = track.ComponentMask == Vector2ComponentMask.X
                            ? new Vector2(val, transform.Position.Y)
                            : new Vector2(transform.Position.X, val);
                    }
                    break;
                case "Scale":
                    if (track.ValueType == "Vector2")
                        transform.Scale = SampleVector2(track.Vector2Keyframes, time, transform.Scale);
                    else if (track.ValueType == "Float")
                    {
                        var val = SampleFloat(track.FloatKeyframes, time, transform.Scale.X);
                        transform.Scale = new Vector2(val, val); // S command: uniform scale
                    }
                    break;
                case "Rotation":
                    transform.Rotation = SampleFloat(track.FloatKeyframes, time, transform.Rotation);
                    break;
                case "Opacity":
                    transform.Opacity = SampleFloat(track.FloatKeyframes, time, transform.Opacity);
                    break;
                case "Color":
                    transform.Color = SampleColor(track.ColorKeyframes, time, transform.Color);
                    break;
            }
        }

        // Sample parameter track
        if (sprite.ParameterTrack != null)
        {
            foreach (var seg in sprite.ParameterTrack.Segments)
            {
                var endTime = seg.EndTime ?? double.MaxValue;
                if (time >= seg.StartTime && time <= endTime)
                {
                    if (seg.Parameter == ParameterType.AdditiveBlending)
                        transform.Additive = true;
                    // FlipH/FlipV would affect texture coordinates, handled by backend
                }
            }
        }

        return transform;
    }

    private static float SampleFloat(List<Keyframe<float>> keyframes, double time, float defaultValue)
    {
        if (keyframes.Count == 0) return defaultValue;
        if (keyframes.Count == 1) return keyframes[0].Value;
        // Sort by time (should already be sorted)
        var sorted = keyframes.OrderBy(k => k.Time).ToList();
        if (time <= sorted[0].Time) return sorted[0].Value;
        if (time >= sorted[^1].Time) return sorted[^1].Value;

        for (int i = 0; i < sorted.Count - 1; i++)
        {
            if (time >= sorted[i].Time && time <= sorted[i + 1].Time)
            {
                var t = (float)((time - sorted[i].Time) / (sorted[i + 1].Time - sorted[i].Time));
                return Lerp(sorted[i].Value, sorted[i + 1].Value, Ease(t, sorted[i].Easing));
            }
        }
        return sorted[^1].Value;
    }

    private static Vector2 SampleVector2(List<Keyframe<Vector2>> keyframes, double time, Vector2 defaultValue)
    {
        if (keyframes.Count == 0) return defaultValue;
        if (keyframes.Count == 1) return keyframes[0].Value;
        var sorted = keyframes.OrderBy(k => k.Time).ToList();
        if (time <= sorted[0].Time) return sorted[0].Value;
        if (time >= sorted[^1].Time) return sorted[^1].Value;

        for (int i = 0; i < sorted.Count - 1; i++)
        {
            if (time >= sorted[i].Time && time <= sorted[i + 1].Time)
            {
                var t = (float)((time - sorted[i].Time) / (sorted[i + 1].Time - sorted[i].Time));
                var eased = Ease(t, sorted[i].Easing);
                return new Vector2(
                    Lerp(sorted[i].Value.X, sorted[i + 1].Value.X, eased),
                    Lerp(sorted[i].Value.Y, sorted[i + 1].Value.Y, eased));
            }
        }
        return sorted[^1].Value;
    }

    private static Color3 SampleColor(List<Keyframe<Color3>> keyframes, double time, Color3 defaultValue)
    {
        if (keyframes.Count == 0) return defaultValue;
        if (keyframes.Count == 1) return keyframes[0].Value;
        var sorted = keyframes.OrderBy(k => k.Time).ToList();
        if (time <= sorted[0].Time) return sorted[0].Value;
        if (time >= sorted[^1].Time) return sorted[^1].Value;

        for (int i = 0; i < sorted.Count - 1; i++)
        {
            if (time >= sorted[i].Time && time <= sorted[i + 1].Time)
            {
                var t = (float)((time - sorted[i].Time) / (sorted[i + 1].Time - sorted[i].Time));
                var eased = Ease(t, sorted[i].Easing);
                return new Color3(
                    Lerp(sorted[i].Value.R, sorted[i + 1].Value.R, eased),
                    Lerp(sorted[i].Value.G, sorted[i + 1].Value.G, eased),
                    Lerp(sorted[i].Value.B, sorted[i + 1].Value.B, eased));
            }
        }
        return sorted[^1].Value;
    }

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;
    private static float Ease(float t, OsbEasing easing)
    {
        // Basic easing functions for common osu! easings
        return easing switch
        {
            OsbEasing.None => t,
            OsbEasing.In => t * t,
            OsbEasing.Out => 1 - (1 - t) * (1 - t),
            OsbEasing.InQuad => t * t,
            OsbEasing.OutQuad => 1 - (1 - t) * (1 - t),
            OsbEasing.InOutQuad => t < 0.5f ? 2 * t * t : 1 - 2 * (1 - t) * (1 - t),
            OsbEasing.InCubic => t * t * t,
            OsbEasing.OutCubic => 1 - (1 - t) * (1 - t) * (1 - t),
            OsbEasing.InOutCubic => t < 0.5f ? 4 * t * t * t : 1 - 4 * (1 - t) * (1 - t) * (1 - t),
            // Default to linear for other easings
            _ => t,
        };
    }
}

public sealed class SpriteTransform
{
    public Vector2 Position { get; set; }
    public Vector2 Scale { get; set; } = Vector2.One;
    public float Rotation { get; set; }
    public float Opacity { get; set; } = 1f;
    public Color3 Color { get; set; } = Color3.White;
    public bool Additive { get; set; }
}
