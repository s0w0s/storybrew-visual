using VisualCompositor.Audio.Model;

namespace VisualCompositor.Audio.State;

/// <summary>Actions for the audio reducer. Records keep payloads immutable.</summary>
public abstract record AudioAction(string Description);

/// <summary>Load a beatmap. Resets current time to 0 and stops playback.</summary>
public record LoadBeatmapAction(BeatmapInfo Beatmap) : AudioAction("Load Beatmap");

/// <summary>Start or pause playback.</summary>
public record PlayPauseAction(bool Play) : AudioAction(Play ? "Play" : "Pause");

/// <summary>Seek to a specific time in milliseconds.</summary>
public record SeekAction(double TimeMs) : AudioAction("Seek");

/// <summary>Set the beat snap division (1=quarter, 2=eighth, 4=sixteenth).</summary>
public record SetBeatSnapDivisionAction(int Division) : AudioAction("Set Beat Snap");

/// <summary>Set the master volume (0.0 to 1.0).</summary>
public record SetVolumeAction(float Volume) : AudioAction("Set Volume");
