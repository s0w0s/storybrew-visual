using VisualCompositor.Audio.Model;

namespace VisualCompositor.Audio.Hitsounds;

/// <summary>Maps beatmap hit objects to hitsound playback events. For each hit object whose
/// <see cref="HitObject.Additions"/> include whistle/finish/clap (or an explicit sample path),
/// a <see cref="HitsoundEvent"/> is produced at the object's start time.</summary>
public static class HitsoundMapper
{
    /// <summary>Map all hit objects in <paramref name="beatmap"/> to hitsound events.</summary>
    public static List<HitsoundEvent> Map(BeatmapInfo beatmap)
    {
        if (beatmap == null) return new List<HitsoundEvent>();
        return Map(beatmap.HitObjects);
    }

    /// <summary>Map a sequence of hit objects to hitsound events.</summary>
    public static List<HitsoundEvent> Map(IEnumerable<HitObject> hitObjects)
    {
        var events = new List<HitsoundEvent>();
        if (hitObjects == null) return events;

        foreach (var ho in hitObjects)
        {
            if (ho == null) continue;

            // An explicit sample path always produces an event (used for storyboard samples).
            if (!string.IsNullOrEmpty(ho.SamplePath))
            {
                events.Add(new HitsoundEvent
                {
                    Time = ho.StartTime,
                    SamplePath = ho.SamplePath,
                    Volume = NormalizeVolume(ho.Volume),
                });
                continue;
            }

            // Otherwise, only emit events for objects with non-normal additions.
            var additions = ho.Additions & ~HitSoundAddition.Normal;
            if (additions == HitSoundAddition.None) continue;

            foreach (var samplePath in ResolveSamplePaths(ho.SampleSet, additions, ho.CustomSampleSet))
            {
                events.Add(new HitsoundEvent
                {
                    Time = ho.StartTime,
                    SamplePath = samplePath,
                    Volume = NormalizeVolume(ho.Volume),
                });
            }
        }

        return events;
    }

    /// <summary>Resolve sample file paths for a sample set + additions combination. Multiple
    /// additions (e.g. Whistle|Clap) produce multiple sample paths. Custom sample sets append
    /// an index (e.g. "normal-hitwhistle2.wav").</summary>
    private static IEnumerable<string> ResolveSamplePaths(SampleSet sampleSet, HitSoundAddition additions, int customSampleSet)
    {
        var prefix = GetSampleSetPrefix(sampleSet);
        var suffix = customSampleSet > 0 ? customSampleSet.ToString() : null;

        if ((additions & HitSoundAddition.Whistle) != 0)
            yield return BuildSampleName(prefix, "hitwhistle", suffix);
        if ((additions & HitSoundAddition.Finish) != 0)
            yield return BuildSampleName(prefix, "hitfinish", suffix);
        if ((additions & HitSoundAddition.Clap) != 0)
            yield return BuildSampleName(prefix, "hitclap", suffix);
    }

    private static string BuildSampleName(string prefix, string soundName, string? suffix)
        => suffix == null ? $"{prefix}-{soundName}.wav" : $"{prefix}-{soundName}{suffix}.wav";

    private static string GetSampleSetPrefix(SampleSet sampleSet) => sampleSet switch
    {
        SampleSet.Normal => "normal",
        SampleSet.Soft => "soft",
        SampleSet.Drum => "drum",
        _ => "normal",
    };

    private static float NormalizeVolume(float volume0to100)
    {
        if (volume0to100 <= 0) return 0;
        if (volume0to100 >= 100) return 1.0f;
        return volume0to100 / 100.0f;
    }
}
