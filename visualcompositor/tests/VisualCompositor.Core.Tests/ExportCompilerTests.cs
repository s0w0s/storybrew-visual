using VisualCompositor.Core.Model;
using VisualCompositor.Core.Primitives;
using VisualCompositor.Core.Validation;
using VisualCompositor.Osb.Export;
using VisualCompositor.Osb.Import;
using VisualCompositor.Osb.Parser;
using Xunit;

namespace VisualCompositor.Core.Tests;

/// <summary>Phase 7 (Task 28) tests for the .osb export compiler.
/// Verifies import→export round-trip fidelity, M/MX+MY and S/V optimizations,
/// RawBlock anchor reinsertion, pre-export validation, and bezier export modes.</summary>
public class ExportCompilerTests
{
    // ---------- helpers ----------

    private static CompositionDocument BuildDocumentWithSprite(Action<SpriteDeclaration>? configure = null)
    {
        var doc = new CompositionDocument();
        var layer = new Layer { Id = "layer_bg", Name = "Background", OsbLayer = OsbLayer.Background };
        doc.Layers.Add(layer);
        var sprite = new SpriteDeclaration
        {
            Id = "spr_1",
            LayerId = layer.Id,
            DeclarationType = "Sprite",
            OsbLayer = OsbLayer.Background,
            Origin = OsbOrigin.Centre,
            TexturePath = "bg.png",
            InitialPosition = new Vector2(320, 240),
        };
        configure?.Invoke(sprite);
        layer.Sprites.Add(sprite);
        return doc;
    }

    private static (ExportResult result, CompositionDocument doc) ImportAndExport(string osb, ExportOptions? opts = null)
    {
        var parsed = new OsbParser().Parse(osb);
        var import = new ImportMapper().Map(parsed);
        var export = new OsbExportCompiler().Compile(import.Document, opts);
        return (export, import.Document);
    }

    /// <summary>Build a document with a single sprite containing a Loop block, with all the
    /// CommandRecords required to pass pre-export validation. Returns the loop block so callers
    /// can reference its HeaderCommandId for RawBlock anchoring.</summary>
    private static (CompositionDocument doc, LoopBlock loop) BuildDocumentWithLoop()
    {
        var doc = new CompositionDocument();
        var layer = new Layer { Id = "layer_fg", Name = "Foreground", OsbLayer = OsbLayer.Foreground };
        doc.Layers.Add(layer);
        var sprite = new SpriteDeclaration
        {
            Id = "spr_1",
            LayerId = layer.Id,
            DeclarationType = "Sprite",
            OsbLayer = OsbLayer.Foreground,
            Origin = OsbOrigin.Centre,
            TexturePath = "bg.png",
            InitialPosition = new Vector2(320, 240),
        };
        layer.Sprites.Add(sprite);

        var loop = new LoopBlock
        {
            Id = "blk_loop_1",
            HeaderCommandId = "cmd_loop_hdr",
            LayerId = layer.Id,
            StartTime = 1000,
            LoopCount = 2,
        };
        var rel = new RelativeCommand
        {
            Id = "cmd_loop_rel_1",
            CommandType = "F",
            Easing = OsbEasing.None,
            StartTime = 0,
            EndTime = 500,
            StartValue = "0",
            EndValue = "1",
            ParentBlockId = loop.Id,
        };
        loop.RelativeCommands.Add(rel);
        sprite.Blocks.Add(loop);

        doc.CommandRecords[loop.HeaderCommandId] = new CommandRecord
        {
            CommandId = loop.HeaderCommandId,
            Lifecycle = CommandRecordLifecycle.ImportedUnchanged,
            ParentBlockId = loop.Id,
            ParentLayerId = layer.Id,
        };
        doc.CommandRecords[rel.Id] = new CommandRecord
        {
            CommandId = rel.Id,
            Lifecycle = CommandRecordLifecycle.ImportedUnchanged,
            ParentBlockId = loop.Id,
            ParentLayerId = layer.Id,
        };

        return (doc, loop);
    }

    private static void AssertSucceeded(ExportResult result)
    {
        Assert.True(result.Succeeded,
            result.Succeeded ? "" : string.Join("\n", result.Diagnostics.Select(d => d.ToString())));
    }

    // ============================================================
    // SubTask 28.1: Import→export round-trip fidelity
    // ============================================================

    [Fact]
    public void RoundTrip_MCommand_ProducesMoveLineWithCorrectValues()
    {
        var (result, _) = ImportAndExport("""
[Events]
Sprite,3,4,"bg.jpg",320,240
 M,0,0,2000,320,240,400,240
""");
        AssertSucceeded(result);
        Assert.Contains(" M,0,0,2000,320,240,400,240", result.Text);
    }

    [Fact]
    public void RoundTrip_FCommand_ProducesFadeLineWithCorrectValues()
    {
        var (result, _) = ImportAndExport("""
[Events]
Sprite,3,4,"bg.jpg",320,240
 F,0,0,1000,0,1
""");
        AssertSucceeded(result);
        Assert.Contains(" F,0,0,1000,0,1", result.Text);
    }

    [Fact]
    public void RoundTrip_MultipleLayers_PreservedInOsbLayerOrder()
    {
        // Sprites intentionally listed out of OsbLayer order; export must emit them in
        // Background, Fail, Pass, Foreground, Overlay order.
        var (result, _) = ImportAndExport("""
[Events]
Sprite,4,4,"over.png",0,0
Sprite,0,4,"bg.png",0,0
Sprite,3,4,"fg.png",0,0
Sprite,1,4,"fail.png",0,0
Sprite,2,4,"pass.png",0,0
""");
        AssertSucceeded(result);

        var bg = result.Text.IndexOf("\"bg.png\"");
        var fail = result.Text.IndexOf("\"fail.png\"");
        var pass = result.Text.IndexOf("\"pass.png\"");
        var fg = result.Text.IndexOf("\"fg.png\"");
        var over = result.Text.IndexOf("\"over.png\"");

        Assert.True(bg >= 0 && fail >= 0 && pass >= 0 && fg >= 0 && over >= 0);
        Assert.True(bg < fail, "Background should come before Fail");
        Assert.True(fail < pass, "Fail should come before Pass");
        Assert.True(pass < fg, "Pass should come before Foreground");
        Assert.True(fg < over, "Foreground should come before Overlay");
    }

    [Fact]
    public void RoundTrip_LoopBlock_ProducesLoopHeaderAndIndentedCommands()
    {
        var (result, _) = ImportAndExport("""
[Events]
Sprite,3,4,"bg.jpg",320,240
 L,1000,3
  F,0,0,500,0,1
  F,1,500,1000,1,0
""");
        AssertSucceeded(result);
        // Loop header: 1-space indent.
        Assert.Contains(" L,1000,3", result.Text);
        // Internal commands: 2-space indent.
        Assert.Contains("  F,0,0,500,0,1", result.Text);
        Assert.Contains("  F,1,500,1000,1,0", result.Text);

        // Header must appear before its internal commands.
        Assert.True(result.Text.IndexOf(" L,1000,3") < result.Text.IndexOf("  F,0,0,500,0,1"));
    }

    [Fact]
    public void RoundTrip_TriggerBlock_ProducesTriggerHeaderAndCommands()
    {
        var (result, _) = ImportAndExport("""
[Events]
Sprite,3,4,"bg.jpg",320,240
 T,HitObjects,1000,2000,1
  S,0,0,100,1,2
""");
        AssertSucceeded(result);
        Assert.Contains(" T,HitObjects,1000,2000,1", result.Text);
        Assert.Contains("  S,0,0,100,1,2", result.Text);
        Assert.True(result.Text.IndexOf(" T,HitObjects,1000,2000,1") < result.Text.IndexOf("  S,0,0,100,1,2"));
    }

    [Fact]
    public void RoundTrip_VariablesSection_EmittedWithSortedKeys()
    {
        var (result, _) = ImportAndExport("""
[Variables]
$x=320
$y=240

[Events]
Sprite,3,4,"bg.jpg",320,240
""");
        AssertSucceeded(result);
        Assert.Contains("[Variables]", result.Text);
        Assert.Contains("$x=320", result.Text);
        Assert.Contains("$y=240", result.Text);

        // Keys sorted ordinally: $x before $y.
        Assert.True(result.Text.IndexOf("$x=320") < result.Text.IndexOf("$y=240"));
        // [Variables] section precedes [Events].
        Assert.True(result.Text.IndexOf("[Variables]") < result.Text.IndexOf("[Events]"));
    }

    // ============================================================
    // SubTask 28.2: M / MX+MY optimization
    // ============================================================

    [Fact]
    public void Position_BothMaskVector2Track_EmitsMCommand()
    {
        var doc = BuildDocumentWithSprite(s =>
        {
            s.PropertyTracks.Add(new PropertyTrack
            {
                PropertyName = "Position",
                ComponentMask = Vector2ComponentMask.Both,
                ValueType = "Vector2",
                Vector2Keyframes =
                {
                    new Keyframe<Vector2> { Time = 0, Value = new Vector2(320, 240), Easing = OsbEasing.None },
                    new Keyframe<Vector2> { Time = 1000, Value = new Vector2(400, 240), Easing = OsbEasing.None },
                },
            });
        });

        var result = new OsbExportCompiler().Compile(doc);
        AssertSucceeded(result);
        Assert.Contains(" M,0,0,1000,320,240,400,240", result.Text);
        Assert.DoesNotContain(" MX,", result.Text);
        Assert.DoesNotContain(" MY,", result.Text);
    }

    [Fact]
    public void Position_AlignedXAndYTracks_MergedIntoMCommand()
    {
        var doc = BuildDocumentWithSprite(s =>
        {
            s.PropertyTracks.Add(new PropertyTrack
            {
                PropertyName = "Position",
                ComponentMask = Vector2ComponentMask.X,
                ValueType = "Float",
                FloatKeyframes =
                {
                    new Keyframe<float> { Time = 0, Value = 320f, Easing = OsbEasing.None },
                    new Keyframe<float> { Time = 1000, Value = 400f, Easing = OsbEasing.None },
                },
            });
            s.PropertyTracks.Add(new PropertyTrack
            {
                PropertyName = "Position",
                ComponentMask = Vector2ComponentMask.Y,
                ValueType = "Float",
                FloatKeyframes =
                {
                    new Keyframe<float> { Time = 0, Value = 240f, Easing = OsbEasing.None },
                    new Keyframe<float> { Time = 1000, Value = 240f, Easing = OsbEasing.None },
                },
            });
        });

        var result = new OsbExportCompiler().Compile(doc);
        AssertSucceeded(result);
        Assert.Contains(" M,0,0,1000,320,240,400,240", result.Text);
        Assert.DoesNotContain(" MX,", result.Text);
        Assert.DoesNotContain(" MY,", result.Text);
    }

    [Fact]
    public void Position_MisalignedXAndYTracks_EmitsSeparateMxAndMy()
    {
        var doc = BuildDocumentWithSprite(s =>
        {
            s.PropertyTracks.Add(new PropertyTrack
            {
                PropertyName = "Position",
                ComponentMask = Vector2ComponentMask.X,
                ValueType = "Float",
                FloatKeyframes =
                {
                    new Keyframe<float> { Time = 0, Value = 320f, Easing = OsbEasing.None },
                    new Keyframe<float> { Time = 1000, Value = 400f, Easing = OsbEasing.None },
                },
            });
            s.PropertyTracks.Add(new PropertyTrack
            {
                PropertyName = "Position",
                ComponentMask = Vector2ComponentMask.Y,
                ValueType = "Float",
                FloatKeyframes =
                {
                    new Keyframe<float> { Time = 0, Value = 240f, Easing = OsbEasing.None },
                    new Keyframe<float> { Time = 2000, Value = 300f, Easing = OsbEasing.None },
                },
            });
        });

        var result = new OsbExportCompiler().Compile(doc);
        AssertSucceeded(result);
        Assert.Contains(" MX,0,0,1000,320,400", result.Text);
        Assert.Contains(" MY,0,0,2000,240,300", result.Text);
        // No merged M command.
        Assert.DoesNotContain(" M,0,0,1000,320,240,400,240", result.Text);
    }

    [Fact]
    public void Position_AlignedTimesButDifferentEasings_EmitsSeparateMxAndMy()
    {
        var doc = BuildDocumentWithSprite(s =>
        {
            s.PropertyTracks.Add(new PropertyTrack
            {
                PropertyName = "Position",
                ComponentMask = Vector2ComponentMask.X,
                ValueType = "Float",
                FloatKeyframes =
                {
                    new Keyframe<float> { Time = 0, Value = 320f, Easing = OsbEasing.None },
                    new Keyframe<float> { Time = 1000, Value = 400f, Easing = OsbEasing.None },
                },
            });
            s.PropertyTracks.Add(new PropertyTrack
            {
                PropertyName = "Position",
                ComponentMask = Vector2ComponentMask.Y,
                ValueType = "Float",
                FloatKeyframes =
                {
                    new Keyframe<float> { Time = 0, Value = 240f, Easing = OsbEasing.Out },
                    new Keyframe<float> { Time = 1000, Value = 240f, Easing = OsbEasing.Out },
                },
            });
        });

        var result = new OsbExportCompiler().Compile(doc);
        AssertSucceeded(result);
        Assert.Contains(" MX,0,0,1000,320,400", result.Text);
        // Y segment easing comes from the start keyframe (Out == 1); start==end so end value omitted.
        Assert.Contains(" MY,1,0,1000,240", result.Text);
        Assert.DoesNotContain(" M,0,0,1000,320,240,400,240", result.Text);
    }

    // ============================================================
    // SubTask 28.3: S / V optimization
    // ============================================================

    [Fact]
    public void Scale_UniformKeyframes_EmitsSCommand()
    {
        var doc = BuildDocumentWithSprite(s =>
        {
            s.PropertyTracks.Add(new PropertyTrack
            {
                PropertyName = "Scale",
                ComponentMask = Vector2ComponentMask.Both,
                ValueType = "Vector2",
                Vector2Keyframes =
                {
                    new Keyframe<Vector2> { Time = 0, Value = new Vector2(1, 1), Easing = OsbEasing.None },
                    new Keyframe<Vector2> { Time = 1000, Value = new Vector2(2, 2), Easing = OsbEasing.None },
                },
            });
        });

        var result = new OsbExportCompiler().Compile(doc);
        AssertSucceeded(result);
        Assert.Contains(" S,0,0,1000,1,2", result.Text);
        Assert.DoesNotContain(" V,", result.Text);
    }

    [Fact]
    public void Scale_NonUniformKeyframes_EmitsVCommand()
    {
        var doc = BuildDocumentWithSprite(s =>
        {
            s.PropertyTracks.Add(new PropertyTrack
            {
                PropertyName = "Scale",
                ComponentMask = Vector2ComponentMask.Both,
                ValueType = "Vector2",
                Vector2Keyframes =
                {
                    new Keyframe<Vector2> { Time = 0, Value = new Vector2(1, 2), Easing = OsbEasing.None },
                    new Keyframe<Vector2> { Time = 1000, Value = new Vector2(3, 4), Easing = OsbEasing.None },
                },
            });
        });

        var result = new OsbExportCompiler().Compile(doc);
        AssertSucceeded(result);
        Assert.Contains(" V,0,0,1000,1,2,3,4", result.Text);
        Assert.DoesNotContain(" S,", result.Text);
    }

    [Fact]
    public void Scale_MixedUniformAndNonUniformKeyframes_EmitsVCommand()
    {
        var doc = BuildDocumentWithSprite(s =>
        {
            s.PropertyTracks.Add(new PropertyTrack
            {
                PropertyName = "Scale",
                ComponentMask = Vector2ComponentMask.Both,
                ValueType = "Vector2",
                Vector2Keyframes =
                {
                    new Keyframe<Vector2> { Time = 0, Value = new Vector2(1, 1), Easing = OsbEasing.None },
                    new Keyframe<Vector2> { Time = 1000, Value = new Vector2(2, 3), Easing = OsbEasing.None },
                },
            });
        });

        var result = new OsbExportCompiler().Compile(doc);
        AssertSucceeded(result);
        Assert.Contains(" V,0,0,1000,1,1,2,3", result.Text);
        Assert.DoesNotContain(" S,", result.Text);
    }

    // ============================================================
    // SubTask 28.4: RawBlock anchor reinsertion
    // ============================================================

    [Fact]
    public void RawBlock_LayerStart_PlacedBeforeSprites()
    {
        var doc = BuildDocumentWithSprite(s =>
        {
            s.PropertyTracks.Add(new PropertyTrack
            {
                PropertyName = "Opacity",
                ComponentMask = Vector2ComponentMask.None,
                ValueType = "Float",
                FloatKeyframes =
                {
                    new Keyframe<float> { Time = 0, Value = 0f, Easing = OsbEasing.None },
                    new Keyframe<float> { Time = 1000, Value = 1f, Easing = OsbEasing.None },
                },
            });
        });
        doc.RawBlocks.Add(new RawBlock
        {
            Id = "raw_start",
            Content = "// layer header comment",
            AnchorKind = RawBlockAnchorKind.LayerStart,
            AnchorCommandId = null,
            LayerId = "layer_bg",
        });

        var result = new OsbExportCompiler().Compile(doc);
        AssertSucceeded(result);
        Assert.Contains("// layer header comment", result.Text);

        var rawIdx = result.Text.IndexOf("// layer header comment");
        var spriteIdx = result.Text.IndexOf("Sprite,Background");
        Assert.True(rawIdx >= 0 && spriteIdx >= 0);
        Assert.True(rawIdx < spriteIdx, "LayerStart raw block should appear before the sprite declaration");
    }

    [Fact]
    public void RawBlock_AfterCommand_PlacedImmediatelyAfterLoopHeader()
    {
        var (doc, loop) = BuildDocumentWithLoop();
        doc.RawBlocks.Add(new RawBlock
        {
            Id = "raw_after_hdr",
            Content = "// after loop header",
            AnchorKind = RawBlockAnchorKind.AfterCommand,
            AnchorCommandId = loop.HeaderCommandId,
            LayerId = "layer_fg",
        });

        var result = new OsbExportCompiler().Compile(doc);
        AssertSucceeded(result);
        Assert.Contains("// after loop header", result.Text);

        var headerIdx = result.Text.IndexOf(" L,1000,2");
        var rawIdx = result.Text.IndexOf("// after loop header");
        var relIdx = result.Text.IndexOf("  F,0,0,500,0,1");
        Assert.True(headerIdx >= 0 && rawIdx >= 0 && relIdx >= 0);
        Assert.True(headerIdx < rawIdx, "Raw block should appear after the loop header line");
        Assert.True(rawIdx < relIdx, "Raw block should appear before the loop's internal command");
    }

    [Fact]
    public void RawBlock_AfterCommand_AnchorNotInOutput_RebasesToScopeEnd()
    {
        var doc = BuildDocumentWithSprite(s =>
        {
            s.PropertyTracks.Add(new PropertyTrack
            {
                PropertyName = "Opacity",
                ComponentMask = Vector2ComponentMask.None,
                ValueType = "Float",
                FloatKeyframes =
                {
                    new Keyframe<float> { Time = 0, Value = 0f, Easing = OsbEasing.None },
                    new Keyframe<float> { Time = 1000, Value = 1f, Easing = OsbEasing.None },
                },
            });
        });

        // Anchor command id exists in CommandRecords (so it passes pre-export validation G) but is
        // not referenced by any block/relative command, so it never becomes a tagged output line.
        const string orphanAnchorId = "cmd_unused_anchor";
        doc.CommandRecords[orphanAnchorId] = new CommandRecord
        {
            CommandId = orphanAnchorId,
            Lifecycle = CommandRecordLifecycle.Deleted,
            ParentLayerId = "layer_bg",
        };
        doc.RawBlocks.Add(new RawBlock
        {
            Id = "raw_rebase",
            Content = "// rebased to end",
            AnchorKind = RawBlockAnchorKind.AfterCommand,
            AnchorCommandId = orphanAnchorId,
            LayerId = "layer_bg",
        });

        var result = new OsbExportCompiler().Compile(doc);
        AssertSucceeded(result);
        // Layer exists → no orphan warning.
        Assert.DoesNotContain(result.Diagnostics, d => d.Code == FailureCodes.RAW_ANCHOR_ORPHANED);
        Assert.Contains("// rebased to end", result.Text);

        // Rebased to scope end: appears after the sprite's last command line.
        var cmdIdx = result.Text.IndexOf(" F,0,0,1000,0,1");
        var rawIdx = result.Text.IndexOf("// rebased to end");
        Assert.True(cmdIdx >= 0 && rawIdx >= 0);
        Assert.True(cmdIdx < rawIdx, "Rebased raw block should appear at the end of the layer content");
    }

    [Fact]
    public void RawBlock_LayerIdNotMatchingAnyLayer_ProducesOrphanWarning()
    {
        var doc = BuildDocumentWithSprite();
        doc.RawBlocks.Add(new RawBlock
        {
            Id = "raw_orphan",
            Content = "// orphan comment",
            AnchorKind = RawBlockAnchorKind.LayerStart,
            AnchorCommandId = null,
            LayerId = "nonexistent_layer",
        });

        var result = new OsbExportCompiler().Compile(doc);
        // Warning, not hard error → export still succeeds.
        Assert.True(result.Succeeded);
        Assert.Contains(result.Diagnostics, d => d.Code == FailureCodes.RAW_ANCHOR_ORPHANED);
        // Orphaned raw block is not placed in the output.
        Assert.DoesNotContain("// orphan comment", result.Text);
    }

    // ============================================================
    // Additional: pre-export validation & bezier export
    // ============================================================

    [Fact]
    public void Export_ValidDocument_SucceedsWithoutHardErrors()
    {
        var doc = BuildDocumentWithSprite();
        var result = new OsbExportCompiler().Compile(doc);
        Assert.True(result.Succeeded);
        Assert.DoesNotContain(result.Diagnostics, d => d.Severity == DiagnosticSeverity.HardError);
        Assert.Contains("Sprite,Background,Centre,\"bg.png\",320,240", result.Text);
    }

    [Fact]
    public void Bezier_WarnOnlyMode_ProducesWarningAndLinearOutput()
    {
        var doc = BuildDocumentWithSprite(s =>
        {
            s.PropertyTracks.Add(new PropertyTrack
            {
                PropertyName = "Position",
                ComponentMask = Vector2ComponentMask.Both,
                ValueType = "Vector2",
                Vector2Keyframes =
                {
                    new Keyframe<Vector2>
                    {
                        Time = 0,
                        Value = new Vector2(320, 240),
                        Easing = OsbEasing.None,
                        Handles = new BezierHandles
                        {
                            InHandle = new Vector2(0, 0),
                            OutHandle = new Vector2(10, 10),
                        },
                    },
                    new Keyframe<Vector2>
                    {
                        Time = 1000,
                        Value = new Vector2(400, 240),
                        Easing = OsbEasing.None,
                    },
                },
            });
        });

        // WarnOnly is the default mode.
        var result = new OsbExportCompiler().Compile(doc);
        AssertSucceeded(result);
        Assert.Contains(result.Diagnostics, d => d.Code == "BEZIER_CURVE_NOT_EXPRESSIBLE");
        // Exported as linear: a single M segment with no handle representation.
        Assert.Contains(" M,0,0,1000,320,240,400,240", result.Text);
    }

    [Fact]
    public void Bezier_BakeToSegmentsMode_ProducesMultipleKeyframeSegments()
    {
        var doc = BuildDocumentWithSprite(s =>
        {
            s.PropertyTracks.Add(new PropertyTrack
            {
                PropertyName = "Position",
                ComponentMask = Vector2ComponentMask.Both,
                ValueType = "Vector2",
                Vector2Keyframes =
                {
                    new Keyframe<Vector2>
                    {
                        Time = 0,
                        Value = new Vector2(320, 240),
                        Easing = OsbEasing.None,
                        Handles = new BezierHandles
                        {
                            InHandle = new Vector2(0, 0),
                            OutHandle = new Vector2(10, 10),
                        },
                    },
                    new Keyframe<Vector2>
                    {
                        Time = 1000,
                        Value = new Vector2(400, 240),
                        Easing = OsbEasing.None,
                    },
                },
            });
        });

        var result = new OsbExportCompiler().Compile(doc, new ExportOptions
        {
            BezierExportMode = BezierExportMode.BakeToSegments,
            BezierBakeSegments = 16,
        });
        AssertSucceeded(result);

        // 2 original keyframes with a bezier segment → baked into 17 keyframes → 16 M lines.
        var mLineCount = result.Text.Split('\n').Count(l => l.StartsWith(" M,"));
        Assert.True(mLineCount > 1, $"Expected multiple baked M lines, got {mLineCount}");
        Assert.Equal(16, mLineCount);
    }
}
