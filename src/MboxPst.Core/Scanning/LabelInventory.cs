namespace MboxPst.Core.Scanning;

public sealed record InventoryEntry(long Offset, IReadOnlyList<string> Labels, long SizeBytes);

public sealed class LabelInventory
{
    public IReadOnlyList<InventoryEntry> Entries { get; init; } = Array.Empty<InventoryEntry>();
    public IReadOnlyDictionary<string, int> CountByLabel { get; init; } = new Dictionary<string, int>();
    public long SourceBytes { get; init; }
}
