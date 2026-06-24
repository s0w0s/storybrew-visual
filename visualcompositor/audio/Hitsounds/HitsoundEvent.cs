namespace VisualCompositor.Audio.Hitsounds;

/// <summary>A hitsound playback event at a specific time. Produced by mapping beatmap hit
/// objects to sample references.</summary>
public sealed class HitsoundEvent
{
    /// <summary>Time in milliseconds at which the sample should play.</summary>
    public double Time { get; set; }

    /// <summary>Resolved sample file path (e.g. "normal-hitwhistle.wav").</summary>
    public string SamplePath { get; set; } = string.Empty;

    /// <summary>Playback volume (0.0 to 1.0).</summary>
    public float Volume { get; set; } = 1.0f;

    /// <summary>Optional storyboard layer id this hitsound is associated with.</summary>
    public string? LayerId { get; set; }

    /// <summary>Deep clone this hitsound event.</summary>
    public HitsoundEvent Clone() => new()
    {
        Time = Time,
        SamplePath = SamplePath,
        Volume = Volume,
        LayerId = LayerId,
    };

    public override string ToString() => $"{(int)Time}ms: {SamplePath} @ {Volume}";
}
