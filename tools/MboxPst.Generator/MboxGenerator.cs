using System.Text;

namespace MboxPst.Generator;

public sealed record GeneratedMessage(string MessageId, IReadOnlyList<string> Labels, string Subject, int AttachmentBytes);

public sealed record GeneratedMbox(IReadOnlyList<GeneratedMessage> Messages);

public static class MboxGenerator
{
    private static readonly (string Label, int Weight)[] Folders =
    {
        ("Caixa de entrada", 70), ("Enviado", 10), ("Arquivada", 5), ("Spam", 3), ("Lixeira", 2), ("Fornecedores", 5), ("Clientes/ACME", 5),
    };

    public static GeneratedMbox Generate(string path, long targetBytes, int seed)
    {
        var random = new Random(seed);
        var messages = new List<GeneratedMessage>();
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 20);
        using var writer = new StreamWriter(stream, new UTF8Encoding(false), 1 << 20) { NewLine = "\n" };

        while (stream.Position + writer.Encoding.GetMaxByteCount(0) < targetBytes)
        {
            int n = messages.Count + 1;
            List<string> labels = PickLabels(random, multiFolder: n % 20 == 2);
            int attachment = random.NextDouble() < 0.30 ? AttachmentSize(random) : 0;
            string subject = $"Mensagem {n} – relatório de ação nº {random.Next(1000, 9999)}";
            string id = $"gen{n:D7}.{seed}@exemplo.com.br";
            WriteMessage(writer, random, id, labels, subject, attachment, n);
            messages.Add(new GeneratedMessage(id, labels, subject, attachment));
            writer.Flush();
        }
        return new GeneratedMbox(messages);
    }

    private static List<string> PickLabels(Random random, bool multiFolder)
    {
        var labels = new List<string> { Pick(random) };
        if (multiFolder)
        {
            string[] others = Folders.Select(f => f.Label).Where(l => l != labels[0]).ToArray();
            labels.Add(others[random.Next(others.Length)]);
        }
        if (random.NextDouble() < 0.30) labels.Add("Aberto");
        if (random.NextDouble() < 0.15) labels.Add("Não lidas");
        if (random.NextDouble() < 0.10) labels.Add("Categoria: atualizações");
        if (random.NextDouble() < 0.03) labels.Add("Com estrela");
        return labels;
    }

    private static string Pick(Random random)
    {
        int roll = random.Next(100);
        foreach (var (label, weight) in Folders)
        {
            if (roll < weight) return label;
            roll -= weight;
        }
        return Folders[0].Label;
    }

    private static int AttachmentSize(Random random)
    {
        double roll = random.NextDouble();
        if (roll < 0.70) return random.Next(10 * 1024, 200 * 1024);
        if (roll < 0.95) return random.Next(200 * 1024, 4 * 1024 * 1024);
        return random.Next(4 * 1024 * 1024, 25 * 1024 * 1024);
    }

    private static void WriteMessage(StreamWriter w, Random random, string id, List<string> labels, string subject, int attachmentBytes, int n)
    {
        w.WriteLine($"From {n}@xxx Mon Oct 13 10:00:00 +0000 2026");
        w.WriteLine($"X-GM-THRID: {1800000000000000000L + n}");
        string joined = string.Join(",", labels);
        if (n % 10 == 0)
        {
            int cut = joined.Length / 2;
            w.WriteLine($"X-Gmail-Labels: =?UTF-8?Q?{QEncode(joined[..cut])}?=");
            w.WriteLine($" =?UTF-8?Q?{QEncode(joined[cut..])}?=");
        }
        else
        {
            w.WriteLine($"X-Gmail-Labels: {joined}");
        }
        w.WriteLine($"Message-ID: <{id}>");
        w.WriteLine($"Date: Mon, {1 + n % 28} Sep 2026 {n % 24:D2}:{n % 60:D2}:00 -0300");
        w.WriteLine($"From: Remetente {n % 97} <remetente{n % 97}@exemplo.com.br>");
        w.WriteLine("To: Destino <destino@exemplo.com.br>");
        w.WriteLine($"Subject: =?UTF-8?Q?{QEncode(subject)}?=");
        w.WriteLine("MIME-Version: 1.0");
        string body = $"<html><body><p>Corpo da mensagem {n}: ação, coração, não.</p></body></html>";
        if (attachmentBytes == 0)
        {
            w.WriteLine("Content-Type: text/html; charset=UTF-8");
            w.WriteLine("Content-Transfer-Encoding: 8bit");
            w.WriteLine();
            w.WriteLine(body);
            w.WriteLine();
            return;
        }
        w.WriteLine("Content-Type: multipart/mixed; boundary=\"limite\"");
        w.WriteLine();
        w.WriteLine("--limite");
        w.WriteLine("Content-Type: text/html; charset=UTF-8");
        w.WriteLine("Content-Transfer-Encoding: 8bit");
        w.WriteLine();
        w.WriteLine(body);
        w.WriteLine("--limite");
        w.WriteLine($"Content-Type: application/octet-stream; name=\"anexo-{n}.bin\"");
        w.WriteLine($"Content-Disposition: attachment; filename=\"anexo-{n}.bin\"");
        w.WriteLine("Content-Transfer-Encoding: base64");
        w.WriteLine();
        var data = new byte[attachmentBytes];
        random.NextBytes(data);
        string base64 = Convert.ToBase64String(data);
        for (int i = 0; i < base64.Length; i += 76)
            w.WriteLine(base64.AsSpan(i, Math.Min(76, base64.Length - i)));
        w.WriteLine("--limite--");
        w.WriteLine();
    }

    private static string QEncode(string text)
    {
        var sb = new StringBuilder();
        foreach (byte b in Encoding.UTF8.GetBytes(text))
        {
            if (b == (byte)' ') sb.Append('_');
            else if (b < 128 && (char.IsLetterOrDigit((char)b) || b is (byte)'.' or (byte)':' or (byte)'/' or (byte)'-')) sb.Append((char)b);
            else sb.Append('=').Append(b.ToString("X2"));
        }
        return sb.ToString();
    }
}
