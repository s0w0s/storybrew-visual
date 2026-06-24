namespace VisualCompositor.Osb.Parser;

/// <summary>Base class for all parsed OSB events. Carries the 1-based source line number.</summary>
public abstract class ParsedEvent
{
    public int LineNumber { get; set; }
}

/// <summary>The fully parsed .osb file: variables + ordered top-level events.</summary>
public sealed class ParsedOsbFile
{
    /// <summary>OSB [Variables] section: variable name (with leading '$') -> value.</summary>
    public Dictionary<string, string> Variables { get; set; } = new();

    /// <summary>Top-level events in source order: ParsedSprite, ParsedSample, ParsedRawLine (and orphan ParsedCommand).</summary>
    public List<ParsedEvent> Events { get; set; } = new();

    /// <summary>Parser diagnostics (non-fatal issues encountered while parsing).</summary>
    public List<string> Diagnostics { get; set; } = new();
}

/// <summary>A "Sprite,..." or "Animation,..." declaration line.</summary>
public sealed class ParsedSprite : ParsedEvent
{
    /// <summary>OSB layer index (0=Background..4=Overlay).</summary>
    public int Layer { get; set; }

    /// <summary>OSB origin index (0..8).</summary>
    public int Origin { get; set; }

    public string TexturePath { get; set; } = string.Empty;

    public float X { get; set; }

    public float Y { get; set; }

    /// <summary>For Animation only. 0 for Sprite.</summary>
    public int FrameCount { get; set; }

    /// <summary>For Animation only. 0 for Sprite.</summary>
    public double FrameDelay { get; set; }

    /// <summary>For Animation only (0=LoopForever, 1=LoopOnce).</summary>
    public int LoopType { get; set; }

    /// <summary>True if parsed from an "Animation," line; false for "Sprite,".</summary>
    public bool IsAnimation { get; set; }

    /// <summary>Child events in source order: ParsedCommand, ParsedLoop, ParsedTrigger, ParsedRawLine.</summary>
    public List<ParsedEvent> Commands { get; set; } = new();
}

/// <summary>A command line " _letter,easing,start,end,values...".</summary>
public sealed class ParsedCommand : ParsedEvent
{
    /// <summary>Command letter: M, MX, MY, S, V, R, F, C, P.</summary>
    public string CommandLetter { get; set; } = string.Empty;

    /// <summary>Numeric easing value (0..34).</summary>
    public int Easing { get; set; }

    public double StartTime { get; set; }

    public double EndTime { get; set; }

    /// <summary>True if the original endTime field was empty (relevant for P open-ended semantics).</summary>
    public bool EndTimeIsEmpty { get; set; }

    /// <summary>Start value(s) as a comma-joined string (e.g. "320,240" for M).</summary>
    public string StartValue { get; set; } = string.Empty;

    /// <summary>End value(s) as a comma-joined string. Equal to StartValue when omitted.</summary>
    public string EndValue { get; set; } = string.Empty;

    /// <summary>Number of leading spaces on the source line.</summary>
    public int IndentLevel { get; set; }
}

/// <summary>A "L,startTime,loopCount" loop block header.</summary>
public sealed class ParsedLoop : ParsedEvent
{
    public double StartTime { get; set; }

    public int LoopCount { get; set; }

    /// <summary>Relative commands (and raw lines) inside this loop, in source order.</summary>
    public List<ParsedEvent> Commands { get; set; } = new();
}

/// <summary>A "T,name,start,end,group" trigger block header.</summary>
public sealed class ParsedTrigger : ParsedEvent
{
    public string TriggerName { get; set; } = string.Empty;

    public double StartTime { get; set; }

    public double EndTime { get; set; }

    public int Group { get; set; }

    /// <summary>Relative commands (and raw lines) inside this trigger, in source order.</summary>
    public List<ParsedEvent> Commands { get; set; } = new();
}

/// <summary>A "Sample,time,layer,"path",volume" line.</summary>
public sealed class ParsedSample : ParsedEvent
{
    public double Time { get; set; }

    public int Layer { get; set; }

    public string AudioPath { get; set; } = string.Empty;

    public float Volume { get; set; }
}

/// <summary>An unknown/comment/blank line preserved verbatim for round-trip fidelity.</summary>
public sealed class ParsedRawLine : ParsedEvent
{
    /// <summary>The original raw text of the line (without line ending).</summary>
    public string Content { get; set; } = string.Empty;
}
