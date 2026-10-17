using MboxPst.Core.Scanning;
using MboxPst.Generator;

namespace MboxPst.Integration.Tests;

public sealed class GeneratorTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "mboxpst-gen-" + Guid.NewGuid());

    public GeneratorTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, true);

    [Fact]
    public void Gera_o_tamanho_pedido_e_o_scanner_le_tudo()
    {
        string path = Path.Combine(_dir, "g.mbox");
        GeneratedMbox generated = MboxGenerator.Generate(path, targetBytes: 8 * 1024 * 1024, seed: 7);

        long size = new FileInfo(path).Length;
        Assert.InRange(size, 8 * 1024 * 1024, 8 * 1024 * 1024 + 26 * 1024 * 1024 * 4 / 3 + 64 * 1024);
        LabelInventory inventory = InventoryScanner.Scan(path, null, CancellationToken.None);
        Assert.Equal(generated.Messages.Count, inventory.Entries.Count);
        Assert.Equal(generated.Messages.Select(m => m.Labels), inventory.Entries.Select(e => e.Labels));
    }

    [Fact]
    public void Mesma_semente_gera_o_mesmo_arquivo()
    {
        string a = Path.Combine(_dir, "a.mbox");
        string b = Path.Combine(_dir, "b.mbox");
        MboxGenerator.Generate(a, 2 * 1024 * 1024, seed: 3);
        MboxGenerator.Generate(b, 2 * 1024 * 1024, seed: 3);
        Assert.Equal(File.ReadAllBytes(a), File.ReadAllBytes(b));
    }

    [Fact]
    public void Tem_anexos_varias_pastas_e_cabecalho_dobrado()
    {
        string path = Path.Combine(_dir, "c.mbox");
        GeneratedMbox generated = MboxGenerator.Generate(path, 16 * 1024 * 1024, seed: 11);

        Assert.Contains(generated.Messages, m => m.AttachmentBytes > 0);
        Assert.Contains(generated.Messages, m => m.Labels.Count(l => l is "Caixa de entrada" or "Enviado" or "Arquivada" or "Spam" or "Lixeira" or "Fornecedores" or "Clientes/ACME") > 1);
        string text = File.ReadAllText(path);
        Assert.Contains("X-Gmail-Labels: =?UTF-8?Q?", text);
        Assert.Contains("\n =?UTF-8?Q?", text);
    }
}
