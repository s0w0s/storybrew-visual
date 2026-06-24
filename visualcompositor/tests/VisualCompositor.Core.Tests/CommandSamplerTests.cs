using VisualCompositor.Core.Model;
using VisualCompositor.Core.Primitives;
using VisualCompositor.Rendering.Sampling;

namespace VisualCompositor.Core.Tests;

public class CommandSamplerTests
{
    private readonly CommandSampler _sampler = new();

    [Fact]
    public void SampleSpriteState_NoCommands_ReturnsDefaultState()
    {
        var sprite = new SpriteDeclaration
        {
            Id = "s1",
            InitialPosition = new Vector2(100, 200),
        };

        var state = _sampler.SampleSpriteState(sprite, 500);

        Assert.Equal(new Vector2(100, 200), state.Position);
        Assert.Equal(Vector2.One, state.Scale);
        Assert.Equal(0f, state.Rotation);
        Assert.Equal(1f, state.Opacity);
        Assert.Equal(Color3.White, state.Color);
        Assert.False(state.Additive);
        Assert.False(state.FlipH);
        Assert.False(state.FlipV);
    }

    [Fact]
    public void SampleSpriteState_FloatKeyframe_LinearInterpolation()
    {
        var sprite = new SpriteDeclaration
        {
            Id = "s1",
            PropertyTracks = new List<PropertyTrack>
            {
                new()
                {
                    PropertyName = "Opacity",
                    ValueType = "Float",
                    FloatKeyframes = new List<Keyframe<float>>
                    {
                        new() { Time = 0, Value = 1f, Easing = OsbEasing.None },
                        new() { Time = 1000, Value = 0f, Easing = OsbEasing.None },
                    },
                },
            },
        };

        // At midpoint, linear interpolation: 1.0 -> 0.0 at t=0.5 => 0.5
        var state = _sampler.SampleSpriteState(sprite, 500);
        Assert.Equal(0.5f, state.Opacity, 0.001f);

        // At start
        state = _sampler.SampleSpriteState(sprite, 0);
        Assert.Equal(1f, state.Opacity, 0.001f);

        // At end
        state = _sampler.SampleSpriteState(sprite, 1000);
        Assert.Equal(0f, state.Opacity, 0.001f);

        // Before first keyframe => first value
        state = _sampler.SampleSpriteState(sprite, -100);
        Assert.Equal(1f, state.Opacity, 0.001f);

        // After last keyframe => last value
        state = _sampler.SampleSpriteState(sprite, 2000);
        Assert.Equal(0f, state.Opacity, 0.001f);
    }

    [Fact]
    public void SampleSpriteState_RotationKeyframe_LinearInterpolation()
    {
        var sprite = new SpriteDeclaration
        {
            Id = "s1",
            PropertyTracks = new List<PropertyTrack>
            {
                new()
                {
                    PropertyName = "Rotation",
                    ValueType = "Float",
                    FloatKeyframes = new List<Keyframe<float>>
                    {
                        new() { Time = 0, Value = 0f, Easing = OsbEasing.None },
                        new() { Time = 1000, Value = 90f, Easing = OsbEasing.None },
                    },
                },
            },
        };

        var state = _sampler.SampleSpriteState(sprite, 500);
        Assert.Equal(45f, state.Rotation, 0.001f);
    }

    [Fact]
    public void SampleSpriteState_ScaleFloatKeyframe_UniformScale()
    {
        var sprite = new SpriteDeclaration
        {
            Id = "s1",
            PropertyTracks = new List<PropertyTrack>
            {
                new()
                {
                    PropertyName = "Scale",
                    ValueType = "Float",
                    FloatKeyframes = new List<Keyframe<float>>
                    {
                        new() { Time = 0, Value = 1f, Easing = OsbEasing.None },
                        new() { Time = 1000, Value = 2f, Easing = OsbEasing.None },
                    },
                },
            },
        };

        var state = _sampler.SampleSpriteState(sprite, 500);
        Assert.Equal(1.5f, state.Scale.X, 0.001f);
        Assert.Equal(1.5f, state.Scale.Y, 0.001f);
    }

    [Fact]
    public void SampleSpriteState_Vector2Keyframe_LinearInterpolation()
    {
        var sprite = new SpriteDeclaration
        {
            Id = "s1",
            PropertyTracks = new List<PropertyTrack>
            {
                new()
                {
                    PropertyName = "Position",
                    ValueType = "Vector2",
                    Vector2Keyframes = new List<Keyframe<Vector2>>
                    {
                        new() { Time = 0, Value = new Vector2(0, 0), Easing = OsbEasing.None },
                        new() { Time = 1000, Value = new Vector2(100, 200), Easing = OsbEasing.None },
                    },
                },
            },
        };

        // At midpoint: (50, 100)
        var state = _sampler.SampleSpriteState(sprite, 500);
        Assert.Equal(50f, state.Position.X, 0.001f);
        Assert.Equal(100f, state.Position.Y, 0.001f);

        // At start
        state = _sampler.SampleSpriteState(sprite, 0);
        Assert.Equal(0f, state.Position.X, 0.001f);
        Assert.Equal(0f, state.Position.Y, 0.001f);

        // At end
        state = _sampler.SampleSpriteState(sprite, 1000);
        Assert.Equal(100f, state.Position.X, 0.001f);
        Assert.Equal(200f, state.Position.Y, 0.001f);
    }

    [Fact]
    public void SampleSpriteState_Vector2ScaleKeyframe()
    {
        var sprite = new SpriteDeclaration
        {
            Id = "s1",
            PropertyTracks = new List<PropertyTrack>
            {
                new()
                {
                    PropertyName = "Scale",
                    ValueType = "Vector2",
                    Vector2Keyframes = new List<Keyframe<Vector2>>
                    {
                        new() { Time = 0, Value = new Vector2(1, 1), Easing = OsbEasing.None },
                        new() { Time = 1000, Value = new Vector2(3, 5), Easing = OsbEasing.None },
                    },
                },
            },
        };

        var state = _sampler.SampleSpriteState(sprite, 500);
        Assert.Equal(2f, state.Scale.X, 0.001f);
        Assert.Equal(3f, state.Scale.Y, 0.001f);
    }

    [Fact]
    public void SampleSpriteState_ColorKeyframe_LinearInterpolation()
    {
        var sprite = new SpriteDeclaration
        {
            Id = "s1",
            PropertyTracks = new List<PropertyTrack>
            {
                new()
                {
                    PropertyName = "Color",
                    ValueType = "Color",
                    ColorKeyframes = new List<Keyframe<Color3>>
                    {
                        new() { Time = 0, Value = Color3.White, Easing = OsbEasing.None },
                        new() { Time = 1000, Value = Color3.Black, Easing = OsbEasing.None },
                    },
                },
            },
        };

        // At midpoint: (0.5, 0.5, 0.5)
        var state = _sampler.SampleSpriteState(sprite, 500);
        Assert.Equal(0.5f, state.Color.R, 0.001f);
        Assert.Equal(0.5f, state.Color.G, 0.001f);
        Assert.Equal(0.5f, state.Color.B, 0.001f);

        // At start: white
        state = _sampler.SampleSpriteState(sprite, 0);
        Assert.Equal(Color3.White, state.Color);

        // At end: black
        state = _sampler.SampleSpriteState(sprite, 1000);
        Assert.Equal(Color3.Black, state.Color);
    }

    [Fact]
    public void SampleSpriteState_ParameterTrack_AdditiveFlipHFlipV_Active()
    {
        var sprite = new SpriteDeclaration
        {
            Id = "s1",
            ParameterTrack = new ParameterTrack
            {
                Segments = new List<ParameterSegment>
                {
                    new() { Parameter = ParameterType.AdditiveBlending, StartTime = 0, EndTime = 1000 },
                    new() { Parameter = ParameterType.FlipHorizontal, StartTime = 500, EndTime = 1500 },
                    new() { Parameter = ParameterType.FlipVertical, StartTime = 1000, EndTime = null },
                },
            },
        };

        // At time 250: only AdditiveBlending active
        var state = _sampler.SampleSpriteState(sprite, 250);
        Assert.True(state.Additive);
        Assert.False(state.FlipH);
        Assert.False(state.FlipV);

        // At time 750: AdditiveBlending + FlipHorizontal active
        state = _sampler.SampleSpriteState(sprite, 750);
        Assert.True(state.Additive);
        Assert.True(state.FlipH);
        Assert.False(state.FlipV);

        // At time 1200: FlipHorizontal + FlipVertical active (Additive ended at 1000)
        state = _sampler.SampleSpriteState(sprite, 1200);
        Assert.False(state.Additive);
        Assert.True(state.FlipH);
        Assert.True(state.FlipV);

        // At time 2000: only FlipVertical (open-ended) active
        state = _sampler.SampleSpriteState(sprite, 2000);
        Assert.False(state.Additive);
        Assert.False(state.FlipH);
        Assert.True(state.FlipV);
    }

    [Fact]
    public void SampleSpriteState_LoopBlock_AppliesRelativeCommandsPerIteration()
    {
        // Loop: StartTime=1000, LoopCount=3
        // RelativeCommand: F (opacity), StartTime=0, EndTime=500, StartValue="1", EndValue="0"
        // LoopDuration = 500 (max end time)
        var sprite = new SpriteDeclaration
        {
            Id = "s1",
            Blocks = new List<StoryboardBlock>
            {
                new LoopBlock
                {
                    Id = "loop1",
                    StartTime = 1000,
                    LoopCount = 3,
                    RelativeCommands = new List<RelativeCommand>
                    {
                        new()
                        {
                            Id = "rc1",
                            CommandType = "F",
                            Easing = OsbEasing.None,
                            StartTime = 0,
                            EndTime = 500,
                            StartValue = "1",
                            EndValue = "0",
                        },
                    },
                },
            },
        };

        // Before loop starts: opacity should be default (1)
        var state = _sampler.SampleSpriteState(sprite, 500);
        Assert.Equal(1f, state.Opacity, 0.001f);

        // First iteration, start (relTime=0): opacity = 1
        state = _sampler.SampleSpriteState(sprite, 1000);
        Assert.Equal(1f, state.Opacity, 0.001f);

        // First iteration, midpoint (relTime=250): opacity = 0.5
        state = _sampler.SampleSpriteState(sprite, 1250);
        Assert.Equal(0.5f, state.Opacity, 0.001f);

        // First iteration, end (relTime=500): opacity = 0
        state = _sampler.SampleSpriteState(sprite, 1500);
        // At exactly 1500: elapsed=500, iteration=1, iterationTime=0 => opacity=1 (second iteration start)
        Assert.Equal(1f, state.Opacity, 0.001f);

        // Second iteration, midpoint (relTime=250): opacity = 0.5
        state = _sampler.SampleSpriteState(sprite, 1750);
        Assert.Equal(0.5f, state.Opacity, 0.001f);

        // After loop ends (iteration >= LoopCount): opacity should be default (1)
        // Loop ends at 1000 + 3*500 = 2500
        state = _sampler.SampleSpriteState(sprite, 3000);
        Assert.Equal(1f, state.Opacity, 0.001f);
    }

    [Fact]
    public void SampleSpriteState_LoopBlock_RotationCommand()
    {
        // Loop: StartTime=0, LoopCount=2
        // RelativeCommand: R (rotation), StartTime=0, EndTime=1000, StartValue="0", EndValue="360"
        // LoopDuration = 1000
        var sprite = new SpriteDeclaration
        {
            Id = "s1",
            Blocks = new List<StoryboardBlock>
            {
                new LoopBlock
                {
                    Id = "loop1",
                    StartTime = 0,
                    LoopCount = 2,
                    RelativeCommands = new List<RelativeCommand>
                    {
                        new()
                        {
                            Id = "rc1",
                            CommandType = "R",
                            Easing = OsbEasing.None,
                            StartTime = 0,
                            EndTime = 1000,
                            StartValue = "0",
                            EndValue = "360",
                        },
                    },
                },
            },
        };

        // First iteration, midpoint (relTime=500): rotation = 180
        var state = _sampler.SampleSpriteState(sprite, 500);
        Assert.Equal(180f, state.Rotation, 0.001f);

        // Second iteration, midpoint (elapsed=1500, iteration=1, relTime=500): rotation = 180
        state = _sampler.SampleSpriteState(sprite, 1500);
        Assert.Equal(180f, state.Rotation, 0.001f);
    }

    [Fact]
    public void SampleSpriteState_TriggerBlock_AppliesCommandsInRange()
    {
        // Trigger: StartTime=1000, EndTime=2000
        // RelativeCommand: F (opacity), StartTime=0, EndTime=500, StartValue="1", EndValue="0.5"
        var sprite = new SpriteDeclaration
        {
            Id = "s1",
            Blocks = new List<StoryboardBlock>
            {
                new TriggerBlock
                {
                    Id = "trig1",
                    StartTime = 1000,
                    EndTime = 2000,
                    RelativeCommands = new List<RelativeCommand>
                    {
                        new()
                        {
                            Id = "rc1",
                            CommandType = "F",
                            Easing = OsbEasing.None,
                            StartTime = 0,
                            EndTime = 500,
                            StartValue = "1",
                            EndValue = "0.5",
                        },
                    },
                },
            },
        };

        // Before trigger: opacity = 1 (default)
        var state = _sampler.SampleSpriteState(sprite, 500);
        Assert.Equal(1f, state.Opacity, 0.001f);

        // Trigger active, start (relTime=0): opacity = 1
        state = _sampler.SampleSpriteState(sprite, 1000);
        Assert.Equal(1f, state.Opacity, 0.001f);

        // Trigger active, midpoint (relTime=250): opacity = 0.75
        state = _sampler.SampleSpriteState(sprite, 1250);
        Assert.Equal(0.75f, state.Opacity, 0.001f);

        // Trigger active, end of command (relTime=500): opacity = 0.5
        state = _sampler.SampleSpriteState(sprite, 1500);
        Assert.Equal(0.5f, state.Opacity, 0.001f);

        // After trigger ends: opacity = 1 (default)
        state = _sampler.SampleSpriteState(sprite, 2500);
        Assert.Equal(1f, state.Opacity, 0.001f);
    }

    [Fact]
    public void SampleSpriteState_MultiplePropertyTracks_Combined()
    {
        var sprite = new SpriteDeclaration
        {
            Id = "s1",
            InitialPosition = new Vector2(320, 240),
            PropertyTracks = new List<PropertyTrack>
            {
                new()
                {
                    PropertyName = "Position",
                    ValueType = "Vector2",
                    Vector2Keyframes = new List<Keyframe<Vector2>>
                    {
                        new() { Time = 0, Value = new Vector2(0, 0), Easing = OsbEasing.None },
                        new() { Time = 1000, Value = new Vector2(100, 100), Easing = OsbEasing.None },
                    },
                },
                new()
                {
                    PropertyName = "Opacity",
                    ValueType = "Float",
                    FloatKeyframes = new List<Keyframe<float>>
                    {
                        new() { Time = 0, Value = 0f, Easing = OsbEasing.None },
                        new() { Time = 1000, Value = 1f, Easing = OsbEasing.None },
                    },
                },
                new()
                {
                    PropertyName = "Rotation",
                    ValueType = "Float",
                    FloatKeyframes = new List<Keyframe<float>>
                    {
                        new() { Time = 0, Value = 0f, Easing = OsbEasing.None },
                        new() { Time = 1000, Value = 45f, Easing = OsbEasing.None },
                    },
                },
            },
        };

        var state = _sampler.SampleSpriteState(sprite, 500);
        Assert.Equal(50f, state.Position.X, 0.001f);
        Assert.Equal(50f, state.Position.Y, 0.001f);
        Assert.Equal(0.5f, state.Opacity, 0.001f);
        Assert.Equal(22.5f, state.Rotation, 0.001f);
    }

    [Fact]
    public void SampleSpriteState_SingleKeyframe_ReturnsThatValue()
    {
        var sprite = new SpriteDeclaration
        {
            Id = "s1",
            PropertyTracks = new List<PropertyTrack>
            {
                new()
                {
                    PropertyName = "Opacity",
                    ValueType = "Float",
                    FloatKeyframes = new List<Keyframe<float>>
                    {
                        new() { Time = 0, Value = 0.7f, Easing = OsbEasing.None },
                    },
                },
            },
        };

        var state = _sampler.SampleSpriteState(sprite, 500);
        Assert.Equal(0.7f, state.Opacity, 0.001f);
    }
}
