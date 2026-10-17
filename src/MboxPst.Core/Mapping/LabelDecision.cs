namespace MboxPst.Core.Mapping;

public enum DecisionKind
{
    Folder,
    Mark,
    IgnoreLabel,
    Exclude,
}

public sealed record LabelDecision(DecisionKind Kind, IReadOnlyList<string> FolderPath)
{
    public static LabelDecision Folder(params string[] path) => new(DecisionKind.Folder, path);
    public static LabelDecision Mark() => new(DecisionKind.Mark, Array.Empty<string>());
    public static LabelDecision Ignore() => new(DecisionKind.IgnoreLabel, Array.Empty<string>());
    public static LabelDecision Exclude() => new(DecisionKind.Exclude, Array.Empty<string>());
}
