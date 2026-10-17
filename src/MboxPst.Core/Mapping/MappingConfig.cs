namespace MboxPst.Core.Mapping;

public enum CopyPolicy
{
    EachFolder,
    FirstFolderOnly,
}

public sealed class MappingConfig
{
    public IReadOnlyDictionary<string, LabelDecision> Decisions { get; init; } = new Dictionary<string, LabelDecision>();
    public CopyPolicy CopyPolicy { get; init; } = CopyPolicy.EachFolder;
    public IReadOnlyList<string> UserPriority { get; init; } = Array.Empty<string>();
    public string StoreName { get; init; } = "";
}
