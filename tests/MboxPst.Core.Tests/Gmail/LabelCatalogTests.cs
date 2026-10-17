using MboxPst.Core.Gmail;

namespace MboxPst.Core.Tests.Gmail;

public class LabelCatalogTests
{
    [Theory]
    [InlineData("Caixa de entrada", LabelKind.Inbox)]
    [InlineData("Inbox", LabelKind.Inbox)]
    [InlineData("Enviado", LabelKind.Sent)]
    [InlineData("Enviados", LabelKind.Sent)]
    [InlineData("Sent", LabelKind.Sent)]
    [InlineData("Rascunhos", LabelKind.Drafts)]
    [InlineData("Drafts", LabelKind.Drafts)]
    [InlineData("Arquivada", LabelKind.Archived)]
    [InlineData("Archived", LabelKind.Archived)]
    [InlineData("Spam", LabelKind.Spam)]
    [InlineData("Lixeira", LabelKind.Trash)]
    [InlineData("Trash", LabelKind.Trash)]
    [InlineData("Categoria: pessoal", LabelKind.Category)]
    [InlineData("Categoria: atualizações", LabelKind.Category)]
    [InlineData("Category Personal", LabelKind.Category)]
    [InlineData("Category Updates", LabelKind.Category)]
    [InlineData("Category Promotions", LabelKind.Category)]
    [InlineData("Category Social", LabelKind.Category)]
    [InlineData("Category Forums", LabelKind.Category)]
    [InlineData("Category Purchases", LabelKind.Category)]
    [InlineData("Category Travel", LabelKind.Category)]
    [InlineData("Categoria: promoções", LabelKind.Category)]
    [InlineData("Categoria: redes sociais", LabelKind.Category)]
    [InlineData("Categoria: fóruns", LabelKind.Category)]
    [InlineData("Categoria: compras", LabelKind.Category)]
    [InlineData("Categoria: viagens", LabelKind.Category)]
    [InlineData("Categoria:  Pessoal ", LabelKind.Category)]
    [InlineData("Não lidas", LabelKind.Unread)]
    [InlineData("Unread", LabelKind.Unread)]
    [InlineData("Com estrela", LabelKind.Starred)]
    [InlineData("Starred", LabelKind.Starred)]
    [InlineData("IMAP_$Forwarded", LabelKind.Forwarded)]
    [InlineData("$Forwarded", LabelKind.Forwarded)]
    [InlineData("Aberto", LabelKind.Opened)]
    [InlineData("Opened", LabelKind.Opened)]
    [InlineData("Importante", LabelKind.Important)]
    [InlineData("Important", LabelKind.Important)]
    [InlineData("Fornecedores", LabelKind.User)]
    [InlineData("Notes", LabelKind.User)]
    [InlineData("Clientes/ACME", LabelKind.User)]
    [InlineData("Categorias de faturas", LabelKind.User)]
    public void Classify_reconhece_nomes(string label, LabelKind expected)
    {
        Assert.Equal(expected, LabelCatalog.Classify(label));
    }

    [Fact]
    public void Classify_ignora_caixa_e_espacos_nas_pontas()
    {
        Assert.Equal(LabelKind.Inbox, LabelCatalog.Classify("  caixa DE entrada "));
    }

    [Theory]
    [InlineData("Categorias de faturas")]
    [InlineData("Inboxes")]
    [InlineData("Categoria")]
    [InlineData("Category")]
    [InlineData("Category Theory")]
    [InlineData("Category A clients")]
    [InlineData("Categoria: clientes VIP")]
    public void Classify_nao_confunde_prefixo(string label)
    {
        Assert.Equal(LabelKind.User, LabelCatalog.Classify(label));
    }
}
