using System.Security.Cryptography;
using Mail2Pst.Core.Parsing;
using MboxPst.Core.Scanning;

namespace MboxPst.Core.Tests.Scanning;

public class InventoryScannerTests
{
    internal static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, "fixtures", name);

    [Fact]
    public void Scan_conta_emails_e_marcadores()
    {
        LabelInventory inventory = InventoryScanner.Scan(Fixture("gmail-pt.mbox"), null, CancellationToken.None);

        Assert.Equal(12, inventory.Entries.Count);
        var expected = new Dictionary<string, int>
        {
            ["Caixa de entrada"] = 6,
            ["Aberto"] = 2,
            ["Categoria: pessoal"] = 1,
            ["Não lidas"] = 3,
            ["Importante"] = 2,
            ["Enviado"] = 2,
            ["IMAP_$Forwarded"] = 1,
            ["Com estrela"] = 1,
            ["Spam"] = 1,
            ["Lixeira"] = 1,
            ["Fornecedores"] = 2,
            ["Clientes/ACME"] = 1,
            ["Categoria: promoções"] = 1,
            ["Categoria: atualizações"] = 1,
            ["Rascunhos"] = 1,
        };
        Assert.Equal(expected.OrderBy(p => p.Key), inventory.CountByLabel.OrderBy(p => p.Key));
        Assert.Equal(new FileInfo(Fixture("gmail-pt.mbox")).Length, inventory.SourceBytes);
    }

    [Fact]
    public void Scan_guarda_marcadores_e_tamanho_por_email_em_ordem()
    {
        LabelInventory inventory = InventoryScanner.Scan(Fixture("gmail-pt.mbox"), null, CancellationToken.None);

        Assert.Equal(new[] { "Caixa de entrada", "Aberto", "Categoria: pessoal" }, inventory.Entries[0].Labels);
        Assert.Equal(new[] { "Rascunhos" }, inventory.Entries[11].Labels);
        Assert.True(inventory.Entries[11].SizeBytes > 600 * 1024);
        Assert.True(inventory.Entries.Zip(inventory.Entries.Skip(1)).All(p => p.First.Offset < p.Second.Offset));
    }

    [Fact]
    public void Scan_nao_altera_o_arquivo()
    {
        string path = Fixture("gmail-pt.mbox");
        byte[] before = SHA256.HashData(File.ReadAllBytes(path));
        DateTime modified = File.GetLastWriteTimeUtc(path);

        InventoryScanner.Scan(path, null, CancellationToken.None);

        Assert.Equal(before, SHA256.HashData(File.ReadAllBytes(path)));
        Assert.Equal(modified, File.GetLastWriteTimeUtc(path));
    }

    [Fact]
    public void Scan_trata_bug_de_fim_de_arquivo()
    {
        string path = Fixture("mbox-eof-bug.mbox");
        LabelInventory inventory = InventoryScanner.Scan(path, null, CancellationToken.None);
        Assert.Equal(new MboxParser().CountMessages(path), inventory.Entries.Count);
        Assert.True(inventory.Entries.Count >= 2);
    }

    [Fact]
    public void Scan_reporta_progresso_ate_o_tamanho_do_arquivo()
    {
        string path = Fixture("gmail-pt.mbox");
        long last = 0;
        InventoryScanner.Scan(path, read => last = Math.Max(last, read), CancellationToken.None);
        Assert.Equal(new FileInfo(path).Length, last);
    }

    [Fact]
    public void Scan_cancelado_lanca_OperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => InventoryScanner.Scan(Fixture("gmail-pt.mbox"), null, cts.Token));
    }

    [Fact]
    public void Arquivo_que_nao_e_mbox_e_recusado_com_mensagem_clara()
    {
        string path = Path.Combine(Path.GetTempPath(), $"nao-mbox-{Guid.NewGuid()}.zip");
        File.WriteAllBytes(path, new byte[] { 0x50, 0x4B, 0x03, 0x04, 1, 2, 3, 4, 5, 6, 7, 8 });
        try
        {
            var ex = Assert.Throws<MboxPst.Core.Conversion.ConversionException>(() => InventoryScanner.Scan(path, null, CancellationToken.None));
            Assert.StartsWith("Este arquivo não parece um .mbox", ex.Message);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Mbox_vazio_e_recusado_com_mensagem_clara()
    {
        var ex = Assert.Throws<MboxPst.Core.Conversion.ConversionException>(() => InventoryScanner.Scan(Fixture("empty.mbox"), null, CancellationToken.None));
        Assert.Equal("O arquivo não tem nenhum e-mail.", ex.Message);
    }

    [Fact]
    public void Arquivo_sem_marcadores_do_Gmail_tem_entradas_sem_marcador()
    {
        LabelInventory inventory = InventoryScanner.Scan(Fixture("sample.mbox"), null, CancellationToken.None);
        Assert.NotEmpty(inventory.Entries);
        Assert.All(inventory.Entries, e => Assert.Empty(e.Labels));
        Assert.Empty(inventory.CountByLabel);
    }
}
