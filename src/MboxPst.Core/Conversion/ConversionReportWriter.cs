using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace MboxPst.Core.Conversion;

public static class ConversionReportWriter
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static IReadOnlyList<string> Write(ConversionResult result, ConversionOptions options, int sourceMessages, string directory)
    {
        string jsonPath = Path.Combine(directory, Path.GetFileName(result.ReportPath));
        string textPath = Path.ChangeExtension(jsonPath, ".txt");
        var json = new
        {
            source = options.MboxPath,
            sourceMessages,
            converted = result.Converted,
            excluded = result.Excluded,
            skipped = result.SkippedItems,
            warnings = result.Warnings,
            outputFiles = result.OutputFiles,
            verified = result.Verified,
            folders = result.Checks.Select(c => new { path = string.Join(" / ", c.Path), expected = c.Expected, found = c.Found }),
            elapsedSeconds = Math.Round(result.Elapsed.TotalSeconds, 1),
        };
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(json, Json), Encoding.UTF8);
        File.WriteAllText(textPath, Text(result, options, sourceMessages), Encoding.UTF8);
        return new[] { jsonPath, textPath };
    }

    private static string Text(ConversionResult result, ConversionOptions options, int sourceMessages)
    {
        var text = new StringBuilder();
        text.AppendLine($"Arquivo de origem: {options.MboxPath}");
        text.AppendLine($"E-mails no arquivo: {sourceMessages}");
        text.AppendLine($"Convertidos: {result.Converted}");
        text.AppendLine($"Deixados de fora por escolha: {result.Excluded}");
        text.AppendLine($"Pulados por erro: {result.Skipped}");
        text.AppendLine($"Conferência: {(result.Verified ? "tudo bate com a prévia" : "há diferenças, veja abaixo")}");
        text.AppendLine();
        text.AppendLine("Pastas (esperado / encontrado):");
        foreach (FolderCheck check in result.Checks)
            text.AppendLine($"  {string.Join(" / ", check.Path)}: {check.Expected} / {check.Found}{(check.Expected == check.Found ? "" : "  <-- diferença")}");
        if (result.SkippedItems.Count > 0)
        {
            text.AppendLine();
            text.AppendLine("E-mails pulados:");
            foreach (SkippedItem item in result.SkippedItems)
                text.AppendLine($"  {item.Identifier}: {item.Reason}");
        }
        if (result.Warnings.Count > 0)
        {
            text.AppendLine();
            text.AppendLine("Avisos:");
            foreach (string warning in result.Warnings)
                text.AppendLine($"  {warning}");
        }
        text.AppendLine();
        text.AppendLine("Arquivos gerados:");
        foreach (string file in result.OutputFiles)
            text.AppendLine($"  {file}");
        return text.ToString();
    }
}
