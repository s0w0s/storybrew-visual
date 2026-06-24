using System.Globalization;
using VisualCompositor.Core.Model;
using VisualCompositor.Core.Primitives;
using VisualCompositor.Core.Snapshots;
using VisualCompositor.Core.Validation;
using VisualCompositor.Osb.Parser;

namespace VisualCompositor.Osb.Import;

/// <summary>Result of an OSB import: the built <see cref="CompositionDocument"/> plus diagnostics.</summary>
public sealed class ImportResult
{
    public CompositionDocument Document { get; } = new();
    public List<Diagnostic> Diagnostics { get; } = new();
}

/// <summary>Maps a <see cref="ParsedOsbFile"/> to a <see cref="CompositionDocument"/> per design document §7.
/// M/MX/MY → Position (component mask); S/V → Scale; R → Rotation; F → Opacity; C → Color;
/// P → ParameterTrack; L → LoopBlock; T → TriggerBlock; unknown/comment/blank → RawBlock.</summary>
public sealed class ImportMapper
{
    private sealed class LayerState
    {
        public Layer Layer { get; init; } = null!;
        public string? LastCommandId { get; set; }
    }

    /// <summary>Map a parsed .osb file into a CompositionDocument.</summary>
    public ImportResult Map(ParsedOsbFile file)
    {
        var result = new ImportResult();
        var document = result.Document;

        foreach (var kvp in file.Variables)
            document.Variables[kvp.Key] = kvp.Value;

        // Propagate parser diagnostics as warnings.
        foreach (var msg in file.Diagnostics)
            result.Diagnostics.Add(new Diagnostic
            {
                Code = "OSB_PARSE_WARNING",
                Message = msg,
                Severity = DiagnosticSeverity.Warning,
                Scope = "import",
            });

        var layerStates = new Dictionary<OsbLayer, LayerState>();
        var spriteSeq = 0;
        var cmdSeq = 0;
        var blockSeq = 0;
        var rawSeq = 0;
        OsbLayer currentLayer = OsbLayer.Background;

        foreach (var evt in file.Events)
        {
            switch (evt)
            {
                case ParsedSprite ps:
                    {
                        var osbLayer = ToOsbLayer(ps.Layer, result);
                        var layer = GetOrCreateLayer(osbLayer, document, layerStates);
                        currentLayer = osbLayer;
                        var spriteId = PersistentIdGenerator.GenerateForSprite(
                            layer.Id, osbLayer, ps.TexturePath, ps.X, ps.Y, spriteSeq++);
                        var spriteDecl = new SpriteDeclaration
                        {
                            Id = spriteId,
                            LayerId = layer.Id,
                            DeclarationType = ps.IsAnimation ? "Animation" : "Sprite",
                            OsbLayer = osbLayer,
                            Origin = (OsbOrigin)ps.Origin,
                            TexturePath = ps.TexturePath,
                            InitialPosition = new Vector2(ps.X, ps.Y),
                            FrameCount = ps.FrameCount,
                            FrameDelay = ps.FrameDelay,
                            LoopType = (OsbLoopType)ps.LoopType,
                        };
                        layer.Sprites.Add(spriteDecl);
                        ProcessSpriteChildren(ps, spriteDecl, layer, document, layerStates,
                            ref cmdSeq, ref blockSeq, ref rawSeq, result);
                        break;
                    }

                case ParsedSample sample:
                    result.Diagnostics.Add(new Diagnostic
                    {
                        Code = "OSB_IMPORT_SAMPLE_SKIPPED",
                        Message = $"Sample at line {sample.LineNumber} skipped (not represented in document model).",
                        Severity = DiagnosticSeverity.Warning,
                        Scope = "import",
                    });
                    break;

                case ParsedRawLine raw:
                    {
                        var layer = GetOrCreateLayer(currentLayer, document, layerStates);
                        CreateRawBlock(raw, layer, document, layerStates, ref rawSeq);
                        break;
                    }

                case ParsedCommand cmd:
                    result.Diagnostics.Add(new Diagnostic
                    {
                        Code = "OSB_IMPORT_ORPHAN_COMMAND",
                        Message = $"Orphan command '{cmd.CommandLetter}' at line {cmd.LineNumber} has no sprite; skipped.",
                        Severity = DiagnosticSeverity.Warning,
                        Scope = "import",
                    });
                    break;

                case ParsedLoop loop:
                    result.Diagnostics.Add(new Diagnostic
                    {
                        Code = "OSB_IMPORT_ORPHAN_BLOCK",
                        Message = $"Orphan loop at line {loop.LineNumber} has no sprite; skipped.",
                        Severity = DiagnosticSeverity.Warning,
                        Scope = "import",
                    });
                    break;

                case ParsedTrigger trigger:
                    result.Diagnostics.Add(new Diagnostic
                    {
                        Code = "OSB_IMPORT_ORPHAN_BLOCK",
                        Message = $"Orphan trigger at line {trigger.LineNumber} has no sprite; skipped.",
                        Severity = DiagnosticSeverity.Warning,
                        Scope = "import",
                    });
                    break;
            }
        }

        return result;
    }

    private void ProcessSpriteChildren(ParsedSprite ps, SpriteDeclaration spriteDecl, Layer layer,
        CompositionDocument document, Dictionary<OsbLayer, LayerState> layerStates,
        ref int cmdSeq, ref int blockSeq, ref int rawSeq, ImportResult result)
    {
        foreach (var child in ps.Commands)
        {
            switch (child)
            {
                case ParsedCommand cmd:
                    ProcessSpriteCommand(cmd, spriteDecl, layer, document, layerStates, null, ref cmdSeq);
                    break;
                case ParsedLoop loop:
                    ProcessLoop(loop, spriteDecl, layer, document, layerStates, ref cmdSeq, ref blockSeq, ref rawSeq);
                    break;
                case ParsedTrigger trigger:
                    ProcessTrigger(trigger, spriteDecl, layer, document, layerStates, ref cmdSeq, ref blockSeq, ref rawSeq);
                    break;
                case ParsedRawLine raw:
                    CreateRawBlock(raw, layer, document, layerStates, ref rawSeq);
                    break;
            }
        }
    }

    private void ProcessSpriteCommand(ParsedCommand cmd, SpriteDeclaration spriteDecl, Layer layer,
        CompositionDocument document, Dictionary<OsbLayer, LayerState> layerStates,
        string? blockId, ref int cmdSeq)
    {
        var cmdId = PersistentIdGenerator.GenerateForCommand(
            layer.Id, spriteDecl.Id, blockId, cmd.CommandLetter,
            cmd.StartTime, cmd.EndTime, cmd.StartValue, cmd.EndValue, cmdSeq++);

        document.CommandRecords[cmdId] = new CommandRecord
        {
            CommandId = cmdId,
            Lifecycle = CommandRecordLifecycle.ImportedUnchanged,
            ParentBlockId = blockId,
            ParentLayerId = layer.Id,
            SourceReference = new SourceReferenceSnapshot { SourceLine = cmd.LineNumber },
        };
        layerStates[layer.OsbLayer].LastCommandId = cmdId;

        AddKeyframesToTrack(cmd, spriteDecl);
    }

    private void ProcessLoop(ParsedLoop parsedLoop, SpriteDeclaration spriteDecl, Layer layer,
        CompositionDocument document, Dictionary<OsbLayer, LayerState> layerStates,
        ref int cmdSeq, ref int blockSeq, ref int rawSeq)
    {
        var blockId = PersistentIdGenerator.GenerateForBlock(
            layer.Id, spriteDecl.Id, "Loop", parsedLoop.StartTime, parsedLoop.LoopCount, null, blockSeq++);
        var headerCmdId = PersistentIdGenerator.GenerateForCommand(
            layer.Id, spriteDecl.Id, blockId, "L", parsedLoop.StartTime, parsedLoop.LoopCount, "", "", cmdSeq++);

        document.CommandRecords[headerCmdId] = new CommandRecord
        {
            CommandId = headerCmdId,
            Lifecycle = CommandRecordLifecycle.ImportedUnchanged,
            ParentBlockId = blockId,
            ParentLayerId = layer.Id,
            SourceReference = new SourceReferenceSnapshot { SourceLine = parsedLoop.LineNumber },
        };
        layerStates[layer.OsbLayer].LastCommandId = headerCmdId;

        var loopBlock = new LoopBlock
        {
            Id = blockId,
            HeaderCommandId = headerCmdId,
            LayerId = layer.Id,
            StartTime = parsedLoop.StartTime,
            LoopCount = parsedLoop.LoopCount,
        };

        foreach (var child in parsedLoop.Commands)
        {
            switch (child)
            {
                case ParsedCommand cmd:
                    var relCmdId = PersistentIdGenerator.GenerateForCommand(
                        layer.Id, spriteDecl.Id, blockId, cmd.CommandLetter,
                        cmd.StartTime, cmd.EndTime, cmd.StartValue, cmd.EndValue, cmdSeq++);
                    document.CommandRecords[relCmdId] = new CommandRecord
                    {
                        CommandId = relCmdId,
                        Lifecycle = CommandRecordLifecycle.ImportedUnchanged,
                        ParentBlockId = blockId,
                        ParentLayerId = layer.Id,
                        SourceReference = new SourceReferenceSnapshot { SourceLine = cmd.LineNumber },
                    };
                    layerStates[layer.OsbLayer].LastCommandId = relCmdId;

                    loopBlock.RelativeCommands.Add(new RelativeCommand
                    {
                        Id = relCmdId,
                        CommandType = cmd.CommandLetter,
                        Easing = (OsbEasing)cmd.Easing,
                        StartTime = cmd.StartTime,
                        EndTime = cmd.EndTime,
                        StartValue = cmd.StartValue,
                        EndValue = cmd.EndValue,
                        ParentBlockId = blockId,
                    });
                    break;

                case ParsedRawLine raw:
                    CreateRawBlock(raw, layer, document, layerStates, ref rawSeq);
                    break;
            }
        }

        spriteDecl.Blocks.Add(loopBlock);
    }

    private void ProcessTrigger(ParsedTrigger parsedTrigger, SpriteDeclaration spriteDecl, Layer layer,
        CompositionDocument document, Dictionary<OsbLayer, LayerState> layerStates,
        ref int cmdSeq, ref int blockSeq, ref int rawSeq)
    {
        var blockId = PersistentIdGenerator.GenerateForBlock(
            layer.Id, spriteDecl.Id, "Trigger", parsedTrigger.StartTime, parsedTrigger.EndTime,
            parsedTrigger.TriggerName, blockSeq++);
        var headerCmdId = PersistentIdGenerator.GenerateForCommand(
            layer.Id, spriteDecl.Id, blockId, "T", parsedTrigger.StartTime, parsedTrigger.EndTime,
            parsedTrigger.TriggerName, parsedTrigger.Group.ToString(CultureInfo.InvariantCulture), cmdSeq++);

        document.CommandRecords[headerCmdId] = new CommandRecord
        {
            CommandId = headerCmdId,
            Lifecycle = CommandRecordLifecycle.ImportedUnchanged,
            ParentBlockId = blockId,
            ParentLayerId = layer.Id,
            SourceReference = new SourceReferenceSnapshot { SourceLine = parsedTrigger.LineNumber },
        };
        layerStates[layer.OsbLayer].LastCommandId = headerCmdId;

        var triggerBlock = new TriggerBlock
        {
            Id = blockId,
            HeaderCommandId = headerCmdId,
            LayerId = layer.Id,
            TriggerName = parsedTrigger.TriggerName,
            StartTime = parsedTrigger.StartTime,
            EndTime = parsedTrigger.EndTime,
            Group = parsedTrigger.Group,
        };

        foreach (var child in parsedTrigger.Commands)
        {
            switch (child)
            {
                case ParsedCommand cmd:
                    var relCmdId = PersistentIdGenerator.GenerateForCommand(
                        layer.Id, spriteDecl.Id, blockId, cmd.CommandLetter,
                        cmd.StartTime, cmd.EndTime, cmd.StartValue, cmd.EndValue, cmdSeq++);
                    document.CommandRecords[relCmdId] = new CommandRecord
                    {
                        CommandId = relCmdId,
                        Lifecycle = CommandRecordLifecycle.ImportedUnchanged,
                        ParentBlockId = blockId,
                        ParentLayerId = layer.Id,
                        SourceReference = new SourceReferenceSnapshot { SourceLine = cmd.LineNumber },
                    };
                    layerStates[layer.OsbLayer].LastCommandId = relCmdId;

                    triggerBlock.RelativeCommands.Add(new RelativeCommand
                    {
                        Id = relCmdId,
                        CommandType = cmd.CommandLetter,
                        Easing = (OsbEasing)cmd.Easing,
                        StartTime = cmd.StartTime,
                        EndTime = cmd.EndTime,
                        StartValue = cmd.StartValue,
                        EndValue = cmd.EndValue,
                        ParentBlockId = blockId,
                    });
                    break;

                case ParsedRawLine raw:
                    CreateRawBlock(raw, layer, document, layerStates, ref rawSeq);
                    break;
            }
        }

        spriteDecl.Blocks.Add(triggerBlock);
    }

    private void CreateRawBlock(ParsedRawLine raw, Layer layer, CompositionDocument document,
        Dictionary<OsbLayer, LayerState> layerStates, ref int rawSeq)
    {
        var rawId = PersistentIdGenerator.GenerateForRawBlock(layer.Id, raw.Content, rawSeq++);
        var state = layerStates[layer.OsbLayer];

        RawBlockAnchorKind anchorKind;
        string? anchorCommandId;
        if (state.LastCommandId == null)
        {
            anchorKind = RawBlockAnchorKind.LayerStart;
            anchorCommandId = null;
        }
        else
        {
            anchorKind = RawBlockAnchorKind.AfterCommand;
            anchorCommandId = state.LastCommandId;
        }

        document.RawBlocks.Add(new RawBlock
        {
            Id = rawId,
            Content = raw.Content,
            AnchorKind = anchorKind,
            AnchorCommandId = anchorCommandId,
            LayerId = layer.Id,
        });
    }

    private static Layer GetOrCreateLayer(OsbLayer osbLayer, CompositionDocument document,
        Dictionary<OsbLayer, LayerState> layerStates)
    {
        if (layerStates.TryGetValue(osbLayer, out var existing))
            return existing.Layer;

        var layer = new Layer
        {
            Id = PersistentIdGenerator.GenerateLayerId(osbLayer, 0),
            Name = osbLayer.ToString(),
            OsbLayer = osbLayer,
        };
        document.Layers.Add(layer);
        layerStates[osbLayer] = new LayerState { Layer = layer };
        return layer;
    }

    private static void AddKeyframesToTrack(ParsedCommand cmd, SpriteDeclaration spriteDecl)
    {
        var easing = (OsbEasing)cmd.Easing;
        switch (cmd.CommandLetter)
        {
            case "M":
                {
                    var track = GetOrCreateTrack(spriteDecl, "Position", Vector2ComponentMask.Both, "Vector2");
                    var start = ParseVector2(cmd.StartValue);
                    var end = ParseVector2(cmd.EndValue);
                    track.Vector2Keyframes.Add(new Keyframe<Vector2> { Time = cmd.StartTime, Value = start, Easing = easing });
                    if (cmd.StartTime != cmd.EndTime)
                        track.Vector2Keyframes.Add(new Keyframe<Vector2> { Time = cmd.EndTime, Value = end });
                    break;
                }
            case "MX":
                {
                    var track = GetOrCreateTrack(spriteDecl, "Position", Vector2ComponentMask.X, "Float");
                    var start = ParseFloat(cmd.StartValue);
                    var end = ParseFloat(cmd.EndValue);
                    track.FloatKeyframes.Add(new Keyframe<float> { Time = cmd.StartTime, Value = start, Easing = easing });
                    if (cmd.StartTime != cmd.EndTime)
                        track.FloatKeyframes.Add(new Keyframe<float> { Time = cmd.EndTime, Value = end });
                    break;
                }
            case "MY":
                {
                    var track = GetOrCreateTrack(spriteDecl, "Position", Vector2ComponentMask.Y, "Float");
                    var start = ParseFloat(cmd.StartValue);
                    var end = ParseFloat(cmd.EndValue);
                    track.FloatKeyframes.Add(new Keyframe<float> { Time = cmd.StartTime, Value = start, Easing = easing });
                    if (cmd.StartTime != cmd.EndTime)
                        track.FloatKeyframes.Add(new Keyframe<float> { Time = cmd.EndTime, Value = end });
                    break;
                }
            case "S":
                {
                    var track = GetOrCreateTrack(spriteDecl, "Scale", Vector2ComponentMask.Both, "Vector2");
                    var start = ParseFloat(cmd.StartValue);
                    var end = ParseFloat(cmd.EndValue);
                    track.Vector2Keyframes.Add(new Keyframe<Vector2> { Time = cmd.StartTime, Value = new Vector2(start, start), Easing = easing });
                    if (cmd.StartTime != cmd.EndTime)
                        track.Vector2Keyframes.Add(new Keyframe<Vector2> { Time = cmd.EndTime, Value = new Vector2(end, end) });
                    break;
                }
            case "V":
                {
                    var track = GetOrCreateTrack(spriteDecl, "Scale", Vector2ComponentMask.Both, "Vector2");
                    var start = ParseVector2(cmd.StartValue);
                    var end = ParseVector2(cmd.EndValue);
                    track.Vector2Keyframes.Add(new Keyframe<Vector2> { Time = cmd.StartTime, Value = start, Easing = easing });
                    if (cmd.StartTime != cmd.EndTime)
                        track.Vector2Keyframes.Add(new Keyframe<Vector2> { Time = cmd.EndTime, Value = end });
                    break;
                }
            case "R":
                {
                    var track = GetOrCreateTrack(spriteDecl, "Rotation", Vector2ComponentMask.None, "Float");
                    var start = ParseFloat(cmd.StartValue);
                    var end = ParseFloat(cmd.EndValue);
                    track.FloatKeyframes.Add(new Keyframe<float> { Time = cmd.StartTime, Value = start, Easing = easing });
                    if (cmd.StartTime != cmd.EndTime)
                        track.FloatKeyframes.Add(new Keyframe<float> { Time = cmd.EndTime, Value = end });
                    break;
                }
            case "F":
                {
                    var track = GetOrCreateTrack(spriteDecl, "Opacity", Vector2ComponentMask.None, "Float");
                    var start = ParseFloat(cmd.StartValue);
                    var end = ParseFloat(cmd.EndValue);
                    track.FloatKeyframes.Add(new Keyframe<float> { Time = cmd.StartTime, Value = start, Easing = easing });
                    if (cmd.StartTime != cmd.EndTime)
                        track.FloatKeyframes.Add(new Keyframe<float> { Time = cmd.EndTime, Value = end });
                    break;
                }
            case "C":
                {
                    var track = GetOrCreateTrack(spriteDecl, "Color", Vector2ComponentMask.None, "Color");
                    var start = ParseColor(cmd.StartValue);
                    var end = ParseColor(cmd.EndValue);
                    track.ColorKeyframes.Add(new Keyframe<Color3> { Time = cmd.StartTime, Value = start, Easing = easing });
                    if (cmd.StartTime != cmd.EndTime)
                        track.ColorKeyframes.Add(new Keyframe<Color3> { Time = cmd.EndTime, Value = end });
                    break;
                }
            case "P":
                {
                    var paramType = ParseParameter(cmd.StartValue);
                    spriteDecl.ParameterTrack ??= new ParameterTrack();
                    spriteDecl.ParameterTrack.Segments.Add(new ParameterSegment
                    {
                        Parameter = paramType,
                        StartTime = cmd.StartTime,
                        EndTime = cmd.EndTimeIsEmpty ? null : cmd.EndTime,
                        OpenEndedMode = cmd.EndTimeIsEmpty ? OpenEndedMode.UntilLayerEnd : OpenEndedMode.ExplicitEnd,
                    });
                    break;
                }
        }
    }

    private static PropertyTrack GetOrCreateTrack(SpriteDeclaration spriteDecl, string propertyName,
        Vector2ComponentMask mask, string valueType)
    {
        foreach (var t in spriteDecl.PropertyTracks)
        {
            if (t.PropertyName == propertyName && t.ComponentMask == mask)
                return t;
        }
        var track = new PropertyTrack
        {
            PropertyName = propertyName,
            ComponentMask = mask,
            ValueType = valueType,
        };
        spriteDecl.PropertyTracks.Add(track);
        return track;
    }

    private static OsbLayer ToOsbLayer(int value, ImportResult result)
    {
        if (value >= 0 && value <= 4)
            return (OsbLayer)value;
        result.Diagnostics.Add(new Diagnostic
        {
            Code = "OSB_IMPORT_INVALID_LAYER",
            Message = $"Invalid layer value {value}; defaulting to Background.",
            Severity = DiagnosticSeverity.Warning,
            Scope = "import",
        });
        return OsbLayer.Background;
    }

    private static float ParseFloat(string s)
    {
        if (float.TryParse(s.AsSpan().Trim(), NumberStyles.Float | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var v))
            return v;
        return 0f;
    }

    private static Vector2 ParseVector2(string s)
    {
        var parts = s.Split(',');
        var x = parts.Length > 0 ? ParseFloat(parts[0]) : 0f;
        var y = parts.Length > 1 ? ParseFloat(parts[1]) : 0f;
        return new Vector2(x, y);
    }

    private static Color3 ParseColor(string s)
    {
        var parts = s.Split(',');
        var r = parts.Length > 0 ? ParseFloat(parts[0]) / 255f : 0f;
        var g = parts.Length > 1 ? ParseFloat(parts[1]) / 255f : 0f;
        var b = parts.Length > 2 ? ParseFloat(parts[2]) / 255f : 0f;
        return new Color3(r, g, b);
    }

    private static ParameterType ParseParameter(string s)
    {
        return s.Trim() switch
        {
            "A" => ParameterType.AdditiveBlending,
            "H" => ParameterType.FlipHorizontal,
            "V" => ParameterType.FlipVertical,
            _ => ParameterType.None,
        };
    }
}
