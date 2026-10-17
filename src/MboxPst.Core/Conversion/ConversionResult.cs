using Mail2Pst.Core.Writing;
using MboxPst.Core.Mapping;

namespace MboxPst.Core.Conversion;

public sealed record ConversionOptions(string MboxPath, string OutputDirectory, string OutputName, MappingConfig Mapping, long MaxPartBytes)
{
    internal Func<PstWriter>? WriterFactory { get; init; }
}

public sealed record FolderCheck(IReadOnlyList<string> Path, int Expected, int Found);

public sealed record SkippedItem(string Identifier, string Reason);

public sealed record ConversionResult(
    IReadOnlyList<string> OutputFiles,
    string ReportPath,
    int Converted,
    int Excluded,
    int Skipped,
    IReadOnlyList<SkippedItem> SkippedItems,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<FolderCheck> Checks,
    bool Verified,
    TimeSpan Elapsed);

public sealed record ProgressInfo(int Done, int Total, long BytesRead, long SourceBytes, double MegabytesPerSecond, TimeSpan? Remaining);

public sealed class ConversionException(string message, Exception? inner = null) : Exception(message, inner);
