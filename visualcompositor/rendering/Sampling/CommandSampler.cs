using VisualCompositor.Core.Model;
using VisualCompositor.Core.Primitives;

namespace VisualCompositor.Rendering.Sampling;

/// <summary>Computes sprite state at a given time by sampling property tracks and parameter tracks.
/// Also expands Loop/Trigger blocks to compute their effect at time T.</summary>
public sealed class CommandSampler
{
    public SpriteState SampleSpriteState(SpriteDeclaration sprite, double time)
    {
        var state = new SpriteState
        {
            Position = sprite.InitialPosition,
            Scale = Vector2.One,
            Rotation = 0,
            Opacity = 1,
            Color = Color3.White,
        };

        // Sample each property track
        foreach (var track in sprite.PropertyTracks)
        {
            switch (track.PropertyName)
            {
                case "Position":
                    if (track.Vector2Keyframes.Count > 0)
                        state.Position = SampleVector2(track.Vector2Keyframes, time);
                    break;
                case "Scale":
                    if (track.Vector2Keyframes.Count > 0)
                        state.Scale = SampleVector2(track.Vector2Keyframes, time);
                    else if (track.FloatKeyframes.Count > 0)
                    {
                        var s = SampleFloat(track.FloatKeyframes, time);
                        state.Scale = new Vector2(s, s);
                    }
                    break;
                case "Rotation":
                    if (track.FloatKeyframes.Count > 0)
                        state.Rotation = SampleFloat(track.FloatKeyframes, time);
                    break;
                case "Opacity":
                    if (track.FloatKeyframes.Count > 0)
                        state.Opacity = SampleFloat(track.FloatKeyframes, time);
                    break;
                case "Color":
                    if (track.ColorKeyframes.Count > 0)
                        state.Color = SampleColor(track.ColorKeyframes, time);
                    break;
            }
        }

        // Sample parameter track
        if (sprite.ParameterTrack != null)
        {
            foreach (var seg in sprite.ParameterTrack.Segments)
            {
                bool isActive = IsSegmentActive(seg, time);
                if (isActive)
                {
                    switch (seg.Parameter)
                    {
                        case ParameterType.AdditiveBlending: state.Additive = true; break;
                        case ParameterType.FlipHorizontal: state.FlipH = true; break;
                        case ParameterType.FlipVertical: state.FlipV = true; break;
                    }
                }
            }
        }

        // Sample Loop/Trigger blocks
        foreach (var block in sprite.Blocks)
        {
            if (block is LoopBlock loop)
            {
                SampleLoopBlock(loop, time, state);
            }
            else if (block is TriggerBlock trigger)
            {
                SampleTriggerBlock(trigger, time, state);
            }
        }

        return state;
    }

    private bool IsSegmentActive(ParameterSegment seg, double time)
    {
        if (time < seg.StartTime) return false;
        if (seg.EndTime.HasValue && time > seg.EndTime.Value) return false;
        // Open-ended segments are active from start time onwards
        return true;
    }

    private void SampleLoopBlock(LoopBlock loop, double time, SpriteState state)
    {
        // Loop starts at loop.StartTime, repeats LoopCount times
        var loopDuration = GetLoopDuration(loop);
        if (loopDuration <= 0) return;

        var elapsed = time - loop.StartTime;
        if (elapsed < 0) return;

        var iteration = (int)(elapsed / loopDuration);
        if (iteration >= loop.LoopCount) return;

        var iterationTime = elapsed - (iteration * loopDuration);
        ApplyRelativeCommands(loop.RelativeCommands, iterationTime, state);
    }

    private void SampleTriggerBlock(TriggerBlock trigger, double time, SpriteState state)
    {
        // Triggers activate when time is within [StartTime, EndTime]
        // For preview, we apply trigger commands if time is in range
        if (time < trigger.StartTime || time > trigger.EndTime) return;
        ApplyRelativeCommands(trigger.RelativeCommands, time - trigger.StartTime, state);
    }

    private double GetLoopDuration(LoopBlock loop)
    {
        if (loop.RelativeCommands.Count == 0) return 0;
        var maxEnd = loop.RelativeCommands.Max(c => c.EndTime);
        return maxEnd;
    }

    private void ApplyRelativeCommands(List<RelativeCommand> commands, double relTime, SpriteState state)
    {
        foreach (var cmd in commands)
        {
            if (relTime < cmd.StartTime) continue;
            var value = InterpolateValue(cmd, relTime);
            ApplyCommandValue(cmd.CommandType, value, cmd.StartValue, cmd.EndValue, state);
        }
    }

    private double InterpolateValue(RelativeCommand cmd, double time)
    {
        if (cmd.EndTime <= cmd.StartTime) return 0;
        var t = (time - cmd.StartTime) / (cmd.EndTime - cmd.StartTime);
        t = Math.Clamp(t, 0, 1);
        return Ease(t, cmd.Easing);
    }

    private void ApplyCommandValue(string commandType, double t, string startValue, string endValue, SpriteState state)
    {
        // Parse and apply based on command type
        switch (commandType)
        {
            case "F":
                {
                    if (double.TryParse(startValue, out var s) && double.TryParse(endValue, out var e))
                        state.Opacity = (float)(s + (e - s) * t);
                    break;
                }
            case "R":
                {
                    if (double.TryParse(startValue, out var s) && double.TryParse(endValue, out var e))
                        state.Rotation = (float)(s + (e - s) * t);
                    break;
                }
            case "S":
                {
                    if (double.TryParse(startValue, out var s) && double.TryParse(endValue, out var e))
                    {
                        var v = (float)(s + (e - s) * t);
                        state.Scale = new Vector2(v, v);
                    }
                    break;
                }
            // M, MX, MY, V, C, P require more complex parsing - basic implementation for MVP
        }
    }

    private float SampleFloat(List<Keyframe<float>> keyframes, double time)
    {
        if (keyframes.Count == 0) return 0;
        if (keyframes.Count == 1 || time <= keyframes[0].Time) return keyframes[0].Value;
        if (time >= keyframes[^1].Time) return keyframes[^1].Value;

        for (int i = 0; i < keyframes.Count - 1; i++)
        {
            var k1 = keyframes[i];
            var k2 = keyframes[i + 1];
            if (time >= k1.Time && time <= k2.Time)
            {
                if (k2.Time <= k1.Time) return k1.Value;
                var t = (time - k1.Time) / (k2.Time - k1.Time);
                t = Ease(t, k2.Easing);
                return (float)(k1.Value + (k2.Value - k1.Value) * t);
            }
        }
        return keyframes[^1].Value;
    }

    private Vector2 SampleVector2(List<Keyframe<Vector2>> keyframes, double time)
    {
        if (keyframes.Count == 0) return Vector2.Zero;
        if (keyframes.Count == 1 || time <= keyframes[0].Time) return keyframes[0].Value;
        if (time >= keyframes[^1].Time) return keyframes[^1].Value;

        for (int i = 0; i < keyframes.Count - 1; i++)
        {
            var k1 = keyframes[i];
            var k2 = keyframes[i + 1];
            if (time >= k1.Time && time <= k2.Time)
            {
                if (k2.Time <= k1.Time) return k1.Value;
                var t = (time - k1.Time) / (k2.Time - k1.Time);
                t = Ease(t, k2.Easing);
                return new Vector2(
                    (float)(k1.Value.X + (k2.Value.X - k1.Value.X) * t),
                    (float)(k1.Value.Y + (k2.Value.Y - k1.Value.Y) * t));
            }
        }
        return keyframes[^1].Value;
    }

    private Color3 SampleColor(List<Keyframe<Color3>> keyframes, double time)
    {
        if (keyframes.Count == 0) return Color3.White;
        if (keyframes.Count == 1 || time <= keyframes[0].Time) return keyframes[0].Value;
        if (time >= keyframes[^1].Time) return keyframes[^1].Value;

        for (int i = 0; i < keyframes.Count - 1; i++)
        {
            var k1 = keyframes[i];
            var k2 = keyframes[i + 1];
            if (time >= k1.Time && time <= k2.Time)
            {
                if (k2.Time <= k1.Time) return k1.Value;
                var t = (time - k1.Time) / (k2.Time - k1.Time);
                t = Ease(t, k2.Easing);
                return new Color3(
                    (float)(k1.Value.R + (k2.Value.R - k1.Value.R) * t),
                    (float)(k1.Value.G + (k2.Value.G - k1.Value.G) * t),
                    (float)(k1.Value.B + (k2.Value.B - k1.Value.B) * t));
            }
        }
        return keyframes[^1].Value;
    }

    private static double Ease(double t, OsbEasing easing)
    {
        // Basic easing implementations for common types
        return easing switch
        {
            OsbEasing.None => t,
            OsbEasing.In => t * t,
            OsbEasing.Out => 1 - (1 - t) * (1 - t),
            OsbEasing.InQuad => t * t,
            OsbEasing.OutQuad => 1 - (1 - t) * (1 - t),
            OsbEasing.InOutQuad => t < 0.5 ? 2 * t * t : 1 - Math.Pow(-2 * t + 2, 2) / 2,
            OsbEasing.InCubic => t * t * t,
            OsbEasing.OutCubic => 1 - Math.Pow(1 - t, 3),
            OsbEasing.InOutCubic => t < 0.5 ? 4 * t * t * t : 1 - Math.Pow(-2 * t + 2, 3) / 2,
            _ => t, // Fallback to linear for unimplemented easings
        };
    }
}

public sealed class SpriteState
{
    public Vector2 Position { get; set; }
    public Vector2 Scale { get; set; } = Vector2.One;
    public float Rotation { get; set; }
    public float Opacity { get; set; } = 1;
    public Color3 Color { get; set; } = Color3.White;
    public bool Additive { get; set; }
    public bool FlipH { get; set; }
    public bool FlipV { get; set; }
}
