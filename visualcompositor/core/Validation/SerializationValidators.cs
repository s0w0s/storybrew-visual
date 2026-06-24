using VisualCompositor.Core.Model;
using VisualCompositor.Core.Serialization;

namespace VisualCompositor.Core.Validation;

/// <summary>Serialization-scope validators.
/// Checks that a document can survive a serialize→deserialize round-trip
/// without structural loss. Runs at pre-storybrewcomp-save, deserialize-load,
/// and debug-integrity-scan entry points.</summary>
public static class SerializationValidators
{
    /// <summary>Validates that serializing and deserializing the document
    /// produces a structurally equivalent document (schema version, layer count,
    /// sprite count, block count, command record count, raw block count).</summary>
    public static void ValidateRoundTripFidelity(CompositionDocument document, DiagnosticCollection diagnostics)
    {
        // Serialize
        string json;
        try
        {
            json = StorybrewCompSerializer.Serialize(document);
        }
        catch (Exception ex)
        {
            diagnostics.Add(new Diagnostic
            {
                Code = FailureCodes.SERIALIZATION_SERIALIZE_FAILED,
                Message = $"Serialization failed: {ex.Message}",
                Severity = DiagnosticSeverity.HardError,
                Scope = "serialization",
            });
            return;
        }

        // Deserialize
        DeserializeResult result;
        try
        {
            result = StorybrewCompSerializer.Deserialize(json);
        }
        catch (Exception ex)
        {
            diagnostics.Add(new Diagnostic
            {
                Code = FailureCodes.SERIALIZATION_DESERIALIZE_FAILED,
                Message = $"Deserialization failed: {ex.Message}",
                Severity = DiagnosticSeverity.HardError,
                Scope = "serialization",
            });
            return;
        }

        var roundTripped = result.Document;

        // Schema version must be current
        if (roundTripped.SchemaVersion != CompositionDocument.CurrentSchemaVersion)
        {
            diagnostics.Add(new Diagnostic
            {
                Code = FailureCodes.SERIALIZATION_SCHEMA_VERSION_MISMATCH,
                Message = $"Schema version mismatch after round-trip: expected {CompositionDocument.CurrentSchemaVersion}, got {roundTripped.SchemaVersion}",
                Severity = DiagnosticSeverity.HardError,
                Scope = "serialization",
            });
        }

        // Layer count must match
        if (roundTripped.Layers.Count != document.Layers.Count)
        {
            diagnostics.Add(new Diagnostic
            {
                Code = FailureCodes.SERIALIZATION_LAYER_COUNT_MISMATCH,
                Message = $"Layer count mismatch after round-trip: expected {document.Layers.Count}, got {roundTripped.Layers.Count}",
                Severity = DiagnosticSeverity.HardError,
                Scope = "serialization",
            });
        }

        // Command record count must match
        if (roundTripped.CommandRecords.Count != document.CommandRecords.Count)
        {
            diagnostics.Add(new Diagnostic
            {
                Code = FailureCodes.SERIALIZATION_COMMAND_RECORD_COUNT_MISMATCH,
                Message = $"CommandRecord count mismatch after round-trip: expected {document.CommandRecords.Count}, got {roundTripped.CommandRecords.Count}",
                Severity = DiagnosticSeverity.HardError,
                Scope = "serialization",
            });
        }

        // RawBlock count must match
        if (roundTripped.RawBlocks.Count != document.RawBlocks.Count)
        {
            diagnostics.Add(new Diagnostic
            {
                Code = FailureCodes.SERIALIZATION_RAW_BLOCK_COUNT_MISMATCH,
                Message = $"RawBlock count mismatch after round-trip: expected {document.RawBlocks.Count}, got {roundTripped.RawBlocks.Count}",
                Severity = DiagnosticSeverity.HardError,
                Scope = "serialization",
            });
        }

        // ExpandedBlockHistory count must match
        if (roundTripped.ExpandedBlockHistories.Count != document.ExpandedBlockHistories.Count)
        {
            diagnostics.Add(new Diagnostic
            {
                Code = FailureCodes.SERIALIZATION_EXPANDED_HISTORY_COUNT_MISMATCH,
                Message = $"ExpandedBlockHistory count mismatch after round-trip: expected {document.ExpandedBlockHistories.Count}, got {roundTripped.ExpandedBlockHistories.Count}",
                Severity = DiagnosticSeverity.HardError,
                Scope = "serialization",
            });
        }

        // Per-layer sprite and block count must match
        for (int i = 0; i < Math.Min(document.Layers.Count, roundTripped.Layers.Count); i++)
        {
            var origLayer = document.Layers[i];
            var rtLayer = roundTripped.Layers[i];

            if (rtLayer.Sprites.Count != origLayer.Sprites.Count)
            {
                diagnostics.Add(new Diagnostic
                {
                    Code = FailureCodes.SERIALIZATION_SPRITE_COUNT_MISMATCH,
                    Message = $"Sprite count mismatch in layer '{origLayer.Id}' after round-trip: expected {origLayer.Sprites.Count}, got {rtLayer.Sprites.Count}",
                    Severity = DiagnosticSeverity.HardError,
                    Scope = "serialization",
                });
            }

            if (rtLayer.Blocks.Count != origLayer.Blocks.Count)
            {
                diagnostics.Add(new Diagnostic
                {
                    Code = FailureCodes.SERIALIZATION_BLOCK_COUNT_MISMATCH,
                    Message = $"Block count mismatch in layer '{origLayer.Id}' after round-trip: expected {origLayer.Blocks.Count}, got {rtLayer.Blocks.Count}",
                    Severity = DiagnosticSeverity.HardError,
                    Scope = "serialization",
                });
            }

            // Per-sprite block count
            for (int j = 0; j < Math.Min(origLayer.Sprites.Count, rtLayer.Sprites.Count); j++)
            {
                var origSprite = origLayer.Sprites[j];
                var rtSprite = rtLayer.Sprites[j];

                if (rtSprite.Blocks.Count != origSprite.Blocks.Count)
                {
                    diagnostics.Add(new Diagnostic
                    {
                        Code = FailureCodes.SERIALIZATION_BLOCK_COUNT_MISMATCH,
                        Message = $"Block count mismatch in sprite '{origSprite.Id}' after round-trip: expected {origSprite.Blocks.Count}, got {rtSprite.Blocks.Count}",
                        Severity = DiagnosticSeverity.HardError,
                        Scope = "serialization",
                    });
                }

                if (rtSprite.PropertyTracks.Count != origSprite.PropertyTracks.Count)
                {
                    diagnostics.Add(new Diagnostic
                    {
                        Code = FailureCodes.SERIALIZATION_PROPERTY_TRACK_COUNT_MISMATCH,
                        Message = $"PropertyTrack count mismatch in sprite '{origSprite.Id}' after round-trip: expected {origSprite.PropertyTracks.Count}, got {rtSprite.PropertyTracks.Count}",
                        Severity = DiagnosticSeverity.HardError,
                        Scope = "serialization",
                    });
                }
            }
        }
    }
}
