using VisualCompositor.Osb.Parser;
using Xunit;

namespace VisualCompositor.Core.Tests;

public class OsbParserTests
{
    [Fact]
    public void Parse_SimpleSpriteWithCommands_ProducesSpriteAndCommands()
    {
        var osb = """
[Events]
Sprite,3,4,"bg.jpg",320,240
 F,0,0,1000,0,1
 M,0,0,2000,320,240,320,240
""";
        var parser = new OsbParser();
        var result = parser.Parse(osb);

        Assert.Single(result.Events);
        var sprite = Assert.IsType<ParsedSprite>(result.Events[0]);
        Assert.Equal(3, sprite.Layer);
        Assert.Equal(4, sprite.Origin);
        Assert.Equal("bg.jpg", sprite.TexturePath);
        Assert.Equal(320f, sprite.X);
        Assert.Equal(240f, sprite.Y);
        Assert.Equal(2, sprite.Commands.Count);

        var fade = Assert.IsType<ParsedCommand>(sprite.Commands[0]);
        Assert.Equal("F", fade.CommandLetter);
        Assert.Equal(0, fade.Easing);
        Assert.Equal(0, fade.StartTime);
        Assert.Equal(1000, fade.EndTime);
        Assert.Equal("0", fade.StartValue);
        Assert.Equal("1", fade.EndValue);

        var move = Assert.IsType<ParsedCommand>(sprite.Commands[1]);
        Assert.Equal("M", move.CommandLetter);
        Assert.Equal("320,240", move.StartValue);
        Assert.Equal("320,240", move.EndValue);
    }

    [Fact]
    public void Parse_Variables_AreSubstitutedInEvents()
    {
        var osb = """
[Variables]
$x=320
$y=240

[Events]
Sprite,3,4,"bg.jpg",$x,$y
 M,0,0,2000,$x,$y,400,240
""";
        var parser = new OsbParser();
        var result = parser.Parse(osb);

        Assert.Equal("320", result.Variables["$x"]);
        Assert.Equal("240", result.Variables["$y"]);

        var sprite = Assert.IsType<ParsedSprite>(result.Events[0]);
        Assert.Equal(320f, sprite.X);
        Assert.Equal(240f, sprite.Y);

        var move = Assert.IsType<ParsedCommand>(sprite.Commands[0]);
        Assert.Equal("320,240", move.StartValue);
        Assert.Equal("400,240", move.EndValue);
    }

    [Fact]
    public void Parse_LoopBlock_ContainsRelativeCommands()
    {
        var osb = """
[Events]
Sprite,3,4,"bg.jpg",320,240
 L,1000,3
  F,0,0,500,0,1
  F,1,500,1000,1,0
""";
        var parser = new OsbParser();
        var result = parser.Parse(osb);

        var sprite = Assert.IsType<ParsedSprite>(result.Events[0]);
        Assert.Single(sprite.Commands);
        var loop = Assert.IsType<ParsedLoop>(sprite.Commands[0]);
        Assert.Equal(1000, loop.StartTime);
        Assert.Equal(3, loop.LoopCount);
        Assert.Equal(2, loop.Commands.Count);

        var cmd1 = Assert.IsType<ParsedCommand>(loop.Commands[0]);
        Assert.Equal("F", cmd1.CommandLetter);
        Assert.Equal(0, cmd1.StartTime);
        Assert.Equal(500, cmd1.EndTime);
        Assert.Equal(2, cmd1.IndentLevel);

        var cmd2 = Assert.IsType<ParsedCommand>(loop.Commands[1]);
        Assert.Equal("F", cmd2.CommandLetter);
        Assert.Equal(500, cmd2.StartTime);
        Assert.Equal(1000, cmd2.EndTime);
    }

    [Fact]
    public void Parse_TriggerBlock_ContainsRelativeCommands()
    {
        var osb = """
[Events]
Sprite,3,4,"bg.jpg",320,240
 T,HitObjects,1000,2000,1
  S,0,0,100,1,2
""";
        var parser = new OsbParser();
        var result = parser.Parse(osb);

        var sprite = Assert.IsType<ParsedSprite>(result.Events[0]);
        Assert.Single(sprite.Commands);
        var trigger = Assert.IsType<ParsedTrigger>(sprite.Commands[0]);
        Assert.Equal("HitObjects", trigger.TriggerName);
        Assert.Equal(1000, trigger.StartTime);
        Assert.Equal(2000, trigger.EndTime);
        Assert.Equal(1, trigger.Group);
        Assert.Single(trigger.Commands);

        var cmd = Assert.IsType<ParsedCommand>(trigger.Commands[0]);
        Assert.Equal("S", cmd.CommandLetter);
        Assert.Equal("1", cmd.StartValue);
        Assert.Equal("2", cmd.EndValue);
    }

    [Fact]
    public void Parse_UnknownCommentBlankLines_BecomeRawLines()
    {
        var osb = """
[Events]
// this is a comment
Sprite,3,4,"bg.jpg",320,240

 unknown_line,foo
 F,0,0,1000,0,1
""";
        var parser = new OsbParser();
        var result = parser.Parse(osb);

        // Top-level events: comment raw, sprite (with blank+unknown inside), 
        var topRaw = Assert.IsType<ParsedRawLine>(result.Events[0]);
        Assert.Contains("comment", topRaw.Content);

        var sprite = Assert.IsType<ParsedSprite>(result.Events[1]);
        // Sprite commands: blank line, unknown line, F command
        Assert.Equal(3, sprite.Commands.Count);
        Assert.IsType<ParsedRawLine>(sprite.Commands[0]);
        Assert.IsType<ParsedRawLine>(sprite.Commands[1]);
        Assert.IsType<ParsedCommand>(sprite.Commands[2]);
    }

    [Fact]
    public void Parse_AnimationLine_ProducesAnimationSprite()
    {
        var osb = """
[Events]
Animation,3,4,"frame.jpg",320,240,8,50,0
 F,0,0,1000,0,1
""";
        var parser = new OsbParser();
        var result = parser.Parse(osb);

        var sprite = Assert.IsType<ParsedSprite>(result.Events[0]);
        Assert.True(sprite.IsAnimation);
        Assert.Equal(8, sprite.FrameCount);
        Assert.Equal(50, sprite.FrameDelay);
        Assert.Equal(0, sprite.LoopType);
    }

    [Fact]
    public void Parse_SampleLine_ProducesParsedSample()
    {
        var osb = """
[Events]
Sample,1000,3,"audio.mp3",80
""";
        var parser = new OsbParser();
        var result = parser.Parse(osb);

        var sample = Assert.IsType<ParsedSample>(result.Events[0]);
        Assert.Equal(1000, sample.Time);
        Assert.Equal(3, sample.Layer);
        Assert.Equal("audio.mp3", sample.AudioPath);
        Assert.Equal(80f, sample.Volume);
    }

    [Fact]
    public void Parse_PCommand_WithEmptyEndTime_MarksOpenEnded()
    {
        var osb = """
[Events]
Sprite,3,4,"bg.jpg",320,240
 P,0,1000,,A
""";
        var parser = new OsbParser();
        var result = parser.Parse(osb);

        var sprite = Assert.IsType<ParsedSprite>(result.Events[0]);
        var cmd = Assert.IsType<ParsedCommand>(sprite.Commands[0]);
        Assert.Equal("P", cmd.CommandLetter);
        Assert.True(cmd.EndTimeIsEmpty);
        Assert.Equal(1000, cmd.StartTime);
        Assert.Equal(1000, cmd.EndTime);
        Assert.Equal("A", cmd.StartValue);
    }

    [Fact]
    public void Parse_CommandWithOmittedEndValue_EndValueEqualsStartValue()
    {
        var osb = """
[Events]
Sprite,3,4,"bg.jpg",320,240
 F,0,0,1000,0.5
""";
        var parser = new OsbParser();
        var result = parser.Parse(osb);

        var sprite = Assert.IsType<ParsedSprite>(result.Events[0]);
        var cmd = Assert.IsType<ParsedCommand>(sprite.Commands[0]);
        Assert.Equal("0.5", cmd.StartValue);
        Assert.Equal("0.5", cmd.EndValue);
    }

    [Fact]
    public void Parse_TracksLineNumbers()
    {
        var osb = """
[Events]
Sprite,3,4,"bg.jpg",320,240
 F,0,0,1000,0,1
""";
        var parser = new OsbParser();
        var result = parser.Parse(osb);

        var sprite = Assert.IsType<ParsedSprite>(result.Events[0]);
        Assert.Equal(2, sprite.LineNumber);

        var cmd = Assert.IsType<ParsedCommand>(sprite.Commands[0]);
        Assert.Equal(3, cmd.LineNumber);
    }
}
