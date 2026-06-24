namespace VisualCompositor.Audio.Backend;

/// <summary>No-op audio backend for testing and headless environments. Tracks duration, position,
/// and playing state but produces no actual sound. <see cref="Play"/> advances a simulated clock
/// via <see cref="Tick"/> so tests can drive playback deterministically.</summary>
public sealed class HeadlessAudioBackend : IAudioBackend
{
    /// <summary>Simulated duration in milliseconds. Set via <see cref="Load"/> (parsed from the
    /// file name as "&lt;duration&gt;.ext") or directly for tests. Default 60000.</summary>
    public double DurationMs { get; private set; }

    /// <summary>Current simulated playback position in milliseconds.</summary>
    public double CurrentTimeMs { get; private set; }

    /// <summary>Whether simulated playback is active.</summary>
    public bool IsPlaying { get; private set; }

    /// <summary>Loaded audio path, or null if none.</summary>
    public string? LoadedPath { get; private set; }

    /// <summary>Current volume (0.0 to 1.0).</summary>
    public float Volume { get; private set; } = 1.0f;

    /// <summary>Load an audio file by path. If the file name (without extension) parses as a
    /// number, that number is used as the duration in ms; otherwise defaults to 60000.</summary>
    public void Load(string audioPath)
    {
        LoadedPath = audioPath;
        IsPlaying = false;
        CurrentTimeMs = 0;
        DurationMs = ParseDurationFromPath(audioPath);
    }

    /// <summary>Start playback from the current position.</summary>
    public void Play()
    {
        if (DurationMs <= 0) return;
        IsPlaying = true;
    }

    /// <summary>Pause playback.</summary>
    public void Pause() => IsPlaying = false;

    /// <summary>Seek to a specific time in milliseconds, clamped to [0, DurationMs].</summary>
    public void Seek(double timeMs)
    {
        if (timeMs < 0) timeMs = 0;
        if (DurationMs > 0 && timeMs > DurationMs) timeMs = DurationMs;
        CurrentTimeMs = timeMs;
    }

    /// <summary>Set volume (0.0 to 1.0), clamped.</summary>
    public void SetVolume(float volume)
    {
        if (volume < 0) volume = 0;
        if (volume > 1) volume = 1;
        Volume = volume;
    }

    /// <summary>Advance the simulated clock by <paramref name="deltaMs"/> milliseconds. If
    /// playback reaches the end, <see cref="IsPlaying"/> becomes false and position clamps to
    /// <see cref="DurationMs"/>. No-op when not playing.</summary>
    public void Tick(double deltaMs)
    {
        if (!IsPlaying) return;
        CurrentTimeMs += deltaMs;
        if (DurationMs > 0 && CurrentTimeMs >= DurationMs)
        {
            CurrentTimeMs = DurationMs;
            IsPlaying = false;
        }
    }

    private static double ParseDurationFromPath(string audioPath)
    {
        if (string.IsNullOrEmpty(audioPath)) return 60000;
        var name = Path.GetFileNameWithoutExtension(audioPath);
        if (double.TryParse(name, out var d) && d > 0)
            return d;
        return 60000;
    }
}
