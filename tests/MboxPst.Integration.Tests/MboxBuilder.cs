using System.Text;

namespace MboxPst.Integration.Tests;

public sealed class MboxBuilder
{
    private readonly StringBuilder _text = new();
    private int _count;

    public MboxBuilder Add(string labels, string? subject = null, int attachmentBytes = 0)
    {
        _count++;
        subject ??= $"Mensagem {_count}";
        _text.Append($"From x@xxx Mon Oct 13 10:00:00 +0000 2026\n");
        _text.Append($"X-Gmail-Labels: {labels}\n");
        _text.Append($"Message-ID: <b{_count:D4}@exemplo.com.br>\n");
        _text.Append("Date: Mon, 13 Oct 2026 10:00:00 -0300\n");
        _text.Append($"From: Remetente <r{_count}@exemplo.com.br>\nTo: d@exemplo.com.br\nSubject: {subject}\nMIME-Version: 1.0\n");
        if (attachmentBytes == 0)
        {
            _text.Append("Content-Type: text/plain; charset=UTF-8\n\ncorpo\n\n");
            return this;
        }
        string data = Convert.ToBase64String(Enumerable.Range(0, attachmentBytes).Select(i => (byte)(i * 31)).ToArray(), Base64FormattingOptions.InsertLineBreaks).Replace("\r\n", "\n");
        _text.Append("Content-Type: multipart/mixed; boundary=\"b\"\n\n--b\nContent-Type: text/plain; charset=UTF-8\n\ncorpo\n");
        _text.Append($"--b\nContent-Type: application/octet-stream; name=\"a.bin\"\nContent-Disposition: attachment; filename=\"a.bin\"\nContent-Transfer-Encoding: base64\n\n{data}\n--b--\n\n");
        return this;
    }

    public MboxBuilder AddMany(int count, string labels)
    {
        for (int i = 0; i < count; i++) Add(labels);
        return this;
    }

    public string Save(string directory, string name = "gerado.mbox")
    {
        string path = Path.Combine(directory, name);
        File.WriteAllText(path, _text.ToString(), new UTF8Encoding(false));
        return path;
    }
}
