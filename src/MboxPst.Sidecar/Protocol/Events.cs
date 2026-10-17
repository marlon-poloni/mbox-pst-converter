using System.Text.Json.Serialization;
using MboxPst.Core.Conversion;
using MboxPst.Core.Previewing;

namespace MboxPst.Sidecar.Protocol;

public abstract record Event
{
    [JsonPropertyOrder(-1)]
    public abstract string EventName { get; }
}

public sealed record LabelDto(string Name, string Kind, int Count);

public sealed record FolderDto(string Name, string[] Path, int Messages, FolderDto[] Children)
{
    public static FolderDto From(FolderNode node) =>
        new(node.Name, node.Path.ToArray(), node.Messages, node.Children.Select(From).ToArray());
}

public sealed record PreviewDto(FolderDto Root, int SourceMessages, int MessagesInPst, int ExtraCopies, int MessagesInMoreThanOneFolder, int Excluded, long EstimatedBytes, int FolderCount)
{
    public static PreviewDto From(Preview p) =>
        new(FolderDto.From(p.Root), p.SourceMessages, p.MessagesInPst, p.ExtraCopies, p.MessagesInMoreThanOneFolder, p.Excluded, p.EstimatedBytes, p.FolderCount);
}

public sealed record FolderCheckDto(string[] Path, int Expected, int Found);

public sealed record ScanProgressEvent(long BytesRead, long SourceBytes) : Event
{
    public override string EventName => "scanProgress";
}

public sealed record ScanDoneEvent(string Path, string FileName, int Messages, long SourceBytes, LabelDto[] Labels, MappingDto Mapping, PreviewDto Preview) : Event
{
    public override string EventName => "scanDone";
}

public sealed record PreviewEvent(PreviewDto Preview) : Event
{
    public override string EventName => "preview";
}

public sealed record ConvertProgressEvent(int Done, int Total, long BytesRead, long SourceBytes, double MegabytesPerSecond, double? RemainingSeconds) : Event
{
    public override string EventName => "convertProgress";
}

public sealed record ConvertDoneEvent(
    string[] OutputFiles, long[] OutputSizes, string ReportPath, int Converted, int Excluded, int Skipped,
    SkippedItem[] SkippedItems, string[] Warnings, FolderCheckDto[] Checks, bool Verified, double ElapsedSeconds) : Event
{
    public override string EventName => "convertDone";

    public static ConvertDoneEvent From(ConversionResult r) => new(
        r.OutputFiles.ToArray(), r.OutputFiles.Select(f => new FileInfo(f).Length).ToArray(), r.ReportPath, r.Converted, r.Excluded, r.Skipped, r.SkippedItems.ToArray(), r.Warnings.ToArray(),
        r.Checks.Select(c => new FolderCheckDto(c.Path.ToArray(), c.Expected, c.Found)).ToArray(), r.Verified, Math.Round(r.Elapsed.TotalSeconds, 1));
}

public sealed record CancelledEvent : Event
{
    public override string EventName => "cancelled";
}

public sealed record ErrorEvent(string Message) : Event
{
    public override string EventName => "error";
}
