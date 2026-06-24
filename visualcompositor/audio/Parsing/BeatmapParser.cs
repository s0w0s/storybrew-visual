using System.Globalization;
using VisualCompositor.Audio.Model;
using VisualCompositor.Core.Validation;

namespace VisualCompositor.Audio.Parsing;

/// <summary>Result of parsing an osu! .osu file.</summary>
public sealed class ParseResult
{
    /// <summary>The parsed beatmap info. Never null, but may be empty on total parse failure.</summary>
    public BeatmapInfo Beatmap { get; init; } = new();

    /// <summary>Diagnostics produced during parsing (warnings for skipped lines, etc.).</summary>
    public List<Diagnostic> Diagnostics { get; init; } = new();
}

/// <summary>Parses an osu! .osu file text into a <see cref="BeatmapInfo"/>. Lenient: skips
/// unparseable lines with a warning rather than throwing. Sections parsed: [General],
/// [Metadata], [TimingPoints], [HitObjects], [Editor] (Bookmarks), and [Events] (background).</summary>
public sealed class BeatmapParser
{
    /// <summary>Parse .osu file text into a <see cref="ParseResult"/>.</summary>
    public ParseResult Parse(string text)
    {
        var result = new ParseResult();
        var beatmap = result.Beatmap;
        if (string.IsNullOrEmpty(text))
            return result;

        var lines = text.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
        string? currentSection = null;

        for (var i = 0; i < lines.Length; i++)
        {
            var lineNumber = i + 1;
            var rawLine = lines[i];
            var trimmed = rawLine.Trim();

            if (trimmed.Length == 0 || trimmed.StartsWith("//", StringComparison.Ordinal))
                continue;

            // Section header [Name]
            if (trimmed.StartsWith("[", StringComparison.Ordinal) && trimmed.EndsWith("]", StringComparison.Ordinal) && trimmed.Length >= 2)
            {
                currentSection = trimmed.Substring(1, trimmed.Length - 2).Trim();
                continue;
            }

            if (currentSection == null)
                continue;

            try
            {
                switch (currentSection)
                {
                    case "General":
                        ParseKeyValue(trimmed, out var gKey, out var gVal);
                        if (gKey == "AudioFilename")
                            beatmap.AudioFilename = gVal.Trim().Trim('"');
                        break;
                    case "Metadata":
                        ParseKeyValue(trimmed, out var mKey, out var mVal);
                        if (mKey == "Title")
                            beatmap.Name = mVal.Trim();
                        else if (mKey == "Version")
                            beatmap.DifficultyName = mVal.Trim();
                        break;
                    case "Editor":
                        ParseKeyValue(trimmed, out var eKey, out var eVal);
                        if (eKey == "Bookmarks")
                            beatmap.Bookmarks = ParseBookmarks(eVal);
                        break;
                    case "TimingPoints":
                        ParseTimingPoint(trimmed, beatmap, result, lineNumber);
                        break;
                    case "HitObjects":
                        ParseHitObject(trimmed, beatmap, result, lineNumber);
                        break;
                    case "Events":
                        ParseEvent(trimmed, beatmap);
                        break;
                }
            }
            catch (Exception ex)
            {
                result.Diagnostics.Add(new Diagnostic
                {
                    Code = "audio.parse.line",
                    Severity = DiagnosticSeverity.Warning,
                    Message = $"Line {lineNumber}: failed to parse ({ex.Message}). Line skipped.",
                });
            }
        }

        // Sort control points by offset for correct GetControlPointAt/GetTimingPointAt behavior.
        beatmap.ControlPoints.Sort(CompareControlPoints);

        var firstTiming = beatmap.ControlPoints.FirstOrDefault(cp => !cp.IsInherited);
        if (firstTiming != null)
            beatmap.Bpm = firstTiming.Bpm;

        return result;
    }

    private static void ParseKeyValue(string line, out string key, out string value)
    {
        var colon = line.IndexOf(':');
        if (colon < 0)
        {
            key = line.Trim();
            value = string.Empty;
            return;
        }
        key = line.Substring(0, colon).Trim();
        value = line.Substring(colon + 1);
    }

    private static List<int> ParseBookmarks(string value)
    {
        var result = new List<int>();
        if (string.IsNullOrWhiteSpace(value))
            return result;
        foreach (var part in value.Split(','))
        {
            var trimmed = part.Trim();
            if (trimmed.Length == 0) continue;
            if (int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var b))
                result.Add(b);
        }
        return result;
    }

    private static void ParseTimingPoint(string line, BeatmapInfo beatmap, ParseResult result, int lineNumber)
    {
        try
        {
            beatmap.ControlPoints.Add(ControlPoint.Parse(line));
        }
        catch (Exception ex)
        {
            result.Diagnostics.Add(new Diagnostic
            {
                Code = "audio.parse.timingpoint",
                Severity = DiagnosticSeverity.Warning,
                Message = $"Line {lineNumber}: invalid timing point ({ex.Message}). Skipped.",
            });
        }
    }

    private static void ParseHitObject(string line, BeatmapInfo beatmap, ParseResult result, int lineNumber)
    {
        try
        {
            beatmap.HitObjects.Add(HitObject.Parse(line));
        }
        catch (Exception ex)
        {
            result.Diagnostics.Add(new Diagnostic
            {
                Code = "audio.parse.hitobject",
                Severity = DiagnosticSeverity.Warning,
                Message = $"Line {lineNumber}: invalid hit object ({ex.Message}). Skipped.",
            });
        }
    }

    private static void ParseEvent(string line, BeatmapInfo beatmap)
    {
        // Background: 0,0,"filename",xOffset,yOffset
        var parts = line.Split(',');
        if (parts.Length == 0) return;
        var type = parts[0].Trim();
        if (type != "0") return;
        if (parts.Length < 3) return;
        var filename = parts[2].Trim().Trim('"');
        if (filename.Length > 0 && string.IsNullOrEmpty(beatmap.BackgroundPath))
            beatmap.BackgroundPath = filename;
    }

    private static int CompareControlPoints(ControlPoint a, ControlPoint b)
    {
        var byOffset = a.Offset.CompareTo(b.Offset);
        if (byOffset != 0) return byOffset;
        // Timing points (non-inherited) sort before inherited at the same offset so that
        // GetControlPointAt prefers the inherited point when both are present at the same time.
        return a.IsInherited.CompareTo(b.IsInherited);
    }
}
