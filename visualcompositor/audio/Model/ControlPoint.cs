using System.Globalization;

namespace VisualCompositor.Audio.Model;

/// <summary>Represents an osu! timing/control point. A timing point (red line) carries the
/// beat duration; an inherited point (green line) carries a slider-velocity multiplier.</summary>
public sealed class ControlPoint
{
    /// <summary>Time in milliseconds at which this point becomes active.</summary>
    public double Offset { get; set; }

    /// <summary>Beat duration in milliseconds. Positive for timing points (red lines);
    /// negative for inherited points (green lines), where the magnitude encodes the
    /// slider-velocity multiplier (see <see cref="SliderMultiplier"/>).</summary>
    public double BeatDuration { get; set; } = 60000.0 / 120.0;

    /// <summary>Number of beats per measure (time signature numerator). Default 4.</summary>
    public int BeatPerMeasure { get; set; } = 4;

    /// <summary>Default sample set for hit objects under this point.</summary>
    public SampleSet SampleSet { get; set; } = SampleSet.Normal;

    /// <summary>Custom sample set index (0 = default skin samples).</summary>
    public int CustomSampleSet { get; set; }

    /// <summary>Volume (0-100) for hit objects under this point.</summary>
    public float Volume { get; set; } = 100;

    /// <summary>True for inherited points (green lines). False for timing points (red lines).</summary>
    public bool IsInherited { get; set; }

    /// <summary>Whether Kiai time is active at this point.</summary>
    public bool IsKiai { get; set; }

    /// <summary>Whether the first bar line should be omitted at this point.</summary>
    public bool OmitFirstBarLine { get; set; }

    /// <summary>Beats per minute derived from <see cref="BeatDuration"/>. 0 if not a timing point.</summary>
    public double Bpm => BeatDuration > 0 ? 60000.0 / BeatDuration : 0;

    /// <summary>Slider-velocity multiplier. For inherited points: -BeatDuration/100.
    /// For timing points: 1.0.</summary>
    public double SliderMultiplier => BeatDuration > 0 ? 1.0 : -BeatDuration / 100.0;

    /// <summary>Parse an osu! timing point line.
    /// Format: offset,beatDurationSV,beatPerMeasure,sampleSet,customSampleSet,volume,uninherited,effects</summary>
    public static ControlPoint Parse(string line)
    {
        var values = line.Split(',');
        if (values.Length < 2)
            throw new InvalidOperationException($"Control point has less than the 2 required parameters: {line}");

        var offset = double.Parse(values[0], CultureInfo.InvariantCulture);
        var beatDurationSV = double.Parse(values[1], CultureInfo.InvariantCulture);
        var beatPerMeasure = values.Length > 2 ? int.Parse(values[2], CultureInfo.InvariantCulture) : 4;
        var sampleSet = values.Length > 3 ? (SampleSet)int.Parse(values[3], CultureInfo.InvariantCulture) : SampleSet.Normal;
        var customSampleSet = values.Length > 4 ? int.Parse(values[4], CultureInfo.InvariantCulture) : 0;
        var volume = values.Length > 5 ? int.Parse(values[5], CultureInfo.InvariantCulture) : 100;

        // uninherited field: 1 = timing point (red), 0 = inherited (green).
        // If missing, infer from the sign of beatDurationSV (positive = timing, negative = inherited).
        bool isInherited;
        if (values.Length > 6)
        {
            var uninherited = int.Parse(values[6], CultureInfo.InvariantCulture);
            isInherited = uninherited == 0;
        }
        else
        {
            isInherited = beatDurationSV < 0;
        }

        var isKiai = false;
        var omitFirstBarLine = false;
        if (values.Length > 7)
        {
            var effects = int.Parse(values[7], CultureInfo.InvariantCulture);
            isKiai = (effects & 1) != 0;
            omitFirstBarLine = (effects & 8) != 0;
        }

        return new ControlPoint
        {
            Offset = offset,
            BeatDuration = beatDurationSV,
            BeatPerMeasure = beatPerMeasure,
            SampleSet = sampleSet,
            CustomSampleSet = customSampleSet,
            Volume = volume,
            IsInherited = isInherited,
            IsKiai = isKiai,
            OmitFirstBarLine = omitFirstBarLine,
        };
    }

    /// <summary>Deep clone this control point.</summary>
    public ControlPoint Clone() => new()
    {
        Offset = Offset,
        BeatDuration = BeatDuration,
        BeatPerMeasure = BeatPerMeasure,
        SampleSet = SampleSet,
        CustomSampleSet = CustomSampleSet,
        Volume = Volume,
        IsInherited = IsInherited,
        IsKiai = IsKiai,
        OmitFirstBarLine = OmitFirstBarLine,
    };

    public override string ToString()
        => (IsInherited
            ? $"{Offset}ms, {SliderMultiplier}x, {BeatPerMeasure}/4"
            : $"{Offset}ms, {Bpm}bpm, {BeatPerMeasure}/4") + (IsKiai ? " Kiai" : "");
}
