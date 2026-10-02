using System.Numerics;
using EsoData.Catalogs;

namespace EsoData.Addons;

public sealed record CollectedSetPiece(long PieceId, long SlotMask, string? Name, bool? Collected);
public sealed record SetCollectionProgress(long SetId, long? SlotMask, int? CollectedCount,
    int? TotalCount, bool? Complete, int? ReconstructionCrystals, long? UnknownSlotMask,
    IReadOnlyList<CollectedSetPiece> Pieces)
{
    /// <summary>Describes an observed collection using a complete game-API piece list for that set.
    /// Missing definitions or mask bits outside the definitions leave completion and cost unknown.</summary>
    public static SetCollectionProgress Describe(long setId, long? mask, GameCatalog? catalog)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(setId);
        if (mask < 0) throw new ArgumentOutOfRangeException(nameof(mask));
        var definitions = (catalog?.CollectionPieces ?? []).Where(p => p.SetId == setId)
            .DistinctBy(p => p.SlotMask).OrderBy(p => p.SlotMask).ToArray();
        var pieces = definitions.Select(p => new CollectedSetPiece(p.PieceId, p.SlotMask,
            p.Name ?? catalog?.Items.GetValueOrDefault(p.PieceId)?.Name,
            mask is long m ? (m & p.SlotMask) == p.SlotMask : null)).ToArray();
        int? collected = mask is long value ? BitOperations.PopCount((ulong)value) : null;
        int? total = pieces.Length > 0 ? pieces.Length : null;
        var definedMask = definitions.Aggregate(0L, (current, p) => current | p.SlotMask);
        long? unknown = mask is long observed ? observed & ~definedMask : null;
        var resolved = total.HasValue && collected.HasValue && unknown == 0;
        bool? complete = resolved ? collected == total : null;
        int? cost = resolved && collected > 0
            ? total == 1 ? 25 : (int)Math.Floor(75 - 50.0 * (collected!.Value - 1) / (total!.Value - 1))
            : null;
        return new(setId, mask, collected, total, complete, cost, unknown, pieces);
    }
}
