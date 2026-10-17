using MboxPst.Core.Mapping;
using MboxPst.Core.Previewing;
using MboxPst.Core.Scanning;

namespace MboxPst.Core.Tests.Previewing;

public class PreviewBuilderTests
{
    private static LabelInventory Inventory(params (string[] Labels, long Size)[] messages)
    {
        var entries = messages.Select((m, i) => new InventoryEntry(i * 1000, m.Labels, m.Size)).ToList();
        var counts = entries.SelectMany(e => e.Labels).GroupBy(l => l).ToDictionary(g => g.Key, g => g.Count());
        return new LabelInventory { Entries = entries, CountByLabel = counts, SourceBytes = entries.Sum(e => e.SizeBytes) };
    }

    private static MappingConfig Config(LabelInventory inventory, CopyPolicy policy = CopyPolicy.EachFolder, params (string, LabelDecision)[] changes)
    {
        MappingConfig d = DefaultMapping.For(inventory.CountByLabel, "Todos os e-mails");
        var decisions = new Dictionary<string, LabelDecision>(d.Decisions);
        foreach (var (label, decision) in changes)
            decisions[label] = decision;
        return new MappingConfig { Decisions = decisions, CopyPolicy = policy, UserPriority = d.UserPriority, StoreName = d.StoreName };
    }

    private static Preview Build(LabelInventory inventory, MappingConfig config) => PreviewBuilder.Build(inventory, config, "arquivo");

    [Fact]
    public void Arvore_tem_pastas_padrao_primeiro_e_depois_as_do_usuario_em_ordem_alfabetica()
    {
        LabelInventory inv = Inventory(
            (new[] { "Zebra" }, 10), (new[] { "Spam" }, 10), (new[] { "Caixa de entrada" }, 10),
            (new[] { "Ábaco" }, 10), (new[] { "Enviado" }, 10), (new[] { "Clientes/ACME" }, 10), (new[] { "Clientes/Beta" }, 10));

        Preview p = Build(inv, Config(inv));

        Assert.Equal("Todos os e-mails", p.Root.Name);
        Assert.Equal(new[] { "Caixa de Entrada", "Itens Enviados", "Lixo Eletrônico", "Ábaco", "Clientes", "Zebra" },
            p.Root.Children.Select(c => c.Name));
        FolderNode clientes = p.Root.Children.Single(c => c.Name == "Clientes");
        Assert.Equal(0, clientes.Messages);
        Assert.Equal(new[] { "ACME", "Beta" }, clientes.Children.Select(c => c.Name));
        Assert.Equal(new[] { "Clientes", "ACME" }, clientes.Children[0].Path);
        Assert.Equal(8, p.FolderCount);
    }

    [Fact]
    public void Subpasta_de_pasta_padrao_fica_dentro_dela()
    {
        LabelInventory inv = Inventory((new[] { "Caixa de entrada" }, 10), (new[] { "Fornecedores" }, 10));
        Preview p = Build(inv, Config(inv, CopyPolicy.EachFolder, ("Fornecedores", LabelDecision.Folder("Caixa de Entrada", "Fornecedores"))));

        FolderNode inbox = Assert.Single(p.Root.Children);
        Assert.Equal(1, inbox.Messages);
        Assert.Equal("Fornecedores", Assert.Single(inbox.Children).Name);
    }

    [Fact]
    public void Conta_copias_com_EachFolder()
    {
        LabelInventory inv = Inventory(
            (new[] { "Caixa de entrada", "Fornecedores", "Enviado" }, 100),
            (new[] { "Caixa de entrada" }, 100),
            (new[] { "Enviado" }, 100));

        Preview p = Build(inv, Config(inv));

        Assert.Equal(3, p.SourceMessages);
        Assert.Equal(5, p.MessagesInPst);
        Assert.Equal(2, p.ExtraCopies);
        Assert.Equal(1, p.MessagesInMoreThanOneFolder);
        Assert.Equal(2, p.Root.Children.Single(c => c.Name == "Caixa de Entrada").Messages);
    }

    [Fact]
    public void Conta_copias_com_FirstFolderOnly()
    {
        LabelInventory inv = Inventory(
            (new[] { "Caixa de entrada", "Fornecedores", "Enviado" }, 100),
            (new[] { "Caixa de entrada" }, 100),
            (new[] { "Enviado" }, 100));

        Preview p = Build(inv, Config(inv, CopyPolicy.FirstFolderOnly));

        Assert.Equal(3, p.MessagesInPst);
        Assert.Equal(0, p.ExtraCopies);
        Assert.Equal(0, p.MessagesInMoreThanOneFolder);
    }

    [Fact]
    public void Conta_excluidos()
    {
        LabelInventory inv = Inventory((new[] { "Lixeira" }, 10), (new[] { "Lixeira", "Caixa de entrada" }, 10), (new[] { "Caixa de entrada" }, 10));
        Preview p = Build(inv, Config(inv, CopyPolicy.EachFolder, ("Lixeira", LabelDecision.Exclude())));

        Assert.Equal(2, p.Excluded);
        Assert.Equal(1, p.MessagesInPst);
        Assert.DoesNotContain(p.Root.Children, c => c.Name == "Itens Excluídos");
    }

    [Fact]
    public void Tamanho_estimado_soma_copias_com_fator_medido()
    {
        LabelInventory inv = Inventory((new[] { "Caixa de entrada", "Enviado" }, 1000), (new[] { "Spam" }, 400));
        Preview p = Build(inv, Config(inv));
        Assert.Equal(1800, p.EstimatedBytes);
    }

    [Fact]
    public void Pasta_sem_email_nao_aparece()
    {
        LabelInventory inv = Inventory((new[] { "Caixa de entrada" }, 10));
        Preview p = Build(inv, Config(inv));
        Assert.Equal(new[] { "Caixa de Entrada" }, p.Root.Children.Select(c => c.Name));
        Assert.Equal(1, p.FolderCount);
    }

    [Fact]
    public void Arquivo_sem_marcadores_vai_para_pasta_de_fallback()
    {
        LabelInventory inv = Inventory((Array.Empty<string>(), 10), (Array.Empty<string>(), 10));
        Preview p = Build(inv, Config(inv));
        FolderNode only = Assert.Single(p.Root.Children);
        Assert.Equal("arquivo", only.Name);
        Assert.Equal(2, only.Messages);
    }

    [Fact]
    public void Pastas_com_caixa_diferente_viram_uma_so()
    {
        LabelInventory inv = Inventory((new[] { "Clientes" }, 10), (new[] { "clientes" }, 10));
        Preview p = Build(inv, Config(inv));
        FolderNode only = Assert.Single(p.Root.Children);
        Assert.Equal(2, only.Messages);
    }

    [Fact]
    public void Contagens_batem_com_o_arquivo_de_teste()
    {
        var messages = new List<(string[], long)>();
        for (int i = 0; i < 316; i++) messages.Add((new[] { "Caixa de entrada", "Fornecedores" }, 1000));
        for (int i = 0; i < 12; i++) messages.Add((new[] { "Caixa de entrada", "Fornecedores", "Enviado" }, 1000));
        for (int i = 0; i < 6614 - 328; i++) messages.Add((new[] { "Caixa de entrada", "Aberto" }, 1000));
        LabelInventory inv = Inventory(messages.ToArray());

        Preview p = Build(inv, Config(inv));

        Assert.Equal(6614, p.SourceMessages);
        Assert.Equal(328, p.MessagesInMoreThanOneFolder);
        Assert.Equal(340, p.ExtraCopies);
        Assert.Equal(6954, p.MessagesInPst);
    }
}
