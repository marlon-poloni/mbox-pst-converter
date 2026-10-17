using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Mail2Pst.Core.Models;
using Mail2Pst.Core.Writing;
using MboxPst.Core.Conversion;
using MboxPst.Core.Mapping;
using MboxPst.Core.Scanning;
using PSTFileFormat;

namespace MboxPst.Integration.Tests;

public sealed class PipelineTests : IDisposable
{
    private readonly string _out = Path.Combine(Path.GetTempPath(), "mboxpst-pipe-" + Guid.NewGuid());

    public PipelineTests() => Directory.CreateDirectory(_out);

    public void Dispose()
    {
        foreach (string f in Directory.Exists(_out) ? Directory.GetFiles(_out, "*", SearchOption.AllDirectories) : Array.Empty<string>())
            File.SetAttributes(f, FileAttributes.Normal);
        if (Directory.Exists(_out))
        {
            if (!OperatingSystem.IsWindows())
                new DirectoryInfo(_out).UnixFileMode |= UnixFileMode.UserWrite;
            Directory.Delete(_out, true);
        }
    }

    internal static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, "fixtures", name);

    private sealed record Expected(string Id, string[] Folders, string Subject, bool Read, bool Flagged = false, bool Forwarded = false);

    private static readonly Expected[] Table =
    {
        new("msg01@exemplo.com.br", new[] { "Caixa de Entrada" }, "Bom dia", true),
        new("msg02@exemplo.com.br", new[] { "Caixa de Entrada" }, "Reunião amanhã", false),
        new("msg03@exemplo.com.br", new[] { "Itens Enviados" }, "Fwd: Proposta", true, Forwarded: true),
        new("msg04@exemplo.com.br", new[] { "Caixa de Entrada" }, "Contrato assinado", true, Flagged: true),
        new("msg05@exemplo.com.br", new[] { "Lixo Eletrônico" }, "Você ganhou", false),
        new("msg06@exemplo.com.br", new[] { "Itens Excluídos" }, "Newsletter antiga", true),
        new("msg07@exemplo.com.br", new[] { "Caixa de Entrada", "Fornecedores" }, "Pedido 1234", true),
        new("msg08@exemplo.com.br", new[] { "Clientes/ACME" }, "Orçamento ACME", true),
        new("msg09@exemplo.com.br", new[] { "Caixa de Entrada", "Fornecedores", "Itens Enviados" }, "Re: Pedido 1234", true),
        new("msg10@exemplo.com.br", new[] { "Arquivo Morto" }, "Promoção de verão", true),
        new("msg11@exemplo.com.br", new[] { "Caixa de Entrada" }, "Contribuinte – ACME LTDA", false),
        new("msg12@exemplo.com.br", new[] { "Rascunhos" }, "Relatório com anexo", true),
    };

    private static (LabelInventory, MappingConfig) Prepare(string mbox = "gmail-pt.mbox", Func<MappingConfig, MappingConfig>? change = null)
    {
        LabelInventory inventory = InventoryScanner.Scan(Fixture(mbox), null, CancellationToken.None);
        MappingConfig mapping = DefaultMapping.For(inventory.CountByLabel, "Todos os e-mails");
        return (inventory, change?.Invoke(mapping) ?? mapping);
    }

    private ConversionResult Run(MappingConfig mapping, LabelInventory inventory, string mbox = "gmail-pt.mbox", long maxPartBytes = 20480L * 1024 * 1024,
        CancellationToken ct = default, Func<PstWriter>? writerFactory = null, Action<ProgressInfo>? onProgress = null)
    {
        var options = new ConversionOptions(Fixture(mbox), _out, "teste", mapping, maxPartBytes) { WriterFactory = writerFactory };
        return ConversionPipeline.Run(options, inventory, onProgress, ct);
    }

    private static MappingConfig With(MappingConfig m, string label, LabelDecision decision)
    {
        var decisions = new Dictionary<string, LabelDecision>(m.Decisions) { [label] = decision };
        return new MappingConfig { Decisions = decisions, CopyPolicy = m.CopyPolicy, UserPriority = m.UserPriority, StoreName = m.StoreName };
    }

    [Fact]
    public void Cada_email_esta_nas_pastas_certas()
    {
        var (inventory, mapping) = Prepare();
        ConversionResult result = Run(mapping, inventory);

        IReadOnlyList<PstItem> items = PstSnapshot.Read(result.OutputFiles);
        Assert.Equal(15, items.Count);
        foreach (Expected e in Table)
        {
            PstItem[] copies = items.Where(i => i.MessageId?.Trim('<', '>') == e.Id).ToArray();
            Assert.Equal(e.Folders.OrderBy(f => f), copies.Select(c => c.Folder).OrderBy(f => f));
            Assert.All(copies, c =>
            {
                Assert.Equal(e.Subject, c.Subject);
                Assert.Equal(e.Read, c.IsRead);
                Assert.Equal(e.Flagged, c.IsFlagged);
                Assert.Equal(e.Forwarded, c.IsForwarded);
                Assert.Equal($"remetente{int.Parse(e.Id[3..5])}@exemplo.com.br", c.SenderEmail);
                Assert.NotNull(c.Date);
            });
        }
        PstItem draft = items.Single(i => i.Folder == "Rascunhos");
        Assert.Equal(SourceAttachments("msg12@exemplo.com.br"), draft.Attachments);
        Assert.All(items, i => Assert.Equal("Destino", i.DisplayTo));
        Assert.Equal("Todos os e-mails", PstSnapshot.StoreName(result.OutputFiles[0]));
    }

    [Fact]
    public void Verificacao_interna_bate_com_a_previa()
    {
        var (inventory, mapping) = Prepare();
        ConversionResult result = Run(mapping, inventory);

        Assert.True(result.Verified);
        Assert.All(result.Checks, c => Assert.Equal(c.Expected, c.Found));
        Assert.Equal(6, result.Checks.Single(c => c.Path.SequenceEqual(new[] { "Caixa de Entrada" })).Found);
        Assert.Equal(12, result.Converted);
        Assert.Equal(0, result.Excluded);
        Assert.Equal(0, result.Skipped);
    }

    [SkippableFact]
    public void Leitor_independente_confirma()
    {
        string? python = Environment.GetEnvironmentVariable("MBOXPST_PYTHON") ?? "python3";
        bool available = PythonHasPypff(python);
        Assert.False(!available && Environment.GetEnvironmentVariable("CI") == "true", "pypff é obrigatório na CI");
        Skip.IfNot(available, "pypff não instalado (pip install -r tests/independent/requirements.txt)");

        var (inventory, mapping) = Prepare();
        ConversionResult result = Run(mapping, inventory);

        var attachments = SourceAttachments("msg12@exemplo.com.br");
        var expected = Table.Select(e => new
        {
            messageId = e.Id,
            folders = e.Folders,
            subject = e.Subject,
            senderEmail = $"remetente{int.Parse(e.Id[3..5])}@exemplo.com.br",
            isRead = e.Read,
            isFlagged = e.Flagged,
            isForwarded = e.Forwarded,
            attachments = e.Id == "msg12@exemplo.com.br" ? attachments.Select(a => new { name = a.Name, sha256 = a.Sha256 }).ToArray() : Array.Empty<object>(),
        });
        string expectedPath = Path.Combine(_out, "expected.json");
        File.WriteAllText(expectedPath, JsonSerializer.Serialize(expected));

        var psi = new ProcessStartInfo(python, new[] { "-I", Path.Combine(AppContext.BaseDirectory, "independent", "verify_pst.py"), expectedPath }.Concat(result.OutputFiles))
        { RedirectStandardOutput = true, RedirectStandardError = true };
        using Process process = Process.Start(psi)!;
        string output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, output);
    }

    internal static IReadOnlyList<PstAttachment> SourceAttachments(string messageId)
    {
        using var stream = File.OpenRead(Fixture("gmail-pt.mbox"));
        var parser = new MimeKit.MimeParser(stream, MimeKit.MimeFormat.Mbox);
        while (!parser.IsEndOfStream)
        {
            MimeKit.MimeMessage message = parser.ParseMessage();
            if (message.MessageId != messageId) continue;
            return message.Attachments.OfType<MimeKit.MimePart>().Select(part =>
            {
                using var data = new MemoryStream();
                part.Content.DecodeTo(data);
                return new PstAttachment(part.FileName, Convert.ToHexString(SHA256.HashData(data.ToArray())));
            }).ToArray();
        }
        throw new InvalidOperationException(messageId);
    }

    private static bool PythonHasPypff(string python)
    {
        try
        {
            using Process p = Process.Start(new ProcessStartInfo(python, new[] { "-c", "import pypff" }) { RedirectStandardError = true })!;
            p.WaitForExit();
            return p.ExitCode == 0;
        }
        catch (System.ComponentModel.Win32Exception) { return false; }
    }

    [Fact]
    public void Assunto_com_travessao_cp1252_sai_correto()
    {
        var (inventory, mapping) = Prepare();
        ConversionResult result = Run(mapping, inventory);
        Assert.Contains(PstSnapshot.Read(result.OutputFiles), i => i.Subject == "Contribuinte – ACME LTDA");
    }

    [Fact]
    public void Excluidos_ficam_fora_e_aparecem_no_relatorio()
    {
        var (inventory, mapping) = Prepare(change: m => With(m, "Lixeira", LabelDecision.Exclude()));
        ConversionResult result = Run(mapping, inventory);

        Assert.Equal(1, result.Excluded);
        Assert.Equal(11, result.Converted);
        Assert.DoesNotContain(PstSnapshot.Read(result.OutputFiles), i => i.Subject == "Newsletter antiga");
        string report = File.ReadAllText(result.ReportPath);
        Assert.Contains("\"excluded\": 1", report);
        Assert.True(File.Exists(Path.ChangeExtension(result.ReportPath, ".txt")));
    }

    [Fact]
    public void Cancelar_no_meio_nao_deixa_pst_com_nome_final()
    {
        var (inventory, mapping) = Prepare();
        using var cts = new CancellationTokenSource();
        int seen = 0;
        string tempBefore = string.Join("|", Directory.GetFiles(Path.GetTempPath(), "mail2pst-*"));

        Assert.ThrowsAny<OperationCanceledException>(() => Run(mapping, inventory, ct: cts.Token,
            writerFactory: () => new CallbackWriter(() => { if (++seen == 5) cts.Cancel(); })));

        Assert.Empty(Directory.GetFiles(_out, "*.pst"));
        Assert.Empty(Directory.GetDirectories(_out));
        Assert.Equal(tempBefore, string.Join("|", Directory.GetFiles(Path.GetTempPath(), "mail2pst-*")));
    }

    [SkippableFact]
    public void Saida_sem_permissao_gera_erro_claro()
    {
        Skip.If(OperatingSystem.IsWindows());
        var (inventory, mapping) = Prepare();
        string locked = Path.Combine(_out, "bloqueada");
        Directory.CreateDirectory(locked);
        new DirectoryInfo(locked).UnixFileMode = UnixFileMode.UserRead | UnixFileMode.UserExecute;

        var options = new ConversionOptions(Fixture("gmail-pt.mbox"), locked, "teste", mapping, 1L << 34);
        var ex = Assert.Throws<ConversionException>(() => ConversionPipeline.Run(options, inventory, null, CancellationToken.None));

        new DirectoryInfo(locked).UnixFileMode |= UnixFileMode.UserWrite;
        Assert.Contains(locked, ex.Message);
        Assert.Contains("permissão", ex.Message);
    }

    [Fact]
    public void Disco_cheio_gera_erro_claro()
    {
        var (inventory, mapping) = Prepare();
        var ex = Assert.Throws<ConversionException>(() => Run(mapping, inventory,
            writerFactory: () => new CallbackWriter(() => throw new IOException("No space left on device", 28))));

        Assert.Equal($"Espaço em disco insuficiente em {_out}.", ex.Message);
        Assert.Empty(Directory.GetFiles(_out, "*.pst"));
    }

    [Fact]
    public void Origem_nao_e_alterada()
    {
        string path = Fixture("gmail-pt.mbox");
        byte[] before = SHA256.HashData(File.ReadAllBytes(path));
        var (inventory, mapping) = Prepare();
        Run(mapping, inventory);
        Assert.Equal(before, SHA256.HashData(File.ReadAllBytes(path)));
    }

    [Fact]
    public void Divide_em_partes_quando_passa_do_limite()
    {
        var (inventory, mapping) = Prepare();
        ConversionResult result = Run(mapping, inventory, maxPartBytes: 400L * 1024);

        Assert.True(result.OutputFiles.Count >= 2, string.Join(",", result.OutputFiles));
        Assert.Equal("teste-1.pst", Path.GetFileName(result.OutputFiles[0]));
        Assert.Equal(15, PstSnapshot.Read(result.OutputFiles).Count);
        Assert.True(result.Verified);
    }

    [Fact]
    public void Progresso_chega_ao_total()
    {
        var (inventory, mapping) = Prepare();
        var events = new List<ProgressInfo>();
        Run(mapping, inventory, onProgress: events.Add);

        ProgressInfo last = events.Last();
        Assert.Equal(12, last.Total);
        Assert.Equal(12, last.Done);
        Assert.Equal(inventory.SourceBytes, last.BytesRead);
    }

    [Fact]
    public void Mbox_sem_marcadores_vai_para_pasta_com_nome_do_arquivo()
    {
        var (inventory, mapping) = Prepare("sample.mbox");
        ConversionResult result = Run(mapping, inventory, mbox: "sample.mbox");
        Assert.All(PstSnapshot.Read(result.OutputFiles), i => Assert.Equal("sample", i.Folder));
        Assert.True(result.Verified);
    }


    private ConversionResult RunFile(string mboxPath, Func<MappingConfig, MappingConfig>? change = null, CancellationToken ct = default,
        Action<ProgressInfo>? onProgress = null, string outputName = "teste")
    {
        LabelInventory inventory = InventoryScanner.Scan(mboxPath, null, CancellationToken.None);
        MappingConfig mapping = DefaultMapping.For(inventory.CountByLabel, "Todos os e-mails");
        mapping = change?.Invoke(mapping) ?? mapping;
        return ConversionPipeline.Run(new ConversionOptions(mboxPath, _out, outputName, mapping, 1L << 34), inventory, onProgress, ct);
    }

    private string Source() => Path.Combine(_out, "origem");

    [Theory]
    [InlineData("*")]
    [InlineData("Backup?")]
    [InlineData("a/b")]
    [InlineData("..")]
    [InlineData("  ")]
    public void Nome_de_saida_invalido_nao_apaga_outros_arquivos(string name)
    {
        File.WriteAllText(Path.Combine(_out, "pessoal.pst"), "nao apagar");
        File.WriteAllText(Path.Combine(_out, "Backup1.pst"), "nao apagar");

        var (inventory, mapping) = Prepare();
        var options = new ConversionOptions(Fixture("gmail-pt.mbox"), _out, name, mapping, 1L << 34);
        var ex = Assert.Throws<ConversionException>(() => ConversionPipeline.Run(options, inventory, null, CancellationToken.None));

        Assert.StartsWith("O nome do arquivo", ex.Message);
        Assert.True(File.Exists(Path.Combine(_out, "pessoal.pst")));
        Assert.True(File.Exists(Path.Combine(_out, "Backup1.pst")));
    }

    [Fact]
    public void Arquivo_com_nome_parecido_nao_bloqueia_nem_e_apagado()
    {
        File.WriteAllText(Path.Combine(_out, "teste-antigo.pst"), "nao apagar");
        var (inventory, mapping) = Prepare();
        ConversionResult result = Run(mapping, inventory);
        Assert.True(result.Verified);
        Assert.True(File.Exists(Path.Combine(_out, "teste-antigo.pst")));
    }

    [Fact]
    public void Ja_existir_o_arquivo_de_saida_bloqueia_com_o_nome_certo()
    {
        File.WriteAllText(Path.Combine(_out, "teste-2.pst"), "x");
        var (inventory, mapping) = Prepare();
        var ex = Assert.Throws<ConversionException>(() => Run(mapping, inventory));
        Assert.Contains("teste-2.pst", ex.Message);
    }

    [Fact]
    public void Relatorio_que_nao_pode_ser_gravado_nao_deixa_pst_com_nome_final()
    {
        Directory.CreateDirectory(Path.Combine(_out, "teste-relatorio.json"));
        var (inventory, mapping) = Prepare();
        var ex = Assert.Throws<ConversionException>(() => Run(mapping, inventory));
        Assert.Contains("teste-relatorio.json", ex.Message);
        Assert.Empty(Directory.GetFiles(_out, "*.pst"));
    }

    [Fact]
    public void Relatorio_e_pst_so_aparecem_com_nome_final_juntos_no_fim()
    {
        var (inventory, mapping) = Prepare();
        ConversionResult result = Run(mapping, inventory);
        Assert.True(File.Exists(result.ReportPath));
        Assert.Equal(_out, Path.GetDirectoryName(result.ReportPath));
        Assert.Empty(Directory.GetDirectories(_out));
    }

    [Fact]
    public void Codificacao_corrigida_aparece_nos_avisos()
    {
        var (inventory, mapping) = Prepare();
        ConversionResult result = Run(mapping, inventory);
        Assert.Contains(result.Warnings, w => w.Contains("codificação corrigida"));
    }

    [Fact]
    public void Caixa_diferente_vira_uma_pasta_so_no_pst()
    {
        Directory.CreateDirectory(Source());
        string mbox = new MboxBuilder().Add("Clientes").Add("clientes/ACME").Add("CLIENTES/acme").Save(Source());

        ConversionResult result = RunFile(mbox);

        IReadOnlyList<PstItem> items = PstSnapshot.Read(result.OutputFiles);
        Assert.Equal(new[] { "Clientes", "Clientes/ACME", "Clientes/ACME" }, items.Select(i => i.Folder).OrderBy(f => f, StringComparer.Ordinal));
        Assert.True(result.Verified);
    }

    [Fact]
    public void Marcador_com_nome_de_pasta_especial_cai_na_pasta_especial()
    {
        Directory.CreateDirectory(Source());
        string mbox = new MboxBuilder().Add("Itens excluídos").Add("Lixeira").Save(Source());

        ConversionResult result = RunFile(mbox);

        var pst = new PSTFile(result.OutputFiles[0], FileAccess.Read);
        try
        {
            PSTFolder[] deleted = pst.TopOfPersonalFolders.GetChildFolders()
                .Where(f => string.Equals(f.DisplayName, "Itens Excluídos", StringComparison.OrdinalIgnoreCase)).ToArray();
            PSTFolder only = Assert.Single(deleted);
            Assert.Equal(0x8062u, only.NodeID.Value);
            Assert.Equal(2, only.MessageCount);
        }
        finally { pst.CloseFile(); }
        Assert.True(result.Verified);
    }

    [Fact]
    public void Verificacao_acusa_pastas_irmas_que_so_diferem_na_caixa()
    {
        Directory.CreateDirectory(Source());
        string mbox = new MboxBuilder().Add("Caixa de entrada").Save(Source());
        LabelInventory inventory = InventoryScanner.Scan(mbox, null, CancellationToken.None);
        MappingConfig mapping = DefaultMapping.For(inventory.CountByLabel, "x");
        ConversionResult result = ConversionPipeline.Run(new ConversionOptions(mbox, _out, "teste", mapping, 1L << 34), inventory, null, CancellationToken.None);

        var expected = new MboxPst.Core.Previewing.FolderNode("x", Array.Empty<string>(), 0,
            new[] { new MboxPst.Core.Previewing.FolderNode("caixa de entrada", new[] { "caixa de entrada" }, 1, Array.Empty<MboxPst.Core.Previewing.FolderNode>()) });
        IReadOnlyList<FolderCheck> checks = OutputVerifier.Check(result.OutputFiles, expected);
        Assert.Contains(checks, c => c.Expected != c.Found);
    }

    [Fact]
    public void Cancelar_enquanto_o_leitor_passa_por_excluidos_nao_termina_com_sucesso()
    {
        Directory.CreateDirectory(Source());
        string mbox = new MboxBuilder().AddMany(24, "Caixa de entrada").AddMany(3, "Lixeira").AddMany(5, "Caixa de entrada").Save(Source());
        using var cts = new CancellationTokenSource();

        Assert.ThrowsAny<OperationCanceledException>(() => RunFile(mbox, m => With(m, "Lixeira", LabelDecision.Exclude()), cts.Token,
            onProgress: p => CancelAt(p, 25, cts)));

        Assert.Empty(Directory.GetFiles(_out, "*.pst"));
        Assert.Empty(Directory.GetFiles(_out, "*-relatorio.*"));
    }

    [Fact]
    public void Cancelar_nao_deixa_anexo_temporario_do_email_em_leitura()
    {
        Directory.CreateDirectory(Source());
        string mbox = new MboxBuilder().AddMany(25, "Caixa de entrada").Add("Caixa de entrada", attachmentBytes: 5 * 1024 * 1024).AddMany(3, "Caixa de entrada").Save(Source());
        HashSet<string> before = TempFiles();
        using var cts = new CancellationTokenSource();

        Assert.ThrowsAny<OperationCanceledException>(() => RunFile(mbox, ct: cts.Token, onProgress: p => CancelAt(p, 25, cts)));

        Assert.Empty(TempFiles().Except(before));
    }

    [Fact]
    public void Durante_a_conversao_o_nome_final_ainda_nao_existe()
    {
        var (inventory, mapping) = Prepare();
        bool sawFinal = false;
        ConversionResult result = Run(mapping, inventory, writerFactory: () => new CallbackWriter(() => sawFinal |= File.Exists(Path.Combine(_out, "teste.pst"))));
        Assert.False(sawFinal);
        Assert.Equal(Path.Combine(_out, "teste.pst"), result.OutputFiles.Single());
        Assert.Empty(Directory.GetDirectories(_out));
    }

    [Fact]
    public void Conversao_interrompida_antes_nao_bloqueia_a_proxima()
    {
        string stale = Path.Combine(_out, ".conversor-0123456789abcdef");
        Directory.CreateDirectory(stale);
        File.WriteAllText(Path.Combine(stale, "teste.pst"), "parcial");

        var (inventory, mapping) = Prepare();
        ConversionResult result = Run(mapping, inventory);

        Assert.True(result.Verified);
        Assert.False(Directory.Exists(stale));
    }

    private static void CancelAt(ProgressInfo p, int done, CancellationTokenSource cts)
    {
        if (p.Done != done) return;
        Thread.Sleep(500);
        cts.Cancel();
    }

    private static HashSet<string> TempFiles() => Directory.GetFiles(Path.GetTempPath(), "mail2pst-*").ToHashSet();

    private sealed class CallbackWriter(Action beforeEachMessage) : PstWriter
    {
        internal override void WriteMessageCore(PSTFile file, PSTFolder folder, MailMessage message)
        {
            beforeEachMessage();
            base.WriteMessageCore(file, folder, message);
        }
    }
}
