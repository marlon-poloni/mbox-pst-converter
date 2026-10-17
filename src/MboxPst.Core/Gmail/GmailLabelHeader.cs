using System.Text;
using MboxPst.Core.Text;
using MimeKit;
using MimeKit.Utils;

namespace MboxPst.Core.Gmail;

public static class GmailLabelHeader
{
    public static IReadOnlyList<string> Parse(byte[]? rawValue)
    {
        if (rawValue is null || rawValue.Length == 0)
            return Array.Empty<string>();

        string text = Cp1252Repair.Fix(Rfc2047.DecodeText(ParserOptions.Default, Unfold(rawValue)))!;
        IEnumerable<string> pieces = TrySplitQuoted(text) ?? text.Split(',').Select(p => p.Replace("\"", ""));

        var labels = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string piece in pieces)
        {
            string label = Normalize(piece);
            if (label.Length > 0 && seen.Add(label))
                labels.Add(label);
        }
        return labels;
    }

    private static byte[] Unfold(byte[] raw)
    {
        var result = new List<byte>(raw.Length);
        bool inEncodedWord = false;
        int questionMarks = 0;
        for (int i = 0; i < raw.Length; i++)
        {
            byte b = raw[i];
            if (b == (byte)'\r' || b == (byte)'\n')
            {
                if (inEncodedWord)
                    while (i + 1 < raw.Length && (raw[i + 1] == (byte)' ' || raw[i + 1] == (byte)'\t'))
                        i++;
                continue;
            }

            if (!inEncodedWord && b == (byte)'=' && i + 1 < raw.Length && raw[i + 1] == (byte)'?')
            {
                inEncodedWord = true;
                questionMarks = 0;
            }
            else if (inEncodedWord && b == (byte)'?')
            {
                questionMarks++;
                if (questionMarks >= 4 && i + 1 < raw.Length && raw[i + 1] == (byte)'=')
                    inEncodedWord = false;
            }
            result.Add(b);
        }
        return result.ToArray();
    }

    private static List<string>? TrySplitQuoted(string text)
    {
        var pieces = new List<string>();
        var current = new StringBuilder();
        int i = 0;
        while (i <= text.Length)
        {
            int start = i;
            while (i < text.Length && char.IsWhiteSpace(text[i])) i++;
            if (i < text.Length && text[i] == '"')
            {
                i++;
                bool closed = false;
                while (i < text.Length)
                {
                    char c = text[i++];
                    if (c == '\\' && i < text.Length) { current.Append(text[i++]); continue; }
                    if (c == '"') { closed = true; break; }
                    current.Append(c);
                }
                if (!closed)
                    return null;
                while (i < text.Length && text[i] != ',') current.Append(text[i++]);
            }
            else
            {
                i = start;
                while (i < text.Length && text[i] != ',') current.Append(text[i++]);
            }
            pieces.Add(current.ToString());
            current.Clear();
            i++;
        }
        return pieces;
    }

    private static string Normalize(string piece) =>
        string.Join(' ', piece.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
