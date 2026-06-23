using System.Text.Json;
using System.Text.Json.Serialization;
using VisualCompositor.Core.Model;
using VisualCompositor.Core.Primitives;
using VisualCompositor.Core.Snapshots;

namespace VisualCompositor.Core.Serialization;

/// <summary>Serializes/deserializes CompositionDocument to/from .storybrewcomp JSON (schema v2).</summary>
public static class StorybrewCompSerializer
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
        options.Converters.Add(new StoryboardBlockConverter());
        options.Converters.Add(new Vector2Converter());
        options.Converters.Add(new Color3Converter());
        return options;
    }

    /// <summary>Serialize a CompositionDocument to JSON string.</summary>
    public static string Serialize(CompositionDocument document)
    {
        document.SchemaVersion = CompositionDocument.CurrentSchemaVersion;
        return JsonSerializer.Serialize(document, Options);
    }

    /// <summary>Deserialize a JSON string to a CompositionDocument.
    /// Handles v1 migration and missing fields.</summary>
    public static DeserializeResult Deserialize(string json)
    {
        var diagnostics = new List<string>();
        bool isDirty = false;

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        int schemaVersion = 0;
        if (root.TryGetProperty("schemaVersion", out var svEl) && svEl.ValueKind == JsonValueKind.Number)
        {
            schemaVersion = svEl.GetInt32();
        }

        // v1 or missing schemaVersion: migrate to v2
        if (schemaVersion < 2)
        {
            diagnostics.Add($"Migrating from schemaVersion {schemaVersion} to {CompositionDocument.CurrentSchemaVersion}");
            isDirty = true;
        }

        // Re-serialize with schemaVersion=2 and deserialize
        var document = JsonSerializer.Deserialize<CompositionDocument>(json, Options) ?? new CompositionDocument();
        document.SchemaVersion = CompositionDocument.CurrentSchemaVersion;

        // Check for missing commandRecords
        if (document.CommandRecords == null || document.CommandRecords.Count == 0)
        {
            var hasCommands = document.Layers.Any(l =>
                l.Sprites.Any(s => s.Blocks.Any()) || l.Blocks.Any());
            if (hasCommands)
            {
                diagnostics.Add("v2 file missing commandRecords — backfilling");
                isDirty = true;
            }
            document.CommandRecords ??= new Dictionary<string, CommandRecord>();
        }

        // Check for missing expandedBlockHistories
        if (document.ExpandedBlockHistories == null)
        {
            diagnostics.Add("v2 file missing expandedBlockHistories — backfilling");
            isDirty = true;
            document.ExpandedBlockHistories = new List<ExpandedBlockHistory>();
        }

        document.IsDirty = isDirty;

        return new DeserializeResult
        {
            Document = document,
            Diagnostics = diagnostics,
            WasMigrated = schemaVersion < 2,
            IsDirty = isDirty,
        };
    }

    /// <summary>Serialize to a stream (for file writing).</summary>
    public static void Serialize(CompositionDocument document, Stream stream)
    {
        document.SchemaVersion = CompositionDocument.CurrentSchemaVersion;
        JsonSerializer.Serialize(stream, document, Options);
    }

    /// <summary>Deserialize from a stream (for file reading).</summary>
    public static DeserializeResult Deserialize(Stream stream)
    {
        using var reader = new StreamReader(stream);
        return Deserialize(reader.ReadToEnd());
    }
}

public sealed class DeserializeResult
{
    public CompositionDocument Document { get; init; } = new();
    public List<string> Diagnostics { get; init; } = new();
    public bool WasMigrated { get; init; }
    public bool IsDirty { get; init; }
}

/// <summary>JSON converter for StoryboardBlock (abstract) — serializes as LoopBlock or TriggerBlock based on BlockType.</summary>
public sealed class StoryboardBlockConverter : JsonConverter<StoryboardBlock>
{
    public override StoryboardBlock? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var blockType = doc.RootElement.TryGetProperty("blockType", out var btEl) ? btEl.GetString() : null;
        return blockType switch
        {
            "Loop" => JsonSerializer.Deserialize<LoopBlock>(doc.RootElement.GetRawText(), options),
            "Trigger" => JsonSerializer.Deserialize<TriggerBlock>(doc.RootElement.GetRawText(), options),
            _ => throw new JsonException($"Unknown block type: {blockType}"),
        };
    }

    public override void Write(Utf8JsonWriter writer, StoryboardBlock value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value, value.GetType(), options);
    }
}

public sealed class Vector2Converter : JsonConverter<Vector2>
{
    public override Vector2 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var x = doc.RootElement.TryGetProperty("x", out var xEl) ? xEl.GetSingle() : 0;
        var y = doc.RootElement.TryGetProperty("y", out var yEl) ? yEl.GetSingle() : 0;
        return new Vector2(x, y);
    }

    public override void Write(Utf8JsonWriter writer, Vector2 value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber("x", value.X);
        writer.WriteNumber("y", value.Y);
        writer.WriteEndObject();
    }
}

public sealed class Color3Converter : JsonConverter<Color3>
{
    public override Color3 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var r = doc.RootElement.TryGetProperty("r", out var rEl) ? rEl.GetSingle() : 0;
        var g = doc.RootElement.TryGetProperty("g", out var gEl) ? gEl.GetSingle() : 0;
        var b = doc.RootElement.TryGetProperty("b", out var bEl) ? bEl.GetSingle() : 0;
        return new Color3(r, g, b);
    }

    public override void Write(Utf8JsonWriter writer, Color3 value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber("r", value.R);
        writer.WriteNumber("g", value.G);
        writer.WriteNumber("b", value.B);
        writer.WriteEndObject();
    }
}
