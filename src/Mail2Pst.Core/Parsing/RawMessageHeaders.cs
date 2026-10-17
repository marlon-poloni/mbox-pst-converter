#nullable enable
using MimeKit;

namespace Mail2Pst.Core.Parsing;

public sealed record RawMessageHeaders(long Offset, long SizeBytes, HeaderList Headers);
