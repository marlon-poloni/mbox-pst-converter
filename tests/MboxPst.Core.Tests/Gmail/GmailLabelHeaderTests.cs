using System.Text;
using MboxPst.Core.Gmail;

namespace MboxPst.Core.Tests.Gmail;

public class GmailLabelHeaderTests
{
    private static IReadOnlyList<string> Parse(string raw) => GmailLabelHeader.Parse(Encoding.UTF8.GetBytes(raw));

    [Fact]
    public void Nulo_e_vazio_viram_lista_vazia()
    {
        Assert.Empty(GmailLabelHeader.Parse(null));
        Assert.Empty(GmailLabelHeader.Parse(Array.Empty<byte>()));
    }

    [Fact]
    public void Separa_por_virgula()
    {
        Assert.Equal(new[] { "Caixa de entrada", "Importante", "Aberto" }, Parse(" Caixa de entrada,Importante,Aberto\r\n"));
    }

    [Fact]
    public void Desfaz_quebra_de_linha()
    {
        Assert.Equal(new[] { "Caixa de entrada", "Enviado" }, Parse(" Caixa de\r\n entrada,Enviado"));
    }

    [Fact]
    public void Decodifica_rfc2047_quebrado_no_meio()
    {
        string raw = " =?UTF-8?Q?Caixa_de_entrada,Categoria:_atualiza=C3=A7=C3=B5es?=\r\n =?UTF-8?Q?,N=C3=A3o_lidas?=";
        Assert.Equal(new[] { "Caixa de entrada", "Categoria: atualizações", "Não lidas" }, Parse(raw));
    }

    [Fact]
    public void Aceita_utf8_cru()
    {
        Assert.Equal(new[] { "Lixeira", "Não lidas" }, Parse(" Lixeira,Não lidas"));
    }

    [Fact]
    public void Respeita_virgula_entre_aspas()
    {
        Assert.Equal(new[] { "Clientes, ACME", "Enviado" }, Parse(" \"Clientes, ACME\",Enviado"));
    }

    [Fact]
    public void Remove_repetidos_e_espacos_nas_pontas()
    {
        Assert.Equal(new[] { "Spam", "Notes" }, Parse(" Spam , Spam,  Notes  "));
    }

    [Fact]
    public void Colapsa_espacos_internos()
    {
        Assert.Equal(new[] { "Processos 2025" }, Parse(" Processos   2025"));
    }

    [Fact]
    public void Decodifica_dobra_dentro_de_uma_palavra_codificada()
    {
        Assert.Equal(new[] { "Caixa de entrada", "Não lidas" }, Parse(" =?UTF-8?Q?Caixa_de_entrada,N=C3=A3o_li\r\n das?="));
    }

    [Fact]
    public void Aspa_no_meio_do_nome_nao_engole_os_seguintes()
    {
        Assert.Equal(new[] { "Pizza 12\" grande", "Caixa de entrada", "Não lidas" }, Parse(" Pizza 12\" grande,Caixa de entrada,Não lidas"));
    }

    [Fact]
    public void Aspa_nao_fechada_separa_por_virgula()
    {
        Assert.Equal(new[] { "Clientes", "ACME", "Enviado", "Não lidas" }, Parse(" \"Clientes, ACME,Enviado,Não lidas"));
    }

    [Fact]
    public void Aspa_escapada_dentro_de_nome_entre_aspas()
    {
        Assert.Equal(new[] { "Diga \"oi\", ok", "Enviado" }, Parse(" \"Diga \\\"oi\\\", ok\",Enviado"));
    }

    [Fact]
    public void Windows_1252_cru_no_nome_e_corrigido()
    {
        byte[] raw = { (byte)' ', (byte)'A', 0x96, (byte)'B', (byte)',', (byte)'N', 0xE3, (byte)'o', (byte)' ', (byte)'l', (byte)'i', (byte)'d', (byte)'a', (byte)'s' };
        Assert.Equal(new[] { "A–B", "Não lidas" }, GmailLabelHeader.Parse(raw));
    }
}
