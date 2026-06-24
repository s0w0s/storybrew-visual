namespace VisualCompositor.Audio.Model;

/// <summary>Lightweight beatmap metadata + timing + hit objects, sufficient for audio/beat/hitsound
/// support in the visual compositor. Does not depend on brewlib.</summary>
public sealed class BeatmapInfo
{
    /// <summary>Beatmap title (from [Metadata] Title).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Difficulty name / version (from [Metadata] Version).</summary>
    public string DifficultyName { get; set; } = string.Empty;

    /// <summary>Audio filename (from [General] AudioFilename).</summary>
    public string AudioFilename { get; set; } = string.Empty;

    /// <summary>Background image path (from [Events] 0,0,"path",...).</summary>
    public string BackgroundPath { get; set; } = string.Empty;

    /// <summary>BPM derived from the first timing point. 0 if no timing points.</summary>
    public double Bpm { get; set; }

    /// <summary>All control points (red + green lines), sorted by offset.</summary>
    public List<ControlPoint> ControlPoints { get; set; } = new();

    /// <summary>All hit objects, in file order.</summary>
    public List<HitObject> HitObjects { get; set; } = new();

    /// <summary>Bookmark timestamps in milliseconds (from [Editor] Bookmarks).</summary>
    public List<int> Bookmarks { get; set; } = new();

    /// <summary>All timing points (red lines only), sorted by offset.</summary>
    public IEnumerable<ControlPoint> TimingPoints => ControlPoints.Where(cp => !cp.IsInherited);

    /// <summary>Returns the active control point (red or green line) at <paramref name="time"/>,
    /// i.e. the latest point with Offset &lt;= time. Returns null if none.</summary>
    public ControlPoint? GetControlPointAt(double time)
    {
        ControlPoint? result = null;
        foreach (var cp in ControlPoints)
        {
            if (cp.Offset <= time)
                result = cp;
            else
                break;
        }
        return result;
    }

    /// <summary>Returns the active timing point (red line) at <paramref name="time"/>,
    /// i.e. the latest non-inherited point with Offset &lt;= time. Returns null if none.</summary>
    public ControlPoint? GetTimingPointAt(double time)
    {
        ControlPoint? result = null;
        foreach (var cp in ControlPoints)
        {
            if (cp.IsInherited) continue;
            if (cp.Offset <= time)
                result = cp;
            else
                break;
        }
        return result;
    }

    /// <summary>Returns the beat duration (ms per beat) at <paramref name="time"/>. Uses the
    /// active timing point's BeatDuration. Returns 0 if no timing point is active.</summary>
    public double GetBeatDurationAt(double time)
    {
        var timing = GetTimingPointAt(time);
        return timing == null ? 0 : timing.BeatDuration;
    }

    /// <summary>Snaps <paramref name="time"/> to the nearest beat division boundary of the
    /// active timing point. <paramref name="beatDivision"/>: 1 = quarter notes, 2 = eighths,
    /// 4 = sixteenths. Returns the original time if no timing point is active.</summary>
    public double SnapToBeat(double time, int beatDivision)
    {
        var timing = GetTimingPointAt(time);
        if (timing == null || timing.BeatDuration <= 0)
            return time;
        return SnapToBeat(time, timing.Offset, timing.BeatDuration, timing.BeatPerMeasure, beatDivision);
    }

    /// <summary>Internal snap helper anchored at a specific timing point offset.</summary>
    private static double SnapToBeat(double time, double timingOffset, double beatDuration, int beatPerMeasure, int beatDivision)
    {
        if (beatDuration <= 0 || beatDivision <= 0)
            return time;

        var divisionDuration = beatDuration / beatDivision;
        var relativeTime = time - timingOffset;
        var snappedBeats = Math.Round(relativeTime / divisionDuration);
        return timingOffset + snappedBeats * divisionDuration;
    }

    /// <summary>Deep clone this beatmap info.</summary>
    public BeatmapInfo Clone() => new()
    {
        Name = Name,
        DifficultyName = DifficultyName,
        AudioFilename = AudioFilename,
        BackgroundPath = BackgroundPath,
        Bpm = Bpm,
        ControlPoints = ControlPoints.Select(cp => cp.Clone()).ToList(),
        HitObjects = HitObjects.Select(ho => ho.Clone()).ToList(),
        Bookmarks = new List<int>(Bookmarks),
    };
}
