using MboxPst.Core.Mapping;

namespace MboxPst.Sidecar.Protocol;

public abstract record Command;

public sealed record ScanCommand(string Path) : Command;

public sealed record PreviewCommand(MappingDto Mapping) : Command;

public sealed record ConvertCommand(string OutputDirectory, string OutputName, MappingDto Mapping, long MaxPartMB) : Command;

public sealed record CancelCommand : Command;

public sealed record DecisionDto(string Kind, string[] FolderPath);

public sealed record MappingDto(string StoreName, string CopyPolicy, Dictionary<string, DecisionDto> Decisions, string[]? UserPriority = null)
{
    public MappingConfig ToConfig() => new()
    {
        StoreName = StoreName,
        CopyPolicy = CopyPolicy == "firstFolderOnly" ? Core.Mapping.CopyPolicy.FirstFolderOnly : Core.Mapping.CopyPolicy.EachFolder,
        UserPriority = UserPriority ?? Array.Empty<string>(),
        Decisions = Decisions.ToDictionary(
            pair => pair.Key,
            pair => new LabelDecision(ParseKind(pair.Value.Kind), pair.Value.FolderPath ?? Array.Empty<string>())),
    };

    public static MappingDto From(MappingConfig config) => new(
        config.StoreName,
        config.CopyPolicy == Core.Mapping.CopyPolicy.FirstFolderOnly ? "firstFolderOnly" : "eachFolder",
        config.Decisions.ToDictionary(pair => pair.Key, pair => new DecisionDto(KindName(pair.Value.Kind), pair.Value.FolderPath.ToArray())),
        config.UserPriority.ToArray());

    private static DecisionKind ParseKind(string kind) => kind switch
    {
        "folder" => DecisionKind.Folder,
        "mark" => DecisionKind.Mark,
        "ignoreLabel" => DecisionKind.IgnoreLabel,
        "exclude" => DecisionKind.Exclude,
        _ => throw new FormatException($"Decisão desconhecida: {kind}"),
    };

    private static string KindName(DecisionKind kind) => kind switch
    {
        DecisionKind.Folder => "folder",
        DecisionKind.Mark => "mark",
        DecisionKind.IgnoreLabel => "ignoreLabel",
        _ => "exclude",
    };
}
