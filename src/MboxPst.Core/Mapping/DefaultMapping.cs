using MboxPst.Core.Gmail;

namespace MboxPst.Core.Mapping;

public static class DefaultMapping
{
    public static MappingConfig For(IReadOnlyDictionary<string, int> countByLabel, string storeName)
    {
        var decisions = countByLabel.Keys.ToDictionary(label => label, DecisionFor, StringComparer.Ordinal);
        string[] priority = countByLabel
            .Where(pair => LabelCatalog.Classify(pair.Key) == LabelKind.User)
            .OrderByDescending(pair => pair.Value)
            .ThenBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => pair.Key)
            .ToArray();

        return new MappingConfig { Decisions = decisions, UserPriority = priority, StoreName = storeName };
    }

    public static LabelDecision DecisionFor(string label)
    {
        LabelKind kind = LabelCatalog.Classify(label);
        return kind switch
        {
            LabelKind.Inbox or LabelKind.Sent or LabelKind.Drafts or LabelKind.Archived or LabelKind.Spam or LabelKind.Trash
                => LabelDecision.Folder(FolderNames.For(kind)),
            LabelKind.Unread or LabelKind.Starred or LabelKind.Forwarded => LabelDecision.Mark(),
            LabelKind.Category or LabelKind.Opened or LabelKind.Important => LabelDecision.Ignore(),
            _ => LabelDecision.Folder(label.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)),
        };
    }
}
