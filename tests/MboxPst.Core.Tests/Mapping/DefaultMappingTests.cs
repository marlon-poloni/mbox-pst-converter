using MboxPst.Core.Mapping;

namespace MboxPst.Core.Tests.Mapping;

public class DefaultMappingTests
{
    internal static readonly Dictionary<string, int> Counts = new()
    {
        ["Caixa de entrada"] = 5773,
        ["Aberto"] = 4075,
        ["Categoria: pessoal"] = 3382,
        ["Importante"] = 2747,
        ["Categoria: atualizações"] = 2141,
        ["Não lidas"] = 1068,
        ["Enviado"] = 703,
        ["Categoria: promoções"] = 337,
        ["Arquivada"] = 163,
        ["Faturas"] = 124,
        ["Spam"] = 76,
        ["Categoria: compras"] = 64,
        ["Fornecedores"] = 46,
        ["Rascunhos"] = 24,
        ["Processos 2025"] = 17,
        ["IMAP_$Forwarded"] = 14,
        ["Notes"] = 12,
        ["Categoria: viagens"] = 11,
        ["Categoria: fóruns"] = 10,
        ["Categoria: redes sociais"] = 8,
        ["Lixeira"] = 7,
        ["Com estrela"] = 6,
        ["Marketing"] = 6,
        ["Compras"] = 3,
    };

    private static readonly MappingConfig Config = DefaultMapping.For(Counts, "Todos os e-mails");

    private static void AssertFolder(string label, params string[] path)
    {
        LabelDecision d = Config.Decisions[label];
        Assert.Equal(DecisionKind.Folder, d.Kind);
        Assert.Equal(path, d.FolderPath);
    }

    [Fact]
    public void Pastas_padrao_vao_para_as_equivalentes_do_Outlook()
    {
        AssertFolder("Caixa de entrada", "Caixa de Entrada");
        AssertFolder("Enviado", "Itens Enviados");
        AssertFolder("Rascunhos", "Rascunhos");
        AssertFolder("Arquivada", "Arquivo Morto");
        AssertFolder("Spam", "Lixo Eletrônico");
        AssertFolder("Lixeira", "Itens Excluídos");
    }

    [Fact]
    public void Marcadores_do_usuario_viram_pasta_com_o_mesmo_nome()
    {
        AssertFolder("Faturas", "Faturas");
        AssertFolder("Fornecedores", "Fornecedores");
        AssertFolder("Processos 2025", "Processos 2025");
    }

    [Fact]
    public void Barra_vira_subpasta()
    {
        MappingConfig config = DefaultMapping.For(new Dictionary<string, int> { ["Clientes/ACME"] = 2 }, "x");
        Assert.Equal(new[] { "Clientes", "ACME" }, config.Decisions["Clientes/ACME"].FolderPath);
    }

    [Theory]
    [InlineData("Categoria: pessoal")]
    [InlineData("Categoria: redes sociais")]
    [InlineData("Aberto")]
    [InlineData("Importante")]
    public void Categorias_e_situacoes_sem_efeito_sao_ignoradas(string label)
    {
        Assert.Equal(DecisionKind.IgnoreLabel, Config.Decisions[label].Kind);
        Assert.Empty(Config.Decisions[label].FolderPath);
    }

    [Theory]
    [InlineData("Não lidas")]
    [InlineData("Com estrela")]
    [InlineData("IMAP_$Forwarded")]
    public void Situacoes_viram_marcacao(string label)
    {
        Assert.Equal(DecisionKind.Mark, Config.Decisions[label].Kind);
    }

    [Fact]
    public void Spam_e_Lixeira_comecam_como_pasta()
    {
        Assert.Equal(DecisionKind.Folder, Config.Decisions["Spam"].Kind);
        Assert.Equal(DecisionKind.Folder, Config.Decisions["Lixeira"].Kind);
    }

    [Fact]
    public void Copia_em_cada_pasta_por_padrao()
    {
        Assert.Equal(CopyPolicy.EachFolder, Config.CopyPolicy);
        Assert.Equal("Todos os e-mails", Config.StoreName);
    }

    [Fact]
    public void Prioridade_dos_marcadores_do_usuario_por_quantidade()
    {
        Assert.Equal(new[] { "Faturas", "Fornecedores", "Processos 2025", "Notes", "Marketing", "Compras" }, Config.UserPriority);
    }

    [Fact]
    public void Empate_na_prioridade_desempata_por_nome()
    {
        MappingConfig config = DefaultMapping.For(new Dictionary<string, int> { ["Zeta"] = 5, ["Alfa"] = 5 }, "x");
        Assert.Equal(new[] { "Alfa", "Zeta" }, config.UserPriority);
    }
}
