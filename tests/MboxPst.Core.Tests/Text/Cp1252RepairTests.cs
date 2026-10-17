using MboxPst.Core.Text;

namespace MboxPst.Core.Tests.Text;

public class Cp1252RepairTests
{
    [Theory]
    [InlineData("Contribuinte \u0096 IND", "Contribuinte – IND")]
    [InlineData("\u0093aspas\u0094", "“aspas”")]
    [InlineData("\u0080 10", "€ 10")]
    [InlineData("fim\u0085", "fim…")]
    public void Troca_caracteres_de_controle_pelos_do_Windows_1252(string input, string expected)
    {
        Assert.Equal(expected, Cp1252Repair.Fix(input));
    }

    [Fact]
    public void Texto_sem_a_faixa_volta_a_mesma_instancia()
    {
        string text = "Lançamento — ok";
        Assert.Same(text, Cp1252Repair.Fix(text));
    }

    [Fact]
    public void Nulo_volta_nulo()
    {
        Assert.Null(Cp1252Repair.Fix(null));
    }

    [Theory]
    [InlineData("\u0081")]
    [InlineData("\u008D")]
    [InlineData("\u008F")]
    [InlineData("\u0090")]
    [InlineData("\u009D")]
    public void Codigos_indefinidos_ficam_como_estao(string input)
    {
        Assert.Equal(input, Cp1252Repair.Fix(input));
    }
}
