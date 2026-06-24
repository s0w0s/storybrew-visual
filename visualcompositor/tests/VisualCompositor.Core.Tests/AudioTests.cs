using VisualCompositor.Audio.Backend;
using VisualCompositor.Audio.Beats;
using VisualCompositor.Audio.Hitsounds;
using VisualCompositor.Audio.Model;
using VisualCompositor.Audio.Parsing;
using VisualCompositor.Audio.State;
using VisualCompositor.Core.Primitives;
using Xunit;

namespace VisualCompositor.Core.Tests;

public class AudioTests
{
    // ---------------- ControlPoint.Parse ----------------

    [Fact]
    public void ControlPoint_Parse_TimingPoint_PositiveBeatDuration()
    {
        var cp = ControlPoint.Parse("1000,500,4,1,0,80,1,0");
        Assert.Equal(1000, cp.Offset);
        Assert.Equal(500, cp.BeatDuration);
        Assert.Equal(4, cp.BeatPerMeasure);
        Assert.Equal(SampleSet.Normal, cp.SampleSet);
        Assert.Equal(0, cp.CustomSampleSet);
        Assert.Equal(80, cp.Volume);
        Assert.False(cp.IsInherited);
        Assert.False(cp.IsKiai);
        Assert.False(cp.OmitFirstBarLine);
    }

    [Fact]
    public void ControlPoint_Parse_InheritedPoint_NegativeBeatDuration()
    {
        var cp = ControlPoint.Parse("2000,-200,4,1,0,80,0,0");
        Assert.Equal(2000, cp.Offset);
        Assert.Equal(-200, cp.BeatDuration);
        Assert.True(cp.IsInherited);
        Assert.Equal(2.0, cp.SliderMultiplier);
    }

    [Fact]
    public void ControlPoint_Parse_Kiai_EffectBit1()
    {
        var cp = ControlPoint.Parse("0,500,4,1,0,100,1,1");
        Assert.True(cp.IsKiai);
        Assert.False(cp.OmitFirstBarLine);
    }

    [Fact]
    public void ControlPoint_Parse_OmitFirstBarLine_EffectBit8()
    {
        var cp = ControlPoint.Parse("0,500,4,1,0,100,1,8");
        Assert.False(cp.IsKiai);
        Assert.True(cp.OmitFirstBarLine);
    }

    [Fact]
    public void ControlPoint_Parse_InheritsIsInheritedFromSign_WhenFieldMissing()
    {
        // No uninherited field (only 6 values). Negative beatDuration => inherited.
        var cp = ControlPoint.Parse("500,-100,4,1,0,90");
        Assert.True(cp.IsInherited);
        // Positive beatDuration => timing point.
        var cp2 = ControlPoint.Parse("500,500,4,1,0,90");
        Assert.False(cp2.IsInherited);
    }

    [Fact]
    public void ControlPoint_Bpm_ComputedFromBeatDuration()
    {
        // 500ms/beat => 120 BPM
        var cp = ControlPoint.Parse("0,500,4,1,0,100,1,0");
        Assert.Equal(120.0, cp.Bpm, 2);
        // 250ms/beat => 240 BPM
        var cp2 = ControlPoint.Parse("0,250,4,1,0,100,1,0");
        Assert.Equal(240.0, cp2.Bpm, 2);
    }

    [Fact]
    public void ControlPoint_Bpm_ZeroForInheritedPoint()
    {
        var cp = ControlPoint.Parse("0,-100,4,1,0,100,0,0");
        Assert.Equal(0, cp.Bpm);
    }

    [Fact]
    public void ControlPoint_Clone_RoundTripsAllFields()
    {
        var cp = new ControlPoint
        {
            Offset = 1234,
            BeatDuration = 450,
            BeatPerMeasure = 3,
            SampleSet = SampleSet.Soft,
            CustomSampleSet = 2,
            Volume = 70,
            IsInherited = false,
            IsKiai = true,
            OmitFirstBarLine = true,
        };
        var clone = cp.Clone();

        Assert.NotSame(cp, clone);
        Assert.Equal(cp.Offset, clone.Offset);
        Assert.Equal(cp.BeatDuration, clone.BeatDuration);
        Assert.Equal(cp.BeatPerMeasure, clone.BeatPerMeasure);
        Assert.Equal(cp.SampleSet, clone.SampleSet);
        Assert.Equal(cp.CustomSampleSet, clone.CustomSampleSet);
        Assert.Equal(cp.Volume, clone.Volume);
        Assert.Equal(cp.IsInherited, clone.IsInherited);
        Assert.Equal(cp.IsKiai, clone.IsKiai);
        Assert.Equal(cp.OmitFirstBarLine, clone.OmitFirstBarLine);
    }

    // ---------------- HitObject.Parse ----------------

    [Fact]
    public void HitObject_Parse_Circle()
    {
        // x,y,time,type(1=circle),hitSound(0)
        var ho = HitObject.Parse("100,200,1500,1,0");
        Assert.Equal(1500, ho.StartTime);
        Assert.Equal(1500, ho.EndTime);
        Assert.Equal(HitObjectKind.Circle, ho.Kind);
        Assert.False(ho.NewCombo);
        Assert.Equal(new Vector2(100, 200), ho.Position);
    }

    [Fact]
    public void HitObject_Parse_Circle_WithNewComboAndWhistle()
    {
        // type=1|4=5 (circle + newCombo), hitSound=2 (whistle)
        var ho = HitObject.Parse("100,200,1500,5,2");
        Assert.Equal(HitObjectKind.Circle, ho.Kind);
        Assert.True(ho.NewCombo);
        Assert.Equal(HitSoundAddition.Whistle, ho.Additions);
    }

    [Fact]
    public void HitObject_Parse_Slider()
    {
        // type=2 (slider), curve params: B|100:200|300:400,slides,length, then hitSample
        var ho = HitObject.Parse("50,100,2000,2,0,B|100:200|300:400,1,150,0:0:0:0:");
        Assert.Equal(2000, ho.StartTime);
        Assert.Equal(HitObjectKind.Slider, ho.Kind);
        Assert.False(ho.NewCombo);
    }

    [Fact]
    public void HitObject_Parse_Spinner_HasEndTime()
    {
        // type=8 (spinner), endTime at index 5
        var ho = HitObject.Parse("256,192,3000,8,0,4000");
        Assert.Equal(HitObjectKind.Spinner, ho.Kind);
        Assert.Equal(3000, ho.StartTime);
        Assert.Equal(4000, ho.EndTime);
    }

    [Fact]
    public void HitObject_Parse_ComboOffset_FromTypeBits()
    {
        // type = 1 (circle) | 4 (newCombo) | (3 << 4) = 1+4+48 = 53
        var ho = HitObject.Parse("100,200,1500,53,0");
        Assert.True(ho.NewCombo);
        Assert.Equal(3, ho.ComboOffset);
    }

    // ---------------- BeatmapParser ----------------

    private static readonly string SampleOsu = """
osu file format v14

[General]
AudioFilename: audio.mp3
AudioLeadIn: 0

[Editor]
Bookmarks: 1000,2000,3000

[Metadata]
Title:Test Song
Version:Hard

[Difficulty]
HPDrainRate:6

[TimingPoints]
0,500,4,1,0,100,1,0
2000,-100,4,1,0,80,0,0

[HitObjects]
100,200,500,1,0
150,250,1000,5,2
256,192,3000,8,0,4000

[Events]
0,0,"bg.jpg",0,0
""";

    [Fact]
    public void BeatmapParser_ParsesGeneralAndMetadata()
    {
        var parser = new BeatmapParser();
        var result = parser.Parse(SampleOsu);

        Assert.Equal("audio.mp3", result.Beatmap.AudioFilename);
        Assert.Equal("Test Song", result.Beatmap.Name);
        Assert.Equal("Hard", result.Beatmap.DifficultyName);
        Assert.Equal("bg.jpg", result.Beatmap.BackgroundPath);
    }

    [Fact]
    public void BeatmapParser_ParsesTimingPointsAndHitObjects()
    {
        var parser = new BeatmapParser();
        var result = parser.Parse(SampleOsu);

        Assert.Equal(2, result.Beatmap.ControlPoints.Count);
        Assert.False(result.Beatmap.ControlPoints[0].IsInherited);
        Assert.True(result.Beatmap.ControlPoints[1].IsInherited);
        Assert.Equal(3, result.Beatmap.HitObjects.Count);
        Assert.Equal(120.0, result.Beatmap.Bpm, 2);
    }

    [Fact]
    public void BeatmapParser_ParsesBookmarks()
    {
        var parser = new BeatmapParser();
        var result = parser.Parse(SampleOsu);

        Assert.Equal(new[] { 1000, 2000, 3000 }, result.Beatmap.Bookmarks);
    }

    [Fact]
    public void BeatmapParser_SkipsInvalidLinesWithWarning()
    {
        var osu = """
[TimingPoints]
0,500,4,1,0,100,1,0
not a timing point

[HitObjects]
100,200,500,1,0
garbage line here
""";
        var parser = new BeatmapParser();
        var result = parser.Parse(osu);

        Assert.Single(result.Beatmap.ControlPoints);
        Assert.Single(result.Beatmap.HitObjects);
        Assert.True(result.Diagnostics.Count >= 2); // at least 2 warnings for bad lines
    }

    [Fact]
    public void BeatmapInfo_GetControlPointAt_ReturnsLatestActive()
    {
        var parser = new BeatmapParser();
        var result = parser.Parse(SampleOsu);
        var bm = result.Beatmap;

        // At 1000ms, the timing point at 0 is active.
        Assert.Equal(0, bm.GetControlPointAt(1000)!.Offset);
        Assert.False(bm.GetControlPointAt(1000)!.IsInherited);
        // At 2500ms, the inherited point at 2000 is active.
        Assert.Equal(2000, bm.GetControlPointAt(2500)!.Offset);
        Assert.True(bm.GetControlPointAt(2500)!.IsInherited);
        // Before any point -> null.
        Assert.Null(bm.GetControlPointAt(-100));
    }

    [Fact]
    public void BeatmapInfo_GetTimingPointAt_ReturnsLatestRedLine()
    {
        var parser = new BeatmapParser();
        var result = parser.Parse(SampleOsu);
        var bm = result.Beatmap;

        // At 2500ms, the inherited point is active for GetControlPointAt, but
        // GetTimingPointAt should still return the red line at 0.
        var timing = bm.GetTimingPointAt(2500);
        Assert.NotNull(timing);
        Assert.Equal(0, timing!.Offset);
        Assert.False(timing.IsInherited);
    }

    [Fact]
    public void BeatmapInfo_SnapToBeat_SnapsToNearestDivision()
    {
        var bm = new BeatmapInfo();
        bm.ControlPoints.Add(new ControlPoint { Offset = 0, BeatDuration = 500, BeatPerMeasure = 4 });

        // beatDuration 500, division 1 (quarter): snap 620 -> 500 (nearest beat)
        Assert.Equal(500, bm.SnapToBeat(620, 1), 3);
        // division 2 (eighth): step 250ms. snap 620 -> 500 (nearest 250 multiple)
        Assert.Equal(500, bm.SnapToBeat(620, 2), 3);
        // division 4 (sixteenth): step 125ms. snap 620 -> 625
        Assert.Equal(625, bm.SnapToBeat(620, 4), 3);
    }

    // ---------------- BeatCalculator ----------------

    [Fact]
    public void BeatCalculator_GetBeatDuration_And_GetBpm()
    {
        var timing = new ControlPoint { Offset = 0, BeatDuration = 500, IsInherited = false };
        Assert.Equal(500, BeatCalculator.GetBeatDuration(timing));
        Assert.Equal(120.0, BeatCalculator.GetBpm(timing), 2);

        var inherited = new ControlPoint { Offset = 0, BeatDuration = -100, IsInherited = true };
        Assert.Equal(0, BeatCalculator.GetBeatDuration(inherited));
        Assert.Equal(0, BeatCalculator.GetBpm(inherited));
    }

    [Fact]
    public void BeatCalculator_SnapToBeat_QuarterAndSixteenth()
    {
        // beatDuration 500, division 1: snap 620 -> 500
        Assert.Equal(500, BeatCalculator.SnapToBeat(620, 500, 4, 1), 3);
        // division 4 (sixteenth, step 125): snap 620 -> 625
        Assert.Equal(625, BeatCalculator.SnapToBeat(620, 500, 4, 4), 3);
    }

    [Fact]
    public void BeatCalculator_GetNextBeatTime_AtOrAfter()
    {
        var timing = new ControlPoint { Offset = 1000, BeatDuration = 500, BeatPerMeasure = 4 };
        // time 1100 -> next beat at 1500
        Assert.Equal(1500, BeatCalculator.GetNextBeatTime(1100, timing), 3);
        // time exactly on beat 1000 -> next beat at 1000 (at or after)
        Assert.Equal(1000, BeatCalculator.GetNextBeatTime(1000, timing), 3);
    }

    [Fact]
    public void BeatCalculator_GetPreviousBeatTime_AtOrBefore()
    {
        var timing = new ControlPoint { Offset = 1000, BeatDuration = 500, BeatPerMeasure = 4 };
        // time 1100 -> previous beat at 1000
        Assert.Equal(1000, BeatCalculator.GetPreviousBeatTime(1100, timing), 3);
        // time exactly on beat 1500 -> previous beat at 1500 (at or before)
        Assert.Equal(1500, BeatCalculator.GetPreviousBeatTime(1500, timing), 3);
    }

    [Fact]
    public void BeatCalculator_GetBeatNumber_And_GetMeasureNumber()
    {
        var timing = new ControlPoint { Offset = 0, BeatDuration = 500, BeatPerMeasure = 4 };
        // 0ms = beat 0, measure 0
        Assert.Equal(0, BeatCalculator.GetBeatNumber(0, timing));
        Assert.Equal(0, BeatCalculator.GetMeasureNumber(0, timing));
        // 500ms = beat 1, measure 0
        Assert.Equal(1, BeatCalculator.GetBeatNumber(500, timing));
        Assert.Equal(0, BeatCalculator.GetMeasureNumber(500, timing));
        // 2000ms = beat 0 of measure 1
        Assert.Equal(0, BeatCalculator.GetBeatNumber(2000, timing));
        Assert.Equal(1, BeatCalculator.GetMeasureNumber(2000, timing));
        // 2500ms = beat 1 of measure 1
        Assert.Equal(1, BeatCalculator.GetBeatNumber(2500, timing));
        Assert.Equal(1, BeatCalculator.GetMeasureNumber(2500, timing));
    }

    // ---------------- HitsoundMapper ----------------

    [Fact]
    public void HitsoundMapper_Whistle_ProducesCorrectSamplePath()
    {
        var ho = new HitObject
        {
            StartTime = 1000,
            SampleSet = SampleSet.Normal,
            Additions = HitSoundAddition.Whistle,
        };
        var events = HitsoundMapper.Map(new[] { ho });
        Assert.Single(events);
        Assert.Equal(1000, events[0].Time);
        Assert.Equal("normal-hitwhistle.wav", events[0].SamplePath);
    }

    [Fact]
    public void HitsoundMapper_Finish_Soft_ProducesCorrectSamplePath()
    {
        var ho = new HitObject
        {
            StartTime = 2000,
            SampleSet = SampleSet.Soft,
            Additions = HitSoundAddition.Finish,
        };
        var events = HitsoundMapper.Map(new[] { ho });
        Assert.Single(events);
        Assert.Equal("soft-hitfinish.wav", events[0].SamplePath);
    }

    [Fact]
    public void HitsoundMapper_MultipleAdditions_ProduceMultipleEvents()
    {
        var ho = new HitObject
        {
            StartTime = 3000,
            SampleSet = SampleSet.Drum,
            Additions = HitSoundAddition.Whistle | HitSoundAddition.Clap,
        };
        var events = HitsoundMapper.Map(new[] { ho });
        Assert.Equal(2, events.Count);
        Assert.Contains(events, e => e.SamplePath == "drum-hitwhistle.wav");
        Assert.Contains(events, e => e.SamplePath == "drum-hitclap.wav");
    }

    [Fact]
    public void HitsoundMapper_CustomSampleSet_AppendsIndex()
    {
        var ho = new HitObject
        {
            StartTime = 4000,
            SampleSet = SampleSet.Normal,
            Additions = HitSoundAddition.Whistle,
            CustomSampleSet = 2,
        };
        var events = HitsoundMapper.Map(new[] { ho });
        Assert.Single(events);
        Assert.Equal("normal-hitwhistle2.wav", events[0].SamplePath);
    }

    [Fact]
    public void HitsoundMapper_NoAdditions_ProducesNoEvents()
    {
        var ho = new HitObject
        {
            StartTime = 5000,
            SampleSet = SampleSet.Normal,
            Additions = HitSoundAddition.None,
        };
        var events = HitsoundMapper.Map(new[] { ho });
        Assert.Empty(events);
    }

    [Fact]
    public void HitsoundMapper_ExplicitSamplePath_UsedDirectly()
    {
        var ho = new HitObject
        {
            StartTime = 6000,
            SampleSet = SampleSet.Normal,
            Additions = HitSoundAddition.None,
            SamplePath = "custom/sample.wav",
            Volume = 50,
        };
        var events = HitsoundMapper.Map(new[] { ho });
        Assert.Single(events);
        Assert.Equal("custom/sample.wav", events[0].SamplePath);
        Assert.Equal(0.5f, events[0].Volume);
    }

    // ---------------- AudioReducer ----------------

    [Fact]
    public void AudioReducer_LoadBeatmap_SetsBeatmapAndResetsTime()
    {
        var state = new AudioState { CurrentTimeMs = 500, IsPlaying = true };
        var bm = new BeatmapInfo { Name = "Test", DifficultyName = "Easy" };

        var next = AudioReducer.Reduce(state, new LoadBeatmapAction(bm));

        Assert.NotNull(next.Beatmap);
        Assert.Equal("Test", next.Beatmap!.Name);
        Assert.Equal(0, next.CurrentTimeMs);
        Assert.False(next.IsPlaying);
        // Original state unchanged
        Assert.Equal(500, state.CurrentTimeMs);
        Assert.True(state.IsPlaying);
    }

    [Fact]
    public void AudioReducer_PlayPause_SetsIsPlaying()
    {
        var state = new AudioState { IsPlaying = false };

        var playing = AudioReducer.Reduce(state, new PlayPauseAction(true));
        Assert.True(playing.IsPlaying);

        var paused = AudioReducer.Reduce(playing, new PlayPauseAction(false));
        Assert.False(paused.IsPlaying);
    }

    [Fact]
    public void AudioReducer_Seek_ClampsNegativeToZero()
    {
        var state = new AudioState { CurrentTimeMs = 100 };
        var next = AudioReducer.Reduce(state, new SeekAction(-500));
        Assert.Equal(0, next.CurrentTimeMs);
    }

    [Fact]
    public void AudioReducer_Seek_ClampsToDurationWhenKnown()
    {
        var state = new AudioState { CurrentTimeMs = 100, DurationMs = 10000 };
        var next = AudioReducer.Reduce(state, new SeekAction(20000));
        Assert.Equal(10000, next.CurrentTimeMs);
    }

    [Fact]
    public void AudioReducer_SetBeatSnapDivision_ClampsToMinimum1()
    {
        var state = new AudioState { BeatSnapDivision = 4 };
        var next = AudioReducer.Reduce(state, new SetBeatSnapDivisionAction(0));
        Assert.Equal(1, next.BeatSnapDivision);

        var next2 = AudioReducer.Reduce(state, new SetBeatSnapDivisionAction(8));
        Assert.Equal(8, next2.BeatSnapDivision);
    }

    [Fact]
    public void AudioReducer_SetVolume_ClampsToZeroOneRange()
    {
        var state = new AudioState { Volume = 0.5f };

        var tooHigh = AudioReducer.Reduce(state, new SetVolumeAction(2.0f));
        Assert.Equal(1.0f, tooHigh.Volume);

        var tooLow = AudioReducer.Reduce(state, new SetVolumeAction(-1.0f));
        Assert.Equal(0.0f, tooLow.Volume);

        var valid = AudioReducer.Reduce(state, new SetVolumeAction(0.75f));
        Assert.Equal(0.75f, valid.Volume);
    }

    [Fact]
    public void AudioReducer_UnknownAction_ReturnsSameState()
    {
        var state = new AudioState { CurrentTimeMs = 100 };
        var next = AudioReducer.Reduce(state, new UnknownAction());
        Assert.Same(state, next);
    }

    private sealed record UnknownAction();

    // ---------------- HeadlessAudioBackend ----------------

    [Fact]
    public void HeadlessAudioBackend_Load_SetsDurationFromNumericName()
    {
        var backend = new HeadlessAudioBackend();
        backend.Load("12345.mp3");
        Assert.Equal(12345, backend.DurationMs);
        Assert.Equal(0, backend.CurrentTimeMs);
        Assert.False(backend.IsPlaying);
    }

    [Fact]
    public void HeadlessAudioBackend_Load_NonNumericName_DefaultsDuration()
    {
        var backend = new HeadlessAudioBackend();
        backend.Load("audio.mp3");
        Assert.Equal(60000, backend.DurationMs);
    }

    [Fact]
    public void HeadlessAudioBackend_PlayPause_TogglesIsPlaying()
    {
        var backend = new HeadlessAudioBackend();
        backend.Load("60000.mp3");
        Assert.False(backend.IsPlaying);

        backend.Play();
        Assert.True(backend.IsPlaying);

        backend.Pause();
        Assert.False(backend.IsPlaying);
    }

    [Fact]
    public void HeadlessAudioBackend_Seek_ClampsToDuration()
    {
        var backend = new HeadlessAudioBackend();
        backend.Load("10000.mp3");

        backend.Seek(5000);
        Assert.Equal(5000, backend.CurrentTimeMs);

        backend.Seek(-100);
        Assert.Equal(0, backend.CurrentTimeMs);

        backend.Seek(99999);
        Assert.Equal(10000, backend.CurrentTimeMs);
    }

    [Fact]
    public void HeadlessAudioBackend_SetVolume_ClampsToZeroOne()
    {
        var backend = new HeadlessAudioBackend();
        backend.SetVolume(0.5f);
        Assert.Equal(0.5f, backend.Volume);

        backend.SetVolume(5.0f);
        Assert.Equal(1.0f, backend.Volume);

        backend.SetVolume(-1.0f);
        Assert.Equal(0.0f, backend.Volume);
    }

    [Fact]
    public void HeadlessAudioBackend_Tick_AdvancesPositionAndStopsAtEnd()
    {
        var backend = new HeadlessAudioBackend();
        backend.Load("10000.mp3");
        backend.Play();
        backend.Tick(3000);
        Assert.Equal(3000, backend.CurrentTimeMs);
        Assert.True(backend.IsPlaying);

        backend.Tick(8000); // would exceed duration
        Assert.Equal(10000, backend.CurrentTimeMs);
        Assert.False(backend.IsPlaying);
    }

    [Fact]
    public void HeadlessAudioBackend_Tick_NoOpWhenNotPlaying()
    {
        var backend = new HeadlessAudioBackend();
        backend.Load("10000.mp3");
        backend.Tick(5000);
        Assert.Equal(0, backend.CurrentTimeMs);
    }
}
