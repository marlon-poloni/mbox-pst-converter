using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MboxPst.Sidecar.Protocol;

public static class Wire
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static Command? ParseCommand(string line)
    {
        try
        {
            JsonNode? node = JsonNode.Parse(line);
            Command? command = node?["cmd"]?.GetValue<string>() switch
            {
                "scan" => node.Deserialize<ScanCommand>(Options),
                "preview" => node.Deserialize<PreviewCommand>(Options),
                "convert" => node.Deserialize<ConvertCommand>(Options),
                "cancel" => new CancelCommand(),
                _ => null,
            };
            return IsComplete(command) ? command : null;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
        {
            return null;
        }
    }

    public const long MaxPartMBLimit = 50 * 1024;

    private static bool IsComplete(Command? command) => command switch
    {
        ScanCommand s => IsAbsolute(s.Path),
        PreviewCommand p => IsValid(p.Mapping),
        ConvertCommand c => IsValid(c.Mapping) && IsAbsolute(c.OutputDirectory) && !string.IsNullOrWhiteSpace(c.OutputName)
            && c.MaxPartMB is >= 1 and <= MaxPartMBLimit,
        CancelCommand => true,
        _ => false,
    };

    private static bool IsAbsolute(string? path) => !string.IsNullOrWhiteSpace(path) && Path.IsPathRooted(path);

    private static readonly HashSet<string> Kinds = new() { "folder", "mark", "ignoreLabel", "exclude" };

    private static bool IsValid(MappingDto? mapping) =>
        mapping is { Decisions: not null } && !string.IsNullOrWhiteSpace(mapping.StoreName)
        && mapping.CopyPolicy is "eachFolder" or "firstFolderOnly"
        && mapping.Decisions.All(pair => !string.IsNullOrEmpty(pair.Key) && IsValid(pair.Value));

    private static bool IsValid(DecisionDto? decision) =>
        decision is { Kind: not null, FolderPath: not null } && Kinds.Contains(decision.Kind)
        && (decision.Kind != "folder" || (decision.FolderPath.Length > 0
            && decision.FolderPath.All(segment => !string.IsNullOrWhiteSpace(segment) && segment.Length <= 255)));

    public static string Serialize(Event e)
    {
        JsonObject json = JsonSerializer.SerializeToNode(e, e.GetType(), Options)!.AsObject();
        json.Remove("eventName");
        var ordered = new JsonObject { ["event"] = e.EventName };
        foreach (var (key, value) in json.ToList())
        {
            json.Remove(key);
            ordered[key] = value;
        }
        return ordered.ToJsonString(Options);
    }
}
