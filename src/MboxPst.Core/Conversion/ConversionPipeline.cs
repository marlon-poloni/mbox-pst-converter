using System.Diagnostics;
using System.Text.RegularExpressions;
using Mail2Pst.Core.Mapping;
using Mail2Pst.Core.Models;
using Mail2Pst.Core.Parsing;
using Mail2Pst.Core.Reporting;
using Mail2Pst.Core.Writing;
using MboxPst.Core.Gmail;
using MboxPst.Core.Mapping;
using MboxPst.Core.Previewing;
using MboxPst.Core.Scanning;
using MboxPst.Core.Text;

namespace MboxPst.Core.Conversion;

public static class ConversionPipeline
{
    private const int ProgressEvery = 25;

    public static ConversionResult Run(ConversionOptions options, LabelInventory inventory, Action<ProgressInfo>? onProgress, CancellationToken ct)
    {
        var clock = Stopwatch.StartNew();
        ValidateOutputName(options.OutputName);
        string outputDirectory = Path.GetFullPath(options.OutputDirectory);
        EnsureWritable(outputDirectory, options.OutputName);
        RemoveStaleWorkDirectories(outputDirectory);
        string workDirectory = Path.Combine(outputDirectory, $"{WorkPrefix}{Guid.NewGuid():N}");
        Directory.CreateDirectory(workDirectory);

        string fallbackFolder = Path.GetFileNameWithoutExtension(options.MboxPath);
        Preview preview = PreviewBuilder.Build(inventory, options.Mapping, fallbackFolder);
        Dictionary<string, IReadOnlyList<string>> canonical = CanonicalPaths(preview.Root);

        var skipped = new List<SkippedItem>();
        var warnings = new List<string>();
        int excluded = 0, done = 0;
        long bytesRead = 0;
        int total = inventory.Entries.Count;

        void Report(bool force)
        {
            if (onProgress is null || (!force && done % ProgressEvery != 0))
                return;
            double seconds = Math.Max(clock.Elapsed.TotalSeconds, 0.001);
            double rate = bytesRead / seconds;
            TimeSpan? remaining = rate > 0 && bytesRead < inventory.SourceBytes
                ? TimeSpan.FromSeconds((inventory.SourceBytes - bytesRead) / rate)
                : force ? TimeSpan.Zero : null;
            onProgress(new ProgressInfo(done, total, Math.Min(bytesRead, inventory.SourceBytes), inventory.SourceBytes, rate / (1024 * 1024), remaining));
        }

        IEnumerable<PlannedMessage> Messages()
        {
            var parser = new MboxParser(rawSpillThreshold: MboxParser.DefaultRawSpillThreshold);
            foreach (ParseResult parsed in parser.Parse(options.MboxPath, read => bytesRead = read))
            {
                PlannedMessage? planned;
                try
                {
                    ct.ThrowIfCancellationRequested();
                    done++;
                    Report(force: false);
                    planned = Plan(parsed);
                }
                catch
                {
                    DisposeAttachments(parsed.Message);
                    throw;
                }
                if (planned is not null)
                    yield return planned;
            }
            bytesRead = inventory.SourceBytes;
        }

        PlannedMessage? Plan(ParseResult parsed)
        {
            if (!parsed.Success)
            {
                skipped.Add(new SkippedItem(parsed.Source.Identifier, parsed.Error!));
                return null;
            }
            foreach (string warning in parsed.Warnings)
                warnings.Add($"{parsed.Source.Identifier}: {warning}");

            MailMessage message = parsed.Message!;
            IReadOnlyList<string> labels = GmailLabelHeader.Parse(message.GmailLabelsRaw);
            Route route = LabelRouter.For(labels, options.Mapping, fallbackFolder);
            if (route.Excluded)
            {
                excluded++;
                DisposeAttachments(message);
                return null;
            }

            if (Repair(message))
                warnings.Add($"{parsed.Source.Identifier}: codificação corrigida (caracteres do Windows-1252 no assunto, nomes ou texto)");
            if (labels.Count > 0)
            {
                message.IsRead = route.IsRead;
                message.IsFlagged = route.IsFlagged;
                message.IsForwarded |= route.IsForwarded;
                message.Categories = route.Categories.ToList();
            }

            return new PlannedMessage
            {
                Message = message,
                TargetFolderPaths = route.Folders.Select(p => canonical.GetValueOrDefault(Key(p), p)).ToArray(),
            };
        }

        PstWriter writer = options.WriterFactory?.Invoke() ?? new PstWriter();
        writer.StoreDisplayName = options.Mapping.StoreName;
        writer.DeletedItemsDisplayName = FolderNames.Deleted;
        var plan = new PstOutputPlan { Name = options.OutputName, MaxSizeBytes = options.MaxPartBytes, IncludeEmptyFolders = false };
        var writeReport = new ConversionReport();

        var moved = new List<string>();
        try
        {
            List<string> written = writer.WritePlan(plan, Messages(), workDirectory, writeReport, total, null, ct);
            ct.ThrowIfCancellationRequested();

            foreach (SkippedMessage s in writeReport.Skipped)
                skipped.Add(new SkippedItem(s.Identifier, s.Reason));
            foreach (SkippedMessage w in writeReport.Warnings)
                warnings.Add($"{w.Identifier}: {w.Reason}");
            Report(force: true);

            IReadOnlyList<FolderCheck> checks = OutputVerifier.Check(written, preview.Root);
            bool verified = checks.All(c => c.Expected == c.Found) && skipped.Count == 0;
            List<string> outputs = written.Select(f => Path.Combine(outputDirectory, Path.GetFileName(f))).ToList();
            string reportPath = Path.Combine(outputDirectory, ReportName(options.OutputName));
            var result = new ConversionResult(outputs, reportPath, writeReport.ConvertedCount, excluded, skipped.Count, skipped, warnings, checks, verified, clock.Elapsed);
            IReadOnlyList<string> reports = ConversionReportWriter.Write(result, options, inventory.Entries.Count, workDirectory);

            foreach (string file in written.Concat(reports))
            {
                string final = Path.Combine(outputDirectory, Path.GetFileName(file));
                File.Move(file, final);
                moved.Add(final);
            }
            Directory.Delete(workDirectory, recursive: true);
            return result;
        }
        catch (OperationCanceledException)
        {
            Undo(moved, workDirectory);
            throw;
        }
        catch (Exception ex)
        {
            Undo(moved, workDirectory);
            throw Translate(ex, outputDirectory) ?? throw new ConversionException($"A conversão falhou: {ex.Message}", ex);
        }
    }

    private static string ReportName(string outputName) => $"{outputName}-relatorio.json";

    private static void Undo(IEnumerable<string> moved, string workDirectory)
    {
        foreach (string file in moved)
        {
            try { File.Delete(file); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        TryDeleteDirectory(workDirectory);
    }

    private static void DisposeAttachments(MailMessage? message)
    {
        if (message is null) return;
        foreach (MailAttachment attachment in message.Attachments)
            attachment.Content.Dispose();
    }

    private static readonly char[] ForbiddenNameChars = { '\\', '/', ':', '*', '?', '"', '<', '>', '|' };

    private static void ValidateOutputName(string name)
    {
        bool bad = string.IsNullOrWhiteSpace(name) || name.Trim() is "." or ".." || name.Length > 200
            || name.IndexOfAny(ForbiddenNameChars) >= 0 || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name != name.Trim();
        if (bad)
            throw new ConversionException("O nome do arquivo não pode ficar vazio, ter espaço nas pontas nem conter estes caracteres: \\ / : * ? \" < > |");
    }

    private static IEnumerable<string> PartsOf(string directory, string outputName)
    {
        var pattern = new Regex($"^{Regex.Escape(outputName)}(-[0-9]+)?\\.pst$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return Directory.EnumerateFiles(directory).Where(f => pattern.IsMatch(Path.GetFileName(f)));
    }

    private static bool Repair(MailMessage message)
    {
        bool changed = false;
        string? Fix(string? text)
        {
            string? fixedText = Cp1252Repair.Fix(text);
            changed |= !ReferenceEquals(fixedText, text);
            return fixedText;
        }
        message.Subject = Fix(message.Subject);
        message.TextBody = Fix(message.TextBody);
        message.HtmlBody = Fix(message.HtmlBody);
        foreach (MailAddress? address in new[] { message.From }.Concat(message.To).Concat(message.Cc).Concat(message.Bcc))
            if (address is not null)
                address.Name = Fix(address.Name);
        return changed;
    }

    private static Dictionary<string, IReadOnlyList<string>> CanonicalPaths(FolderNode root)
    {
        var map = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        void Visit(FolderNode node)
        {
            if (node.Path.Count > 0)
                map[Key(node.Path)] = node.Path;
            foreach (FolderNode child in node.Children)
                Visit(child);
        }
        Visit(root);
        return map;
    }

    private static string Key(IReadOnlyList<string> path) => string.Join('\u001F', path);

    private static void EnsureWritable(string directory, string outputName)
    {
        try
        {
            Directory.CreateDirectory(directory);
            using var probe = new FileStream(Path.Combine(directory, $".mboxpst-{Guid.NewGuid():N}"), FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            throw new ConversionException($"Sem permissão para gravar em {directory}.", ex);
        }

        string? existing = PartsOf(directory, outputName)
            .Concat(new[] { ReportName(outputName), Path.ChangeExtension(ReportName(outputName), ".txt") }
                .Select(n => Path.Combine(directory, n)).Where(p => File.Exists(p) || Directory.Exists(p)))
            .FirstOrDefault();
        if (existing is not null)
            throw new ConversionException($"Já existe o arquivo {Path.GetFileName(existing)} em {directory}. Escolha outro nome ou outra pasta.");
    }

    private static ConversionException? Translate(Exception ex, string directory)
    {
        for (Exception? e = ex; e is not null; e = e.InnerException)
        {
            if (e is IOException io && IsDiskFull(io))
                return new ConversionException($"Espaço em disco insuficiente em {directory}.", ex);
            if (e is UnauthorizedAccessException)
                return new ConversionException($"Sem permissão para gravar em {directory}.", ex);
        }
        return null;
    }

    private static bool IsDiskFull(IOException ex) =>
        (OperatingSystem.IsWindows() ? (ex.HResult & 0xFFFF) is 0x70 or 0x27 : ex.HResult == 28) ||
        ex.Message.Contains("No space left", StringComparison.OrdinalIgnoreCase);

    private const string WorkPrefix = ".conversor-";

    private static void RemoveStaleWorkDirectories(string directory)
    {
        var pattern = new Regex("^\\.conversor-[0-9a-f]{16,32}$");
        foreach (string stale in Directory.EnumerateDirectories(directory).Where(d => pattern.IsMatch(Path.GetFileName(d))).ToList())
            TryDeleteDirectory(stale);
    }

    private static void TryDeleteDirectory(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
