using System.Globalization;
using VisualCompositor.Osb.Parser;

namespace VisualCompositor.Osb.Parser;

/// <summary>Parses .osb text into a <see cref="ParsedOsbFile"/> intermediate representation.
/// Performs [Variables] extraction, variable substitution on [Events] lines, indentation-based
/// nesting of commands under sprites and Loop/Trigger blocks, and preservation of
/// unknown/comment/blank lines as <see cref="ParsedRawLine"/>.</summary>
public sealed class OsbParser
{
    /// <summary>Number of value components per endpoint for each command letter.
    /// P is special (single parameter letter, no end value).</summary>
    private static readonly Dictionary<string, int> ValuesPerEndpoint = new(StringComparer.Ordinal)
    {
        { "M", 2 },
        { "MX", 1 },
        { "MY", 1 },
        { "S", 1 },
        { "V", 2 },
        { "R", 1 },
        { "F", 1 },
        { "C", 3 },
        { "P", 1 },
    };

    /// <summary>Parse .osb text into a <see cref="ParsedOsbFile"/>.</summary>
    public ParsedOsbFile Parse(string text)
    {
        var result = new ParsedOsbFile();
        if (string.IsNullOrEmpty(text))
            return result;

        var lines = text.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);

        string? currentSection = null;
        ParsedSprite? currentSprite = null;
        // Stack of open block contexts (Loop/Trigger). Depth 1 = direct child of sprite.
        ParsedEvent? currentBlock = null;

        for (var i = 0; i < lines.Length; i++)
        {
            var lineNumber = i + 1;
            var rawLine = lines[i];
            var trimmed = rawLine.Trim();

            // Section header [Name]
            if (trimmed.StartsWith("[") && trimmed.EndsWith("]") && trimmed.Length >= 2)
            {
                currentSection = trimmed.Substring(1, trimmed.Length - 2).Trim();
                // Section change closes any open sprite/block.
                currentBlock = null;
                currentSprite = null;
                continue;
            }

            if (currentSection == "Variables")
            {
                ParseVariableLine(rawLine, result.Variables);
                continue;
            }

            if (currentSection != "Events")
                continue;

            // In [Events]: preserve comments and blank lines as raw lines in their current context.
            if (trimmed.Length == 0 || trimmed.StartsWith("//", StringComparison.Ordinal))
            {
                var raw = new ParsedRawLine { Content = rawLine, LineNumber = lineNumber };
                AddToCurrentContext(raw, currentSprite, currentBlock, result);
                continue;
            }

            // Apply variable substitution to non-comment lines.
            var substituted = ApplyVariables(rawLine, result.Variables);
            var depth = CountLeadingSpaces(substituted);
            var lineContent = substituted.Trim();

            // Depth < 2 closes any open block (L/T).
            if (depth < 2)
                currentBlock = null;

            // Depth 0 closes the current sprite (new top-level element).
            if (depth == 0)
                currentSprite = null;

            var parts = lineContent.Split(',');

            switch (parts[0])
            {
                case "Sprite":
                    {
                        currentBlock = null;
                        currentSprite = ParseSprite(parts, lineNumber, isAnimation: false);
                        result.Events.Add(currentSprite);
                        break;
                    }
                case "Animation":
                    {
                        currentBlock = null;
                        currentSprite = ParseSprite(parts, lineNumber, isAnimation: true);
                        result.Events.Add(currentSprite);
                        break;
                    }
                case "Sample":
                    {
                        currentBlock = null;
                        currentSprite = null;
                        var sample = ParseSample(parts, lineNumber, result);
                        result.Events.Add(sample);
                        break;
                    }
                case "L":
                    {
                        var loop = ParseLoop(parts, lineNumber, result);
                        if (currentSprite != null)
                        {
                            currentSprite.Commands.Add(loop);
                            currentBlock = loop;
                        }
                        else
                        {
                            // Orphan L without a sprite: keep at top level.
                            result.Events.Add(loop);
                            currentBlock = loop;
                        }
                        break;
                    }
                case "T":
                    {
                        var trigger = ParseTrigger(parts, lineNumber, result);
                        if (currentSprite != null)
                        {
                            currentSprite.Commands.Add(trigger);
                            currentBlock = trigger;
                        }
                        else
                        {
                            result.Events.Add(trigger);
                            currentBlock = trigger;
                        }
                        break;
                    }
                default:
                    {
                        if (ValuesPerEndpoint.ContainsKey(parts[0]))
                        {
                            var cmd = ParseCommand(parts, depth, lineNumber, result);
                            if (currentBlock != null && depth >= 2)
                                GetBlockCommands(currentBlock).Add(cmd);
                            else if (currentSprite != null)
                                currentSprite.Commands.Add(cmd);
                            else
                                result.Events.Add(cmd); // orphan command
                        }
                        else
                        {
                            // Unknown line: preserve as raw.
                            var raw = new ParsedRawLine { Content = rawLine, LineNumber = lineNumber };
                            AddToCurrentContext(raw, currentSprite, currentBlock, result);
                        }
                        break;
                    }
            }
        }

        return result;
    }

    private static void ParseVariableLine(string line, Dictionary<string, string> variables)
    {
        var eq = line.IndexOf('=');
        if (eq <= 0) return;
        var key = line.Substring(0, eq).Trim();
        var value = line.Substring(eq + 1);
        if (key.Length > 0)
            variables[key] = value;
    }

    private static int CountLeadingSpaces(string line)
    {
        var depth = 0;
        while (depth < line.Length && line[depth] == ' ')
            depth++;
        return depth;
    }

    private static string ApplyVariables(string line, Dictionary<string, string> variables)
    {
        if (variables.Count == 0 || line.IndexOf('$') < 0)
            return line;
        var result = line;
        foreach (var kvp in variables)
            result = result.Replace(kvp.Key, kvp.Value);
        return result;
    }

    private static ParsedSprite ParseSprite(string[] parts, int lineNumber, bool isAnimation)
    {
        // Sprite,<layer>,<origin>,"<path>",<x>,<y>
        // Animation,<layer>,<origin>,"<path>",<x>,<y>,<frameCount>,<frameDelay>,<loopType>
        var sprite = new ParsedSprite { LineNumber = lineNumber, IsAnimation = isAnimation };
        if (parts.Length > 1) sprite.Layer = ParseIntSafe(parts[1], 0);
        if (parts.Length > 2) sprite.Origin = ParseIntSafe(parts[2], 0);
        if (parts.Length > 3) sprite.TexturePath = RemovePathQuotes(parts[3]);
        if (parts.Length > 4) sprite.X = (float)ParseFloatSafe(parts[4], 0f);
        if (parts.Length > 5) sprite.Y = (float)ParseFloatSafe(parts[5], 0f);
        if (isAnimation)
        {
            if (parts.Length > 6) sprite.FrameCount = ParseIntSafe(parts[6], 0);
            if (parts.Length > 7) sprite.FrameDelay = ParseFloatSafe(parts[7], 0f);
            if (parts.Length > 8) sprite.LoopType = ParseIntSafe(parts[8], 0);
        }
        return sprite;
    }

    private static ParsedSample ParseSample(string[] parts, int lineNumber, ParsedOsbFile result)
    {
        // Sample,<time>,<layer>,"<path>",<volume>
        var sample = new ParsedSample { LineNumber = lineNumber };
        if (parts.Length > 1) sample.Time = ParseFloatSafe(parts[1], 0f);
        if (parts.Length > 2) sample.Layer = ParseIntSafe(parts[2], 0);
        if (parts.Length > 3) sample.AudioPath = RemovePathQuotes(parts[3]);
        if (parts.Length > 4) sample.Volume = (float)ParseFloatSafe(parts[4], 100f);
        return sample;
    }

    private static ParsedLoop ParseLoop(string[] parts, int lineNumber, ParsedOsbFile result)
    {
        // L,<startTime>,<loopCount>
        var loop = new ParsedLoop { LineNumber = lineNumber };
        if (parts.Length > 1) loop.StartTime = ParseFloatSafe(parts[1], 0f);
        if (parts.Length > 2) loop.LoopCount = ParseIntSafe(parts[2], 0);
        return loop;
    }

    private static ParsedTrigger ParseTrigger(string[] parts, int lineNumber, ParsedOsbFile result)
    {
        // T,<name>,<startTime>,<endTime>,<group>
        var trigger = new ParsedTrigger { LineNumber = lineNumber };
        if (parts.Length > 1) trigger.TriggerName = parts[1];
        if (parts.Length > 2) trigger.StartTime = ParseFloatSafe(parts[2], 0f);
        if (parts.Length > 3) trigger.EndTime = ParseFloatSafe(parts[3], 0f);
        if (parts.Length > 4) trigger.Group = ParseIntSafe(parts[4], 0);
        return trigger;
    }

    private static ParsedCommand ParseCommand(string[] parts, int indentLevel, int lineNumber, ParsedOsbFile result)
    {
        // _<letter>,<easing>,<startTime>,<endTime>,<values...>
        var cmd = new ParsedCommand
        {
            LineNumber = lineNumber,
            CommandLetter = parts[0],
            IndentLevel = indentLevel,
        };

        if (parts.Length > 1) cmd.Easing = ParseIntSafe(parts[1], 0);
        if (parts.Length > 2) cmd.StartTime = ParseFloatSafe(parts[2], 0f);

        // endTime may be empty (means startTime for non-P; open-ended for P).
        var endTimeEmpty = parts.Length <= 3 || string.IsNullOrEmpty(parts[3]);
        cmd.EndTimeIsEmpty = endTimeEmpty;
        if (endTimeEmpty)
            cmd.EndTime = cmd.StartTime;
        else
            cmd.EndTime = ParseFloatSafe(parts[3], cmd.StartTime);

        // Values start at index 4.
        var valueCount = ValuesPerEndpoint[parts[0]];
        var isParameter = parts[0] == "P";

        if (isParameter)
        {
            // P,<easing>,<start>,<end>,<letter>  (single value, no end value)
            if (parts.Length > 4)
                cmd.StartValue = parts[4];
            cmd.EndValue = cmd.StartValue;
            return cmd;
        }

        // Start value = next `valueCount` components joined by comma.
        cmd.StartValue = JoinValues(parts, 4, valueCount);

        // End value = next `valueCount` components after start, if present.
        var endValueStart = 4 + valueCount;
        var hasEndValue = parts.Length > endValueStart && parts.Skip(endValueStart).Any(p => !string.IsNullOrEmpty(p));
        cmd.EndValue = hasEndValue
            ? JoinValues(parts, endValueStart, valueCount)
            : cmd.StartValue;

        return cmd;
    }

    private static string JoinValues(string[] parts, int start, int count)
    {
        var taken = new List<string>(count);
        for (var i = 0; i < count; i++)
        {
            var idx = start + i;
            taken.Add(idx < parts.Length ? parts[idx] : string.Empty);
        }
        return string.Join(",", taken);
    }

    private static List<ParsedEvent> GetBlockCommands(ParsedEvent block)
    {
        return block switch
        {
            ParsedLoop l => l.Commands,
            ParsedTrigger t => t.Commands,
            _ => new List<ParsedEvent>(),
        };
    }

    private static void AddToCurrentContext(ParsedRawLine raw, ParsedSprite? sprite, ParsedEvent? block, ParsedOsbFile result)
    {
        if (block != null)
            GetBlockCommands(block).Add(raw);
        else if (sprite != null)
            sprite.Commands.Add(raw);
        else
            result.Events.Add(raw);
    }

    private static string RemovePathQuotes(string path)
    {
        var s = path.Trim();
        if (s.Length >= 2 && s[0] == '"' && s[^1] == '"')
            return s.Substring(1, s.Length - 2);
        return s;
    }

    private static int ParseIntSafe(string s, int defaultValue)
    {
        if (int.TryParse(s.Trim(), NumberStyles.Integer | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var v))
            return v;
        return defaultValue;
    }

    private static double ParseFloatSafe(string s, double defaultValue)
    {
        if (double.TryParse(s.Trim(), NumberStyles.Float | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var v))
            return v;
        return defaultValue;
    }
}
