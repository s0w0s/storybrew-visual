namespace VisualCompositor.Audio.Backend;

/// <summary>Abstract audio playback backend. brewlib's audio engine would implement this on
/// Windows; a headless implementation is used for tests and environments without audio hardware.</summary>
public interface IAudioBackend
{
    /// <summary>Duration of the loaded audio in milliseconds. 0 if no audio loaded.</summary>
    double DurationMs { get; }

    /// <summary>Current playback position in milliseconds.</summary>
    double CurrentTimeMs { get; }

    /// <summary>Whether audio is currently playing.</summary>
    bool IsPlaying { get; }

    /// <summary>Load an audio file by path.</summary>
    void Load(string audioPath);

    /// <summary>Start playback from the current position.</summary>
    void Play();

    /// <summary>Pause playback.</summary>
    void Pause();

    /// <summary>Seek to a specific time in milliseconds.</summary>
    void Seek(double timeMs);

    /// <summary>Set volume (0.0 to 1.0).</summary>
    void SetVolume(float volume);
}
