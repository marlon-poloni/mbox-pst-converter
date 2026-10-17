using System.Globalization;
using MboxPst.Core.Mapping;
using MboxPst.Core.Scanning;

namespace MboxPst.Core.Previewing;

public sealed record FolderNode(string Name, IReadOnlyList<string> Path, int Messages, IReadOnlyList<FolderNode> Children);

public sealed record Preview(
    FolderNode Root,
    int SourceMessages,
    int MessagesInPst,
    int ExtraCopies,
    int MessagesInMoreThanOneFolder,
    int Excluded,
    long EstimatedBytes,
    int FolderCount);

public static class PreviewBuilder
{
    public const double PstSizeFactor = 0.75;

    private static readonly StringComparer NameComparer = StringComparer.Create(new CultureInfo("pt-BR"), CompareOptions.IgnoreCase);

    public static Preview Build(LabelInventory inventory, MappingConfig config, string fallbackFolder)
    {
        var root = new MutableNode("", Array.Empty<string>());
        int inPst = 0, multi = 0, excluded = 0;
        long copiedBytes = 0;

        foreach (InventoryEntry entry in inventory.Entries)
        {
            Route route = LabelRouter.For(entry.Labels, config, fallbackFolder);
            if (route.Excluded)
            {
                excluded++;
                continue;
            }
            if (route.Folders.Count > 1)
                multi++;
            foreach (IReadOnlyList<string> path in route.Folders)
            {
                root.Descend(path).Messages++;
                inPst++;
                copiedBytes += entry.SizeBytes;
            }
        }

        FolderNode tree = root.Freeze(config.StoreName, isRoot: true);
        return new Preview(
            tree,
            inventory.Entries.Count,
            inPst,
            inPst - (inventory.Entries.Count - excluded),
            multi,
            excluded,
            (long)Math.Round(copiedBytes * PstSizeFactor),
            CountNodes(tree) - 1);
    }

    private static int CountNodes(FolderNode node) => 1 + node.Children.Sum(CountNodes);

    private sealed class MutableNode(string name, IReadOnlyList<string> path)
    {
        private readonly Dictionary<string, MutableNode> _children = new(StringComparer.OrdinalIgnoreCase);
        public string Name { get; } = name;
        public IReadOnlyList<string> Path { get; } = path;
        public int Messages;

        public MutableNode Descend(IReadOnlyList<string> path)
        {
            MutableNode node = this;
            for (int i = 0; i < path.Count; i++)
            {
                if (!node._children.TryGetValue(path[i], out MutableNode? child))
                {
                    string name = node.Path.Count == 0 ? StandardSpelling(path[i]) : path[i];
                    child = new MutableNode(name, node.Path.Append(name).ToArray());
                    node._children[path[i]] = child;
                }
                node = child;
            }
            return node;
        }

        public FolderNode Freeze(string displayName, bool isRoot = false)
        {
            IEnumerable<MutableNode> ordered = _children.Values
                .OrderBy(c => isRoot ? StandardIndex(c.Name) : int.MaxValue)
                .ThenBy(c => c.Name, NameComparer);
            return new FolderNode(displayName, Path, Messages, ordered.Select(c => c.Freeze(c.Name)).ToArray());
        }

        private static string StandardSpelling(string folder) =>
            FolderNames.StandardOrder.FirstOrDefault(s => string.Equals(s, folder, StringComparison.OrdinalIgnoreCase)) ?? folder;

        private static int StandardIndex(string folder)
        {
            for (int i = 0; i < FolderNames.StandardOrder.Count; i++)
                if (string.Equals(FolderNames.StandardOrder[i], folder, StringComparison.OrdinalIgnoreCase))
                    return i;
            return int.MaxValue;
        }
    }
}
