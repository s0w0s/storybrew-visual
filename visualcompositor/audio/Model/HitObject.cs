using System.Globalization;
using VisualCompositor.Core.Primitives;

namespace VisualCompositor.Audio.Model;

/// <summary>Represents an osu! hit object for hitsound purposes. Curve details for sliders
/// are not retained; only the fields needed for hitsound mapping are kept.</summary>
public sealed class HitObject
{
    /// <summary>Bitmask: 1=circle, 2=slider, 4=newCombo, 8=spinner, 128=hold.</summary>
    private const int FlagCircle = 1;
    private const int FlagSlider = 2;
    private const int FlagNewCombo = 4;
    private const int FlagSpinner = 8;
    private const int FlagHold = 128;

    /// <summary>Start time in milliseconds.</summary>
    public double StartTime { get; set; }

    /// <summary>End time in milliseconds. Equal to <see cref="StartTime"/> for circles.</summary>
    public double EndTime { get; set; }

    /// <summary>Kind of hit object.</summary>
    public HitObjectKind Kind { get; set; }

    /// <summary>Playfield position (0-512, 0-384).</summary>
    public Vector2 Position { get; set; }

    /// <summary>Whether this object starts a new combo.</summary>
    public bool NewCombo { get; set; }

    /// <summary>Combo skip offset (bits 4-6 of the type field).</summary>
    public int ComboOffset { get; set; }

    /// <summary>Hit sound additions (whistle/finish/clap) layered on the normal sound.</summary>
    public HitSoundAddition Additions { get; set; }

    /// <summary>Sample set for the normal hit sound.</summary>
    public SampleSet SampleSet { get; set; } = SampleSet.Normal;

    /// <summary>Sample set for the hit sound additions.</summary>
    public SampleSet AdditionsSampleSet { get; set; } = SampleSet.Normal;

    /// <summary>Custom sample set index (0 = default).</summary>
    public int CustomSampleSet { get; set; }

    /// <summary>Volume (0-100).</summary>
    public float Volume { get; set; } = 100;

    /// <summary>Explicit sample file path, if specified on the hit object. Empty/null otherwise.</summary>
    public string? SamplePath { get; set; }

    /// <summary>Parse an osu! hit object line.
    /// Format: x,y,time,type,hitSound,objectParams,hitSample</summary>
    public static HitObject Parse(string line)
    {
        var values = line.Split(',');

        var x = int.Parse(values[0], CultureInfo.InvariantCulture);
        var y = int.Parse(values[1], CultureInfo.InvariantCulture);
        var startTime = double.Parse(values[2], CultureInfo.InvariantCulture);
        var type = int.Parse(values[3], CultureInfo.InvariantCulture);
        var additions = values.Length > 4
            ? (HitSoundAddition)int.Parse(values[4], CultureInfo.InvariantCulture)
            : HitSoundAddition.None;

        var kind = GetKind(type);
        var newCombo = (type & FlagNewCombo) != 0;
        var comboOffset = (type >> 4) & 7;

        var endTime = startTime;
        var sampleSet = SampleSet.Normal;
        var additionsSampleSet = SampleSet.Normal;
        var customSampleSet = 0;
        var volume = 100f;
        string? samplePath = null;

        // objectParams + hitSample positions depend on the kind.
        // Circle:  index 5 = hitSample
        // Slider:  index 5 = curve (ignored), index 6 = hitSample
        // Spinner: index 5 = endTime, index 6 = hitSample
        // Hold:    index 5 = endTime:hitSample (combined)
        int sampleIndex;
        switch (kind)
        {
            case HitObjectKind.Circle:
                sampleIndex = 5;
                break;
            case HitObjectKind.Slider:
                sampleIndex = 6;
                break;
            case HitObjectKind.Spinner:
                if (values.Length > 5)
                    endTime = double.Parse(values[5], CultureInfo.InvariantCulture);
                sampleIndex = 6;
                break;
            case HitObjectKind.Hold:
                // Hold: values[5] = "endTime:normalSet:additionSet:index:volume:filename"
                if (values.Length > 5)
                {
                    var holdParts = values[5].Split(':');
                    if (holdParts.Length > 0)
                        endTime = double.Parse(holdParts[0], CultureInfo.InvariantCulture);
                    if (holdParts.Length > 1)
                        sampleSet = (SampleSet)int.Parse(holdParts[1], CultureInfo.InvariantCulture);
                    if (holdParts.Length > 2)
                        additionsSampleSet = (SampleSet)int.Parse(holdParts[2], CultureInfo.InvariantCulture);
                    if (holdParts.Length > 3)
                        customSampleSet = int.Parse(holdParts[3], CultureInfo.InvariantCulture);
                    if (holdParts.Length > 4)
                        volume = int.Parse(holdParts[4], CultureInfo.InvariantCulture);
                    if (holdParts.Length > 5)
                        samplePath = holdParts[5];
                }
                return new HitObject
                {
                    StartTime = startTime,
                    EndTime = endTime,
                    Kind = kind,
                    Position = new Vector2(x, y),
                    NewCombo = newCombo,
                    ComboOffset = comboOffset,
                    Additions = additions,
                    SampleSet = sampleSet,
                    AdditionsSampleSet = additionsSampleSet,
                    CustomSampleSet = customSampleSet,
                    Volume = volume,
                    SamplePath = samplePath,
                };
            default:
                sampleIndex = 5;
                break;
        }

        // Parse hitSample (normalSet:additionSet:index:volume:filename) if present.
        if (values.Length > sampleIndex)
        {
            var sampleParts = values[sampleIndex].Split(':');
            if (sampleParts.Length > 0)
            {
                var objectSampleSet = (SampleSet)int.Parse(sampleParts[0], CultureInfo.InvariantCulture);
                if (objectSampleSet != SampleSet.None)
                {
                    sampleSet = objectSampleSet;
                    additionsSampleSet = objectSampleSet;
                }
            }
            if (sampleParts.Length > 1)
            {
                var objectAdditionsSampleSet = (SampleSet)int.Parse(sampleParts[1], CultureInfo.InvariantCulture);
                if (objectAdditionsSampleSet != SampleSet.None)
                    additionsSampleSet = objectAdditionsSampleSet;
            }
            if (sampleParts.Length > 2)
            {
                var objectCustomSampleSet = int.Parse(sampleParts[2], CultureInfo.InvariantCulture);
                if (objectCustomSampleSet != 0)
                    customSampleSet = objectCustomSampleSet;
            }
            if (sampleParts.Length > 3)
            {
                var objectVolume = int.Parse(sampleParts[3], CultureInfo.InvariantCulture);
                if (objectVolume > 0)
                    volume = objectVolume;
            }
            if (sampleParts.Length > 4)
                samplePath = sampleParts[4];
        }

        return new HitObject
        {
            StartTime = startTime,
            EndTime = endTime,
            Kind = kind,
            Position = new Vector2(x, y),
            NewCombo = newCombo,
            ComboOffset = comboOffset,
            Additions = additions,
            SampleSet = sampleSet,
            AdditionsSampleSet = additionsSampleSet,
            CustomSampleSet = customSampleSet,
            Volume = volume,
            SamplePath = samplePath,
        };
    }

    private static HitObjectKind GetKind(int type)
    {
        if ((type & FlagSpinner) != 0) return HitObjectKind.Spinner;
        if ((type & FlagHold) != 0) return HitObjectKind.Hold;
        if ((type & FlagSlider) != 0) return HitObjectKind.Slider;
        return HitObjectKind.Circle;
    }

    /// <summary>Deep clone this hit object.</summary>
    public HitObject Clone() => new()
    {
        StartTime = StartTime,
        EndTime = EndTime,
        Kind = Kind,
        Position = Position,
        NewCombo = NewCombo,
        ComboOffset = ComboOffset,
        Additions = Additions,
        SampleSet = SampleSet,
        AdditionsSampleSet = AdditionsSampleSet,
        CustomSampleSet = CustomSampleSet,
        Volume = Volume,
        SamplePath = SamplePath,
    };

    public override string ToString() => $"{(int)StartTime}, {Kind}{(NewCombo ? " [NewCombo]" : "")}";
}
