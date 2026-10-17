using MboxPst.Core.Gmail;

namespace MboxPst.Core.Mapping;

public sealed record Route(
    bool Excluded,
    IReadOnlyList<IReadOnlyList<string>> Folders,
    bool IsRead,
    bool IsFlagged,
    bool IsForwarded,
    IReadOnlyList<string> Categories);

public static class LabelRouter
{
    private static readonly LabelKind[] StandardKinds =
        { LabelKind.Inbox, LabelKind.Sent, LabelKind.Drafts, LabelKind.Archived, LabelKind.Spam, LabelKind.Trash };

    public static Route For(IReadOnlyList<string> labels, MappingConfig config, string fallbackFolder)
    {
        var folders = new List<(IReadOnlyList<string> Path, (int, int, string) Rank)>();
        var categories = new List<string>();
        bool excluded = false, unread = false, flagged = false, forwarded = false;

        foreach (string label in labels)
        {
            LabelKind kind = LabelCatalog.Classify(label);
            LabelDecision decision = config.Decisions.TryGetValue(label, out LabelDecision? chosen)
                ? chosen
                : DefaultMapping.DecisionFor(label);

            switch (decision.Kind)
            {
                case DecisionKind.Exclude:
                    excluded = true;
                    break;
                case DecisionKind.Folder when decision.FolderPath.All(string.IsNullOrWhiteSpace):
                    break;
                case DecisionKind.Folder:
                    if (!folders.Any(f => SamePath(f.Path, decision.FolderPath)))
                        folders.Add((decision.FolderPath, Rank(label, kind, config)));
                    break;
                case DecisionKind.Mark when kind == LabelKind.Unread:
                    unread = true;
                    break;
                case DecisionKind.Mark when kind == LabelKind.Starred:
                    flagged = true;
                    break;
                case DecisionKind.Mark when kind == LabelKind.Forwarded:
                    forwarded = true;
                    break;
                case DecisionKind.Mark:
                    if (!categories.Contains(label, StringComparer.OrdinalIgnoreCase))
                        categories.Add(label);
                    break;
            }
        }

        if (excluded)
            return new Route(true, Array.Empty<IReadOnlyList<string>>(), !unread, flagged, forwarded, categories);

        IReadOnlyList<IReadOnlyList<string>> targets;
        if (folders.Count == 0)
            targets = new[] { labels.Count == 0 ? new[] { fallbackFolder } : new[] { FolderNames.Archive } };
        else if (config.CopyPolicy == CopyPolicy.FirstFolderOnly)
            targets = new[] { folders.MinBy(f => f.Rank, RankComparer)!.Path };
        else
            targets = folders.Select(f => f.Path).ToArray();

        return new Route(false, targets, !unread, flagged, forwarded, categories);
    }

    private static readonly Comparer<(int Tier, int Index, string Name)> RankComparer = Comparer<(int Tier, int Index, string Name)>.Create((a, b) =>
    {
        int byTier = a.Tier.CompareTo(b.Tier);
        if (byTier != 0) return byTier;
        int byIndex = a.Index.CompareTo(b.Index);
        return byIndex != 0 ? byIndex : string.CompareOrdinal(a.Name, b.Name);
    });

    private static (int, int, string) Rank(string label, LabelKind kind, MappingConfig config)
    {
        if (kind == LabelKind.User)
        {
            int index = IndexOf(config.UserPriority, label);
            return (0, index < 0 ? int.MaxValue : index, label);
        }
        int standard = Array.IndexOf(StandardKinds, kind);
        return standard >= 0 ? (1, standard, label) : (2, 0, label);
    }

    private static int IndexOf(IReadOnlyList<string> list, string value)
    {
        for (int i = 0; i < list.Count; i++)
            if (string.Equals(list[i], value, StringComparison.Ordinal))
                return i;
        return -1;
    }

    private static bool SamePath(IReadOnlyList<string> a, IReadOnlyList<string> b) =>
        a.Count == b.Count && a.Zip(b).All(p => string.Equals(p.First, p.Second, StringComparison.OrdinalIgnoreCase));
}
