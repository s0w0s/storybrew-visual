using VisualCompositor.Core.Model;
using VisualCompositor.Core.Primitives;
using VisualCompositor.Core.Validation;

namespace VisualCompositor.Osb.Export;

/// <summary>Exports a single <see cref="SpriteDeclaration"/> to a list of <see cref="OutputLine"/>s.
/// Handles SubTasks 27.1 (Position M/MX+MY), 27.2 (Scale S/V), 27.3 (Loop/Trigger),
/// and basic emission of R/F/C/P commands.</summary>
internal sealed class SpriteExporter
{
    private readonly BezierExporter _bezier;
    private readonly ExportOptions _options;

    private const string SpriteIndent = " ";
    private const string BlockIndent = "  ";

    public SpriteExporter(BezierExporter bezier, ExportOptions options)
    {
        _bezier = bezier;
        _options = options;
    }

    /// <summary>Export a sprite declaration and all its commands/blocks to output lines.
    /// Sprite-level command lines are emitted with <c>CommandId == null</c>; the caller is
    /// responsible for assigning command ids after collecting all lines for the layer.</summary>
    public List<OutputLine> Export(SpriteDeclaration sprite)
    {
        var lines = new List<OutputLine>();
        lines.Add(new OutputLine(FormatDeclarationLine(sprite)));
        EmitPositionCommands(lines, sprite);
        EmitScaleCommands(lines, sprite);
        EmitRotationCommands(lines, sprite);
        EmitOpacityCommands(lines, sprite);
        EmitColorCommands(lines, sprite);
        EmitParameterCommands(lines, sprite);
        EmitBlocks(lines, sprite);
        return lines;
    }

    // ---------- Declaration line ----------

    private static string FormatDeclarationLine(SpriteDeclaration sprite)
    {
        var layerName = OsbFormat.LayerName(sprite.OsbLayer);
        var originName = OsbFormat.OriginName(sprite.Origin);
        var path = "\"" + sprite.TexturePath + "\"";
        var x = OsbFormat.FormatFloat(sprite.InitialPosition.X);
        var y = OsbFormat.FormatFloat(sprite.InitialPosition.Y);

        if (sprite.DeclarationType == "Animation")
        {
            var frameCount = sprite.FrameCount.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var frameDelay = OsbFormat.FormatDouble(sprite.FrameDelay);
            var loopType = OsbFormat.LoopTypeName(sprite.LoopType);
            return $"Animation,{layerName},{originName},{path},{x},{y},{frameCount},{frameDelay},{loopType}";
        }
        return $"Sprite,{layerName},{originName},{path},{x},{y}";
    }

    // ---------- Position (SubTask 27.1) ----------

    private void EmitPositionCommands(List<OutputLine> lines, SpriteDeclaration sprite)
    {
        var positionTracks = sprite.PropertyTracks
            .Where(t => t.PropertyName == "Position")
            .ToList();
        if (positionTracks.Count == 0)
            return;

        var bothTrack = positionTracks.FirstOrDefault(t => t.ComponentMask == Vector2ComponentMask.Both);
        var xTrack = positionTracks.FirstOrDefault(t => t.ComponentMask == Vector2ComponentMask.X);
        var yTrack = positionTracks.FirstOrDefault(t => t.ComponentMask == Vector2ComponentMask.Y);

        if (bothTrack != null)
        {
            // Single Vector2 track -> M commands.
            EmitVector2Commands(lines, "M", bothTrack);
            // If there are also X or Y tracks, emit them separately (rare).
            if (xTrack != null) EmitFloatCommands(lines, "MX", xTrack);
            if (yTrack != null) EmitFloatCommands(lines, "MY", yTrack);
            return;
        }

        if (xTrack != null && yTrack != null)
        {
            // Check alignment: if keyframe boundaries, easings, and handles all match -> merge to M.
            if (AreFloatTracksAligned(xTrack.FloatKeyframes, yTrack.FloatKeyframes))
            {
                EmitMergedPositionCommands(lines, xTrack.FloatKeyframes, yTrack.FloatKeyframes);
            }
            else
            {
                EmitFloatCommands(lines, "MX", xTrack);
                EmitFloatCommands(lines, "MY", yTrack);
            }
            return;
        }

        // Only X or only Y.
        if (xTrack != null) EmitFloatCommands(lines, "MX", xTrack);
        if (yTrack != null) EmitFloatCommands(lines, "MY", yTrack);
    }

    /// <summary>Check if two float keyframe lists are fully aligned (same count, times, easings, handles).</summary>
    private static bool AreFloatTracksAligned(List<Keyframe<float>> x, List<Keyframe<float>> y)
    {
        if (x.Count != y.Count)
            return false;
        for (var i = 0; i < x.Count; i++)
        {
            var a = x[i];
            var b = y[i];
            if (a.Time != b.Time) return false;
            if (a.Easing != b.Easing) return false;
            if (!HandlesEqual(a.Handles, b.Handles)) return false;
        }
        return true;
    }

    private static bool HandlesEqual(BezierHandles? a, BezierHandles? b)
    {
        if (a == null && b == null) return true;
        if (a == null || b == null) return false;
        return a.InHandle == b.InHandle && a.OutHandle == b.OutHandle;
    }

    /// <summary>Emit M commands by merging aligned X and Y float keyframe lists.</summary>
    private void EmitMergedPositionCommands(List<OutputLine> lines,
        List<Keyframe<float>> xKeyframes, List<Keyframe<float>> yKeyframes)
    {
        // Build merged Vector2 keyframes (preserving handles from X track; they're equal).
        var merged = new List<Keyframe<Vector2>>(xKeyframes.Count);
        for (var i = 0; i < xKeyframes.Count; i++)
        {
            var xk = xKeyframes[i];
            var yk = yKeyframes[i];
            merged.Add(new Keyframe<Vector2>
            {
                Time = xk.Time,
                Value = new Vector2(xk.Value, yk.Value),
                Easing = xk.Easing,
                Handles = xk.Handles,
            });
        }
        var processed = _bezier.ProcessVector2(merged);
        EmitVector2Segments(lines, "M", processed);
    }

    // ---------- Scale (SubTask 27.2) ----------

    private void EmitScaleCommands(List<OutputLine> lines, SpriteDeclaration sprite)
    {
        var scaleTrack = sprite.PropertyTracks
            .FirstOrDefault(t => t.PropertyName == "Scale" && t.ComponentMask == Vector2ComponentMask.Both);
        if (scaleTrack == null)
            return;

        var processed = _bezier.ProcessVector2(scaleTrack.Vector2Keyframes);
        if (IsUniformScale(processed))
            EmitVector2AsUniformScale(lines, "S", processed);
        else
            EmitVector2Segments(lines, "V", processed);
    }

    /// <summary>Check if every keyframe has X == Y (uniform scale).</summary>
    private static bool IsUniformScale(List<Keyframe<Vector2>> keyframes)
    {
        foreach (var kf in keyframes)
            if (kf.Value.X != kf.Value.Y)
                return false;
        return true;
    }

    // ---------- Rotation ----------

    private void EmitRotationCommands(List<OutputLine> lines, SpriteDeclaration sprite)
    {
        var track = sprite.PropertyTracks.FirstOrDefault(t => t.PropertyName == "Rotation");
        if (track == null) return;
        var processed = _bezier.ProcessFloat(track.FloatKeyframes);
        EmitFloatSegments(lines, "R", processed);
    }

    // ---------- Opacity ----------

    private void EmitOpacityCommands(List<OutputLine> lines, SpriteDeclaration sprite)
    {
        var track = sprite.PropertyTracks.FirstOrDefault(t => t.PropertyName == "Opacity");
        if (track == null) return;
        var processed = _bezier.ProcessFloat(track.FloatKeyframes);
        EmitFloatSegments(lines, "F", processed);
    }

    // ---------- Color ----------

    private void EmitColorCommands(List<OutputLine> lines, SpriteDeclaration sprite)
    {
        var track = sprite.PropertyTracks.FirstOrDefault(t => t.PropertyName == "Color");
        if (track == null) return;
        var processed = _bezier.ProcessColor(track.ColorKeyframes);
        EmitColorSegments(lines, processed);
    }

    // ---------- Parameter ----------

    private void EmitParameterCommands(List<OutputLine> lines, SpriteDeclaration sprite)
    {
        if (sprite.ParameterTrack == null) return;
        foreach (var seg in sprite.ParameterTrack.Segments)
        {
            if (seg.Parameter == ParameterType.None) continue;
            var letter = OsbFormat.ParameterLetter(seg.Parameter);
            if (string.IsNullOrEmpty(letter)) continue;
            var startTime = OsbFormat.FormatTime(seg.StartTime);
            // Open-ended (null EndTime) -> empty end time field.
            var endTime = seg.EndTime == null ? "" : OsbFormat.FormatTime(seg.EndTime.Value);
            lines.Add(new OutputLine($"{SpriteIndent}P,0,{startTime},{endTime},{letter}"));
        }
    }

    // ---------- Blocks (SubTask 27.3) ----------

    private void EmitBlocks(List<OutputLine> lines, SpriteDeclaration sprite)
    {
        foreach (var block in sprite.Blocks)
        {
            switch (block)
            {
                case LoopBlock loop:
                    EmitLoopBlock(lines, loop);
                    break;
                case TriggerBlock trigger:
                    EmitTriggerBlock(lines, trigger);
                    break;
            }
        }
    }

    private void EmitLoopBlock(List<OutputLine> lines, LoopBlock loop)
    {
        var startTime = OsbFormat.FormatTime(loop.StartTime);
        var loopCount = loop.LoopCount.ToString(System.Globalization.CultureInfo.InvariantCulture);
        lines.Add(new OutputLine($"{SpriteIndent}L,{startTime},{loopCount}", loop.HeaderCommandId));
        foreach (var rel in loop.RelativeCommands)
            lines.Add(new OutputLine(FormatRelativeCommand(rel), rel.Id));
    }

    private void EmitTriggerBlock(List<OutputLine> lines, TriggerBlock trigger)
    {
        var startTime = OsbFormat.FormatTime(trigger.StartTime);
        var endTime = OsbFormat.FormatTime(trigger.EndTime);
        var header = $"{SpriteIndent}T,{trigger.TriggerName},{startTime},{endTime}";
        if (_options.AlwaysEmitTriggerGroup || trigger.Group != 0)
            header += "," + trigger.Group.ToString(System.Globalization.CultureInfo.InvariantCulture);
        lines.Add(new OutputLine(header, trigger.HeaderCommandId));
        foreach (var rel in trigger.RelativeCommands)
            lines.Add(new OutputLine(FormatRelativeCommand(rel), rel.Id));
    }

    /// <summary>Format a relative command line (2-space indent, original string values preserved).</summary>
    private static string FormatRelativeCommand(RelativeCommand rel)
    {
        var easing = OsbFormat.FormatEasing(rel.Easing);
        var startTime = OsbFormat.FormatTime(rel.StartTime);
        var isParameter = rel.CommandType == "P";

        if (isParameter)
        {
            // P has no end value; empty end time if start == end (open-ended).
            var endTime = rel.EndTime == rel.StartTime ? "" : OsbFormat.FormatTime(rel.EndTime);
            return $"{BlockIndent}P,{easing},{startTime},{endTime},{rel.StartValue}";
        }

        var endT = OsbFormat.FormatTime(rel.EndTime);
        // Omit end value if it equals start value (matches typical .osb style).
        if (rel.StartValue == rel.EndValue)
            return $"{BlockIndent}{rel.CommandType},{easing},{startTime},{endT},{rel.StartValue}";
        return $"{BlockIndent}{rel.CommandType},{easing},{startTime},{endT},{rel.StartValue},{rel.EndValue}";
    }

    // ---------- Segment emission helpers ----------

    private void EmitVector2Commands(List<OutputLine> lines, string letter, PropertyTrack track)
    {
        var processed = _bezier.ProcessVector2(track.Vector2Keyframes);
        EmitVector2Segments(lines, letter, processed);
    }

    private void EmitFloatCommands(List<OutputLine> lines, string letter, PropertyTrack track)
    {
        var processed = _bezier.ProcessFloat(track.FloatKeyframes);
        EmitFloatSegments(lines, letter, processed);
    }

    private void EmitVector2Segments(List<OutputLine> lines, string letter, List<Keyframe<Vector2>> keyframes)
    {
        var deduped = DeduplicateByKeyTime(keyframes);
        if (deduped.Count == 0) return;

        if (deduped.Count == 1)
        {
            var kf = deduped[0];
            lines.Add(new OutputLine(FormatCommand(letter, kf.Easing, kf.Time, kf.Time,
                OsbFormat.FormatVector2(kf.Value), null)));
            return;
        }

        for (var i = 0; i < deduped.Count - 1; i++)
        {
            var start = deduped[i];
            var end = deduped[i + 1];
            if (start.Time == end.Time) continue; // skip zero-length
            var startVal = OsbFormat.FormatVector2(start.Value);
            var endVal = OsbFormat.FormatVector2(end.Value);
            string? endValue = start.Value == end.Value ? null : endVal;
            lines.Add(new OutputLine(FormatCommand(letter, start.Easing, start.Time, end.Time, startVal, endValue)));
        }
    }

    private void EmitVector2AsUniformScale(List<OutputLine> lines, string letter, List<Keyframe<Vector2>> keyframes)
    {
        var deduped = DeduplicateByKeyTime(keyframes);
        if (deduped.Count == 0) return;

        if (deduped.Count == 1)
        {
            var kf = deduped[0];
            lines.Add(new OutputLine(FormatCommand(letter, kf.Easing, kf.Time, kf.Time,
                OsbFormat.FormatFloat(kf.Value.X), null)));
            return;
        }

        for (var i = 0; i < deduped.Count - 1; i++)
        {
            var start = deduped[i];
            var end = deduped[i + 1];
            if (start.Time == end.Time) continue;
            var startVal = OsbFormat.FormatFloat(start.Value.X);
            var endVal = OsbFormat.FormatFloat(end.Value.X);
            string? endValue = start.Value.X == end.Value.X ? null : endVal;
            lines.Add(new OutputLine(FormatCommand(letter, start.Easing, start.Time, end.Time, startVal, endValue)));
        }
    }

    private void EmitFloatSegments(List<OutputLine> lines, string letter, List<Keyframe<float>> keyframes)
    {
        var deduped = DeduplicateByKeyTime(keyframes);
        if (deduped.Count == 0) return;

        if (deduped.Count == 1)
        {
            var kf = deduped[0];
            lines.Add(new OutputLine(FormatCommand(letter, kf.Easing, kf.Time, kf.Time,
                OsbFormat.FormatFloat(kf.Value), null)));
            return;
        }

        for (var i = 0; i < deduped.Count - 1; i++)
        {
            var start = deduped[i];
            var end = deduped[i + 1];
            if (start.Time == end.Time) continue;
            var startVal = OsbFormat.FormatFloat(start.Value);
            var endVal = OsbFormat.FormatFloat(end.Value);
            string? endValue = Math.Abs(start.Value - end.Value) < float.Epsilon ? null : endVal;
            lines.Add(new OutputLine(FormatCommand(letter, start.Easing, start.Time, end.Time, startVal, endValue)));
        }
    }

    private void EmitColorSegments(List<OutputLine> lines, List<Keyframe<Color3>> keyframes)
    {
        var deduped = DeduplicateByKeyTime(keyframes);
        if (deduped.Count == 0) return;

        if (deduped.Count == 1)
        {
            var kf = deduped[0];
            lines.Add(new OutputLine(FormatCommand("C", kf.Easing, kf.Time, kf.Time,
                OsbFormat.FormatColor(kf.Value), null)));
            return;
        }

        for (var i = 0; i < deduped.Count - 1; i++)
        {
            var start = deduped[i];
            var end = deduped[i + 1];
            if (start.Time == end.Time) continue;
            var startVal = OsbFormat.FormatColor(start.Value);
            var endVal = OsbFormat.FormatColor(end.Value);
            string? endValue = start.Value == end.Value ? null : endVal;
            lines.Add(new OutputLine(FormatCommand("C", start.Easing, start.Time, end.Time, startVal, endValue)));
        }
    }

    /// <summary>Format a sprite-level command line: ` _<letter>,<easing>,<start>,<end>,<startVal>[,<endVal>]`.
    /// If <paramref name="endValue"/> is null, it is omitted (start value used for both endpoints).</summary>
    private static string FormatCommand(string letter, OsbEasing easing, double startTime, double endTime,
        string startValue, string? endValue)
    {
        var easingStr = OsbFormat.FormatEasing(easing);
        var startStr = OsbFormat.FormatTime(startTime);
        var endStr = OsbFormat.FormatTime(endTime);
        return endValue == null
            ? $"{SpriteIndent}{letter},{easingStr},{startStr},{endStr},{startValue}"
            : $"{SpriteIndent}{letter},{easingStr},{startStr},{endStr},{startValue},{endValue}";
    }

    // ---------- Keyframe deduplication ----------

    /// <summary>Sort keyframes by time (stable) and remove duplicates at the same time,
    /// keeping the last one (which carries the easing for the next segment).</summary>
    private static List<Keyframe<T>> DeduplicateByKeyTime<T>(List<Keyframe<T>> keyframes)
    {
        if (keyframes.Count == 0)
            return keyframes;

        // Stable sort by time: use Select with index to preserve insertion order for ties.
        var indexed = keyframes.Select((kf, i) => (kf, i)).OrderBy(x => x.kf.Time, Comparer<double>.Default)
            .ThenBy(x => x.i).ToList();
        var result = new List<Keyframe<T>>(keyframes.Count);
        foreach (var (kf, _) in indexed)
        {
            if (result.Count > 0 && result[^1].Time == kf.Time)
                result[^1] = kf; // replace with later keyframe at same time
            else
                result.Add(kf);
        }
        return result;
    }
}
