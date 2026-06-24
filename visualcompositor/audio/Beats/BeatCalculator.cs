using VisualCompositor.Audio.Model;

namespace VisualCompositor.Audio.Beats;

/// <summary>Static utility for beat-related calculations against osu! timing points.</summary>
public static class BeatCalculator
{
    /// <summary>Returns the beat duration (ms per beat) of a timing point. For inherited points
    /// (which carry no beat duration), returns 0.</summary>
    public static double GetBeatDuration(ControlPoint timingPoint)
        => timingPoint.IsInherited ? 0 : timingPoint.BeatDuration;

    /// <summary>Returns the BPM of a timing point. 0 for inherited points or invalid durations.</summary>
    public static double GetBpm(ControlPoint timingPoint)
        => timingPoint.IsInherited || timingPoint.BeatDuration <= 0 ? 0 : 60000.0 / timingPoint.BeatDuration;

    /// <summary>Snaps <paramref name="time"/> to the nearest beat-division boundary anchored at
    /// the timing point's offset. <paramref name="division"/>: 1 = quarter notes, 2 = eighths,
    /// 4 = sixteenths. Returns <paramref name="time"/> unchanged for inherited points or
    /// non-positive beat durations.</summary>
    public static double SnapToBeat(double time, double beatDuration, int beatPerMeasure, int division)
    {
        if (beatDuration <= 0 || division <= 0)
            return time;
        var divisionDuration = beatDuration / division;
        var snappedBeats = Math.Round(time / divisionDuration);
        return snappedBeats * divisionDuration;
    }

    /// <summary>Returns the 0-indexed beat number within the current measure for
    /// <paramref name="time"/> relative to <paramref name="timingPoint"/>. Returns 0 for
    /// inherited points or non-positive beat durations.</summary>
    public static int GetBeatNumber(double time, ControlPoint timingPoint)
    {
        if (timingPoint.IsInherited || timingPoint.BeatDuration <= 0)
            return 0;
        var beatsPerMeasure = timingPoint.BeatPerMeasure <= 0 ? 4 : timingPoint.BeatPerMeasure;
        var relativeTime = time - timingPoint.Offset;
        var beatIndex = (int)Math.Floor(relativeTime / timingPoint.BeatDuration);
        return ((beatIndex % beatsPerMeasure) + beatsPerMeasure) % beatsPerMeasure;
    }

    /// <summary>Returns the measure number (0-indexed) since the timing point for
    /// <paramref name="time"/>. Returns 0 for inherited points or non-positive beat durations.</summary>
    public static int GetMeasureNumber(double time, ControlPoint timingPoint)
    {
        if (timingPoint.IsInherited || timingPoint.BeatDuration <= 0)
            return 0;
        var beatsPerMeasure = timingPoint.BeatPerMeasure <= 0 ? 4 : timingPoint.BeatPerMeasure;
        var relativeTime = time - timingPoint.Offset;
        var beatIndex = (int)Math.Floor(relativeTime / timingPoint.BeatDuration);
        if (beatIndex < 0)
            return 0;
        return beatIndex / beatsPerMeasure;
    }

    /// <summary>Returns the next beat boundary at or after <paramref name="time"/>, anchored at
    /// the timing point's offset. Returns <paramref name="time"/> for inherited points or
    /// non-positive beat durations.</summary>
    public static double GetNextBeatTime(double time, ControlPoint timingPoint)
    {
        if (timingPoint.IsInherited || timingPoint.BeatDuration <= 0)
            return time;
        var relativeTime = time - timingPoint.Offset;
        var beatIndex = Math.Ceiling(relativeTime / timingPoint.BeatDuration);
        return timingPoint.Offset + beatIndex * timingPoint.BeatDuration;
    }

    /// <summary>Returns the previous beat boundary at or before <paramref name="time"/>, anchored
    /// at the timing point's offset. Returns <paramref name="time"/> for inherited points or
    /// non-positive beat durations.</summary>
    public static double GetPreviousBeatTime(double time, ControlPoint timingPoint)
    {
        if (timingPoint.IsInherited || timingPoint.BeatDuration <= 0)
            return time;
        var relativeTime = time - timingPoint.Offset;
        var beatIndex = Math.Floor(relativeTime / timingPoint.BeatDuration);
        return timingPoint.Offset + beatIndex * timingPoint.BeatDuration;
    }
}
