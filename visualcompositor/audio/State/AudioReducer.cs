using VisualCompositor.Audio.Model;

namespace VisualCompositor.Audio.State;

/// <summary>Pure reducer for <see cref="AudioState"/>. Never mutates input state.</summary>
public static class AudioReducer
{
    /// <summary>Reduce an action against the current state, producing a new state.</summary>
    public static AudioState Reduce(AudioState state, object action)
    {
        return action switch
        {
            LoadBeatmapAction a => ReduceLoadBeatmap(state, a),
            PlayPauseAction a => ReducePlayPause(state, a),
            SeekAction a => ReduceSeek(state, a),
            SetBeatSnapDivisionAction a => ReduceSetBeatSnapDivision(state, a),
            SetVolumeAction a => ReduceSetVolume(state, a),
            _ => state,
        };
    }

    private static AudioState ReduceLoadBeatmap(AudioState state, LoadBeatmapAction a)
        => new()
        {
            Beatmap = a.Beatmap?.Clone(),
            IsPlaying = false,
            CurrentTimeMs = 0,
            BeatSnapDivision = state.BeatSnapDivision,
            Volume = state.Volume,
            DurationMs = state.DurationMs,
        };

    private static AudioState ReducePlayPause(AudioState state, PlayPauseAction a)
        => Copy(state, isPlaying: a.Play);

    private static AudioState ReduceSeek(AudioState state, SeekAction a)
    {
        var time = a.TimeMs;
        if (time < 0) time = 0;
        if (state.DurationMs > 0 && time > state.DurationMs) time = state.DurationMs;
        return Copy(state, currentTimeMs: time);
    }

    private static AudioState ReduceSetBeatSnapDivision(AudioState state, SetBeatSnapDivisionAction a)
    {
        var division = a.Division;
        if (division < 1) division = 1;
        return Copy(state, beatSnapDivision: division);
    }

    private static AudioState ReduceSetVolume(AudioState state, SetVolumeAction a)
    {
        var volume = a.Volume;
        if (volume < 0) volume = 0;
        if (volume > 1) volume = 1;
        return Copy(state, volume: volume);
    }

    private static AudioState Copy(AudioState state,
        BeatmapInfo? beatmap = null,
        bool? isPlaying = null,
        double? currentTimeMs = null,
        int? beatSnapDivision = null,
        float? volume = null) => new()
        {
            Beatmap = beatmap ?? state.Beatmap,
            IsPlaying = isPlaying ?? state.IsPlaying,
            CurrentTimeMs = currentTimeMs ?? state.CurrentTimeMs,
            BeatSnapDivision = beatSnapDivision ?? state.BeatSnapDivision,
            Volume = volume ?? state.Volume,
            DurationMs = state.DurationMs,
        };
}
