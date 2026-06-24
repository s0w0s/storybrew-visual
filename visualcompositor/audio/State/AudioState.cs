using VisualCompositor.Audio.Model;

namespace VisualCompositor.Audio.State;

/// <summary>Immutable audio-related editor state. Reducer produces new states, never mutates.</summary>
public sealed class AudioState
{
    /// <summary>The currently loaded beatmap, or null if none.</summary>
    public BeatmapInfo? Beatmap { get; init; }

    /// <summary>Whether audio playback is active.</summary>
    public bool IsPlaying { get; init; }

    /// <summary>Current playback position in milliseconds.</summary>
    public double CurrentTimeMs { get; init; }

    /// <summary>Beat snap division for the timeline grid (1=quarter, 2=eighth, 4=sixteenth). Default 4.</summary>
    public int BeatSnapDivision { get; init; } = 4;

    /// <summary>Master volume (0.0 to 1.0). Default 1.0.</summary>
    public float Volume { get; init; } = 1.0f;

    /// <summary>Audio duration in milliseconds, or 0 when unknown (no upper-bound clamping applied).</summary>
    public double DurationMs { get; init; }

    /// <summary>Initial empty state.</summary>
    public static AudioState Initial => new();

    /// <summary>Deep clone this audio state. The beatmap is cloned to preserve immutability.</summary>
    public AudioState Clone() => new()
    {
        Beatmap = Beatmap?.Clone(),
        IsPlaying = IsPlaying,
        CurrentTimeMs = CurrentTimeMs,
        BeatSnapDivision = BeatSnapDivision,
        Volume = Volume,
        DurationMs = DurationMs,
    };
}
