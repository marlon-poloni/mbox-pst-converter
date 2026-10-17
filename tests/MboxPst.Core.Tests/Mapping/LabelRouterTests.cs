using MboxPst.Core.Mapping;

namespace MboxPst.Core.Tests.Mapping;

public class LabelRouterTests
{
    private const string Fallback = "meu-arquivo";
    private static readonly MappingConfig Base = DefaultMapping.For(DefaultMappingTests.Counts, "Todos os e-mails");

    private static MappingConfig With(params (string Label, LabelDecision Decision)[] changes) => With(Base.CopyPolicy, changes);

    private static MappingConfig With(CopyPolicy policy, params (string Label, LabelDecision Decision)[] changes)
    {
        var decisions = new Dictionary<string, LabelDecision>(Base.Decisions);
        foreach (var (label, decision) in changes)
            decisions[label] = decision;
        return new MappingConfig { Decisions = decisions, CopyPolicy = policy, UserPriority = Base.UserPriority, StoreName = Base.StoreName };
    }

    private static Route For(MappingConfig config, params string[] labels) => LabelRouter.For(labels, config, Fallback);
    private static Route For(params string[] labels) => For(Base, labels);

    private static void AssertFolders(Route route, params string[][] folders)
    {
        Assert.False(route.Excluded);
        Assert.Equal(folders.Length, route.Folders.Count);
        for (int i = 0; i < folders.Length; i++)
            Assert.Equal(folders[i], route.Folders[i]);
    }

    [Fact]
    public void Caixa_de_entrada_vai_para_Caixa_de_Entrada()
    {
        Route route = For("Caixa de entrada", "Aberto");
        AssertFolders(route, new[] { "Caixa de Entrada" });
        Assert.True(route.IsRead);
        Assert.False(route.IsFlagged);
        Assert.False(route.IsForwarded);
        Assert.Empty(route.Categories);
    }

    [Fact]
    public void Nao_lidas_deixa_nao_lido()
    {
        Assert.False(For("Caixa de entrada", "Não lidas").IsRead);
    }

    [Fact]
    public void Unread_em_ingles_deixa_nao_lido()
    {
        Assert.False(For("Inbox", "Unread").IsRead);
    }

    [Fact]
    public void Com_estrela_sinaliza()
    {
        Assert.True(For("Caixa de entrada", "Com estrela").IsFlagged);
    }

    [Fact]
    public void Forwarded_marca_encaminhado()
    {
        Assert.True(For("Enviado", "IMAP_$Forwarded").IsForwarded);
    }

    [Fact]
    public void Sem_marcador_de_pasta_vai_para_Arquivo_Morto()
    {
        AssertFolders(For("Importante", "Categoria: pessoal"), new[] { "Arquivo Morto" });
    }

    [Fact]
    public void Sem_cabecalho_vai_para_pasta_de_fallback()
    {
        AssertFolders(For(), new[] { Fallback });
    }

    [Fact]
    public void Varias_pastas_EachFolder()
    {
        AssertFolders(For("Caixa de entrada", "Fornecedores", "Enviado"),
            new[] { "Caixa de Entrada" }, new[] { "Fornecedores" }, new[] { "Itens Enviados" });
    }

    [Fact]
    public void Varias_pastas_FirstFolderOnly_respeita_prioridade()
    {
        MappingConfig config = With(CopyPolicy.FirstFolderOnly);
        AssertFolders(For(config, "Caixa de entrada", "Notes", "Fornecedores", "Enviado"), new[] { "Fornecedores" });
        AssertFolders(For(config, "Lixeira", "Enviado", "Caixa de entrada"), new[] { "Caixa de Entrada" });
        AssertFolders(For(config, "Lixeira", "Spam"), new[] { "Lixo Eletrônico" });
    }

    [Fact]
    public void FirstFolderOnly_marcador_fora_da_prioridade_vem_depois_dos_listados()
    {
        MappingConfig config = With(CopyPolicy.FirstFolderOnly);
        AssertFolders(For(config, "Novo marcador", "Compras"), new[] { "Compras" });
        AssertFolders(For(config, "Novo marcador", "Caixa de entrada"), new[] { "Novo marcador" });
    }

    [Fact]
    public void Dois_marcadores_para_a_mesma_pasta_nao_duplicam()
    {
        MappingConfig config = With(("Fornecedores", LabelDecision.Folder("Caixa de Entrada")));
        AssertFolders(For(config, "Caixa de entrada", "Fornecedores"), new[] { "Caixa de Entrada" });
    }

    [Fact]
    public void Mesma_pasta_com_caixa_diferente_nao_duplica()
    {
        MappingConfig config = With(("Fornecedores", LabelDecision.Folder("caixa de entrada")));
        AssertFolders(For(config, "Caixa de entrada", "Fornecedores"), new[] { "Caixa de Entrada" });
    }

    [Fact]
    public void IgnoreLabel_em_pasta_cai_nas_outras()
    {
        MappingConfig config = With(("Spam", LabelDecision.Ignore()));
        AssertFolders(For(config, "Spam", "Fornecedores"), new[] { "Fornecedores" });
    }

    [Fact]
    public void IgnoreLabel_sem_outras_pastas_vai_para_Arquivo_Morto()
    {
        MappingConfig config = With(("Spam", LabelDecision.Ignore()));
        AssertFolders(For(config, "Spam"), new[] { "Arquivo Morto" });
    }

    [Fact]
    public void Exclude_tira_o_email_mesmo_com_outros_marcadores()
    {
        MappingConfig config = With(("Lixeira", LabelDecision.Exclude()));
        Route route = For(config, "Lixeira", "Caixa de entrada");
        Assert.True(route.Excluded);
        Assert.Empty(route.Folders);
    }

    [Fact]
    public void Mark_em_marcador_de_usuario_vira_categoria()
    {
        MappingConfig config = With(("Fornecedores", LabelDecision.Mark()));
        Route route = For(config, "Fornecedores");
        Assert.Equal(new[] { "Fornecedores" }, route.Categories);
        AssertFolders(route, new[] { "Arquivo Morto" });
    }

    [Fact]
    public void Mark_em_pasta_padrao_vira_categoria_e_mantem_as_outras_pastas()
    {
        MappingConfig config = With(("Caixa de entrada", LabelDecision.Mark()));
        Route route = For(config, "Caixa de entrada", "Fornecedores");
        Assert.Equal(new[] { "Caixa de entrada" }, route.Categories);
        AssertFolders(route, new[] { "Fornecedores" });
    }

    [Fact]
    public void Folder_em_marcador_de_situacao_cria_pasta()
    {
        MappingConfig config = With(("Importante", LabelDecision.Folder("Importante")));
        AssertFolders(For(config, "Caixa de entrada", "Importante"), new[] { "Caixa de Entrada" }, new[] { "Importante" });
    }

    [Fact]
    public void Nao_lidas_ignorada_deixa_lido()
    {
        MappingConfig config = With(("Não lidas", LabelDecision.Ignore()));
        Assert.True(For(config, "Caixa de entrada", "Não lidas").IsRead);
    }

    [Fact]
    public void Nao_lidas_como_pasta_cria_pasta_e_deixa_lido()
    {
        MappingConfig config = With(("Não lidas", LabelDecision.Folder("Pendentes")));
        Route route = For(config, "Caixa de entrada", "Não lidas");
        Assert.True(route.IsRead);
        AssertFolders(route, new[] { "Caixa de Entrada" }, new[] { "Pendentes" });
    }

    [Fact]
    public void Marcador_desconhecido_sem_decisao_vira_pasta_de_usuario()
    {
        AssertFolders(For("Projeto Novo"), new[] { "Projeto Novo" });
    }

    [Fact]
    public void Desconhecido_com_barra_vira_subpasta()
    {
        AssertFolders(For("Clientes/ACME"), new[] { "Clientes", "ACME" });
    }

    [Fact]
    public void Renomeado_e_movido_para_subpasta()
    {
        MappingConfig config = With(("Fornecedores", LabelDecision.Folder("Clientes", "Fornecedores")));
        AssertFolders(For(config, "Fornecedores"), new[] { "Clientes", "Fornecedores" });
    }

    [Fact]
    public void Categoria_marcada_vira_categoria_do_Outlook()
    {
        MappingConfig config = With(("Categoria: pessoal", LabelDecision.Mark()));
        Assert.Equal(new[] { "Categoria: pessoal" }, For(config, "Caixa de entrada", "Categoria: pessoal").Categories);
    }

    [Fact]
    public void Caminho_vazio_e_ignorado()
    {
        MappingConfig config = With(("Fornecedores", LabelDecision.Folder()));
        AssertFolders(For(config, "Fornecedores"), new[] { "Arquivo Morto" });
        AssertFolders(For("/"), new[] { "Arquivo Morto" });
    }

    [Fact]
    public void FirstFolderOnly_desempate_por_nome_e_ordinal()
    {
        var config = new MappingConfig { CopyPolicy = CopyPolicy.FirstFolderOnly, StoreName = "x" };
        AssertFolders(LabelRouter.For(new[] { "a", "B" }, config, Fallback), new[] { "B" });
        AssertFolders(LabelRouter.For(new[] { "B", "a" }, config, Fallback), new[] { "B" });
    }

    [Fact]
    public void Exclude_com_marcacao_e_nao_lidas_continua_excluido()
    {
        MappingConfig config = With(("Lixeira", LabelDecision.Exclude()), ("Fornecedores", LabelDecision.Mark()));
        Route route = For(config, "Fornecedores", "Não lidas", "Lixeira");
        Assert.True(route.Excluded);
        Assert.Empty(route.Folders);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_recusa_nome_vazio(string name)
    {
        Assert.Equal("Dê um nome para a pasta.", FolderNames.Validate(name));
    }

    [Fact]
    public void Validate_recusa_nome_longo_demais()
    {
        Assert.Equal("O nome da pasta pode ter no máximo 255 caracteres.", FolderNames.Validate(new string('a', 256)));
    }

    [Fact]
    public void Validate_aceita_nome_normal()
    {
        Assert.Null(FolderNames.Validate("Fornecedores"));
        Assert.Null(FolderNames.Validate(new string('a', 255)));
    }
}
