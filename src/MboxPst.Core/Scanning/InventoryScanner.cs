using System.Text;
using Mail2Pst.Core.Parsing;
using MboxPst.Core.Conversion;
using MboxPst.Core.Gmail;
using MimeKit;

namespace MboxPst.Core.Scanning;

public static class InventoryScanner
{
    public const string LabelsHeader = "X-Gmail-Labels";

    public static LabelInventory Scan(string mboxPath, Action<long>? onBytesRead, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        long sourceBytes = new FileInfo(mboxPath).Length;
        EnsureLooksLikeMbox(mboxPath);
        var entries = new List<InventoryEntry>();
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        var parser = new MboxParser(rawSpillThreshold: MboxParser.DefaultRawSpillThreshold);

        foreach (RawMessageHeaders message in parser.EnumerateHeaders(mboxPath, onBytesRead))
        {
            ct.ThrowIfCancellationRequested();
            IReadOnlyList<string> labels = LabelsOf(message.Headers);
            foreach (string label in labels)
                counts[label] = counts.GetValueOrDefault(label) + 1;
            entries.Add(new InventoryEntry(message.Offset, labels, message.SizeBytes));
        }

        if (entries.Count == 0)
            throw new ConversionException(EmptyMessage);
        onBytesRead?.Invoke(sourceBytes);
        return new LabelInventory { Entries = entries, CountByLabel = counts, SourceBytes = sourceBytes };
    }

    private const string EmptyMessage = "O arquivo não tem nenhum e-mail.";

    private static void EnsureLooksLikeMbox(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var buffer = new byte[64 * 1024];
        int read = stream.Read(buffer, 0, buffer.Length);
        string head = Encoding.Latin1.GetString(buffer, 0, read).TrimStart('\u00EF', '\u00BB', '\u00BF', '\r', '\n', ' ', '\t');
        if (head.Length == 0)
            throw new ConversionException(EmptyMessage);
        if (!head.StartsWith("From ", StringComparison.Ordinal))
            throw new ConversionException("Este arquivo não parece um .mbox do Gmail. Se ele veio num .zip do Google Takeout, extraia o .zip e escolha o arquivo .mbox de dentro dele.");
    }

    public static IReadOnlyList<string> LabelsOf(HeaderList headers)
    {
        int index = headers.IndexOf(LabelsHeader);
        return index < 0 ? Array.Empty<string>() : GmailLabelHeader.Parse(headers[index].RawValue);
    }
}
