using System.Security.Cryptography;
using System.Text;
using VisualCompositor.Core.Primitives;

namespace VisualCompositor.Core.Model;

/// <summary>Generates opaque persistent ids for commands and blocks.
/// Hash is ONLY used for initial import allocation. After save, ids are never recomputed.
/// Ids are opaque — never use hash for content consistency validation.</summary>
public static class PersistentIdGenerator
{
    /// <summary>Generate a deterministic id for an imported command based on its source content.
    /// This is ONLY called during initial .osb import. Never recomputed after save.</summary>
    public static string GenerateForCommand(
        string layerId,
        string? spriteId,
        string? blockId,
        string commandType,
        double startTime,
        double endTime,
        string startValue,
        string endValue,
        int sequenceNumber)
    {
        var input = $"{layerId}|{spriteId ?? ""}|{blockId ?? ""}|{commandType}|{startTime:F3}|{endTime:F3}|{startValue}|{endValue}|{sequenceNumber}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return "cmd_" + Convert.ToHexString(hash[..12]).ToLowerInvariant();
    }

    /// <summary>Generate a deterministic id for an imported block (Loop/Trigger).</summary>
    public static string GenerateForBlock(
        string layerId,
        string? spriteId,
        string blockType,
        double startTime,
        double endTimeOrCount,
        string? triggerName,
        int sequenceNumber)
    {
        var input = $"{layerId}|{spriteId ?? ""}|{blockType}|{startTime:F3}|{endTimeOrCount:F3}|{triggerName ?? ""}|{sequenceNumber}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return "blk_" + Convert.ToHexString(hash[..12]).ToLowerInvariant();
    }

    /// <summary>Generate a deterministic id for a sprite declaration.</summary>
    public static string GenerateForSprite(
        string layerId,
        OsbLayer osbLayer,
        string texturePath,
        float posX,
        float posY,
        int sequenceNumber)
    {
        var input = $"{layerId}|{osbLayer}|{texturePath}|{posX:F3}|{posY:F3}|{sequenceNumber}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return "spr_" + Convert.ToHexString(hash[..12]).ToLowerInvariant();
    }

    /// <summary>Generate a deterministic id for a raw block.</summary>
    public static string GenerateForRawBlock(
        string layerId,
        string content,
        int sequenceNumber)
    {
        var input = $"{layerId}|{content}|{sequenceNumber}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return "raw_" + Convert.ToHexString(hash[..12]).ToLowerInvariant();
    }

    /// <summary>Generate a fresh unique id for a newly created command (not from import).
    /// Uses a GUID-based approach since there's no source content to hash.</summary>
    public static string GenerateNewCommandId()
    {
        return "cmd_new_" + Guid.NewGuid().ToString("N")[..16];
    }

    /// <summary>Generate a fresh unique id for a newly created block.</summary>
    public static string GenerateNewBlockId()
    {
        return "blk_new_" + Guid.NewGuid().ToString("N")[..16];
    }

    /// <summary>Generate a transaction id for ExpandBlockTransaction.</summary>
    public static string GenerateTransactionId()
    {
        return "txn_" + Guid.NewGuid().ToString("N")[..16];
    }

    /// <summary>Generate a layer id.</summary>
    public static string GenerateLayerId(OsbLayer osbLayer, int sequenceNumber)
    {
        return $"layer_{osbLayer}_{sequenceNumber}";
    }

    /// <summary>Generate a marker id.</summary>
    public static string GenerateMarkerId()
    {
        return "mkr_" + Guid.NewGuid().ToString("N")[..16];
    }

    /// <summary>Validate that all ids in a document are unique. Does NOT recompute hashes.</summary>
    public static bool ValidateIdUniqueness(CompositionDocument document, out List<string> duplicates)
    {
        var allIds = new Dictionary<string, int>();
        var dupList = new List<string>();

        void CheckId(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (!allIds.TryAdd(id, 1))
            {
                if (allIds[id] == 1)
                    dupList.Add(id);
                allIds[id]++;
            }
        }

        foreach (var layer in document.Layers)
        {
            CheckId(layer.Id);
            foreach (var sprite in layer.Sprites)
            {
                CheckId(sprite.Id);
                foreach (var block in sprite.Blocks) CheckId(block.Id);
            }
            foreach (var block in layer.Blocks) CheckId(block.Id);
        }
        foreach (var rawBlock in document.RawBlocks) CheckId(rawBlock.Id);
        foreach (var kvp in document.CommandRecords) CheckId(kvp.Key);

        duplicates = dupList;
        return duplicates.Count == 0;
    }
}
