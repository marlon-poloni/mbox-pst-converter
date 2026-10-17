using System.Text.Json;
using MboxPst.Core.Mapping;
using MboxPst.Sidecar.Protocol;

namespace MboxPst.Core.Tests.Sidecar;

public class ProtocolTests
{
    [Fact]
    public void Le_comando_scan()
    {
        var command = Assert.IsType<ScanCommand>(Wire.ParseCommand("""{"cmd":"scan","path":"/tmp/a.mbox"}"""));
        Assert.Equal("/tmp/a.mbox", command.Path);
    }

    [Fact]
    public void Le_comando_convert_com_mapeamento()
    {
        string line = """
            {"cmd":"convert","outputDirectory":"/out","outputName":"Todos","maxPartMB":1024,
             "mapping":{"storeName":"Todos os e-mails","copyPolicy":"firstFolderOnly","userPriority":["Fornecedores"],
                        "decisions":{"Spam":{"kind":"exclude","folderPath":[]},"Fornecedores":{"kind":"folder","folderPath":["Clientes","Fornecedores"]}}}}
            """;
        var command = Assert.IsType<ConvertCommand>(Wire.ParseCommand(line.ReplaceLineEndings(" ")));

        Assert.Equal("/out", command.OutputDirectory);
        Assert.Equal("Todos", command.OutputName);
        Assert.Equal(1024, command.MaxPartMB);
        MappingConfig config = command.Mapping.ToConfig();
        Assert.Equal("Todos os e-mails", config.StoreName);
        Assert.Equal(CopyPolicy.FirstFolderOnly, config.CopyPolicy);
        Assert.Equal(new[] { "Fornecedores" }, config.UserPriority);
        Assert.Equal(DecisionKind.Exclude, config.Decisions["Spam"].Kind);
        Assert.Equal(new[] { "Clientes", "Fornecedores" }, config.Decisions["Fornecedores"].FolderPath);
    }

    [Fact]
    public void Le_preview_e_cancel()
    {
        Assert.IsType<PreviewCommand>(Wire.ParseCommand("""{"cmd":"preview","mapping":{"storeName":"x","copyPolicy":"eachFolder","decisions":{}}}"""));
        Assert.IsType<CancelCommand>(Wire.ParseCommand("""{"cmd":"cancel"}"""));
    }

    [Theory]
    [InlineData("não é json")]
    [InlineData("""{"cmd":"voar"}""")]
    [InlineData("""{"path":"/a"}""")]
    [InlineData("""{"cmd":"scan"}""")]
    [InlineData("""{"cmd":"scan","path":"relativo.mbox"}""")]
    [InlineData("""{"cmd":"convert","outputDirectory":"/o","outputName":"x","maxPartMB":0,"mapping":{"storeName":"x","copyPolicy":"eachFolder","decisions":{}}}""")]
    [InlineData("""{"cmd":"convert","outputDirectory":"/o","outputName":"x","maxPartMB":-5,"mapping":{"storeName":"x","copyPolicy":"eachFolder","decisions":{}}}""")]
    [InlineData("""{"cmd":"convert","outputDirectory":"/o","outputName":"x","maxPartMB":9007199254740,"mapping":{"storeName":"x","copyPolicy":"eachFolder","decisions":{}}}""")]
    [InlineData("""{"cmd":"convert","outputDirectory":"/o","outputName":"x","mapping":{"storeName":"x","copyPolicy":"eachFolder","decisions":{}}}""")]
    [InlineData("""{"cmd":"convert","outputDirectory":"rel","outputName":"x","maxPartMB":10,"mapping":{"storeName":"x","copyPolicy":"eachFolder","decisions":{}}}""")]
    [InlineData("""{"cmd":"preview","mapping":{"storeName":"x","copyPolicy":"eachFolder","decisions":{"Spam":null}}}""")]
    [InlineData("""{"cmd":"preview","mapping":{"storeName":"x","copyPolicy":"eachFolder","decisions":{"Spam":{"kind":"voar","folderPath":[]}}}}""")]
    [InlineData("""{"cmd":"preview","mapping":{"storeName":"x","copyPolicy":"qualquer","decisions":{}}}""")]
    [InlineData("""{"cmd":"preview","mapping":{"storeName":"x","copyPolicy":"eachFolder","decisions":{"A":{"kind":"folder","folderPath":[""]}}}}""")]
    [InlineData("""{"cmd":"preview","mapping":{"storeName":"x","copyPolicy":"eachFolder","decisions":{"A":{"kind":"folder","folderPath":[null]}}}}""")]
    [InlineData("""{"cmd":"preview","mapping":{"storeName":"x","copyPolicy":"eachFolder","decisions":{"A":{"kind":"folder","folderPath":[]}}}}""")]
    [InlineData("""{"cmd":"preview","mapping":{"copyPolicy":"eachFolder","decisions":{}}}""")]
    public void Comando_invalido_vira_nulo(string line)
    {
        Assert.Null(Wire.ParseCommand(line));
    }

    [Fact]
    public void Mapeamento_vai_e_volta()
    {
        var config = new MappingConfig
        {
            StoreName = "Todos",
            CopyPolicy = CopyPolicy.EachFolder,
            UserPriority = new[] { "A" },
            Decisions = new Dictionary<string, LabelDecision> { ["A"] = LabelDecision.Folder("X", "A"), ["Não lidas"] = LabelDecision.Mark() },
        };

        MappingConfig back = MappingDto.From(config).ToConfig();

        Assert.Equal(config.StoreName, back.StoreName);
        Assert.Equal(config.UserPriority, back.UserPriority);
        Assert.Equal(new[] { "X", "A" }, back.Decisions["A"].FolderPath);
        Assert.Equal(DecisionKind.Mark, back.Decisions["Não lidas"].Kind);
    }

    [Fact]
    public void Evento_tem_campo_event_e_nomes_em_camelCase()
    {
        string line = Wire.Serialize(new ErrorEvent("Algo deu errado"));
        using JsonDocument doc = JsonDocument.Parse(line);
        Assert.Equal("error", doc.RootElement.GetProperty("event").GetString());
        Assert.Equal("Algo deu errado", doc.RootElement.GetProperty("message").GetString());
        Assert.DoesNotContain('\n', line);
    }

    [Fact]
    public void Evento_de_progresso_usa_segundos()
    {
        string line = Wire.Serialize(new ConvertProgressEvent(5, 12, 100, 200, 1.5, 3.25));
        using JsonDocument doc = JsonDocument.Parse(line);
        Assert.Equal("convertProgress", doc.RootElement.GetProperty("event").GetString());
        Assert.Equal(3.25, doc.RootElement.GetProperty("remainingSeconds").GetDouble());
        Assert.Equal(12, doc.RootElement.GetProperty("total").GetInt32());
    }
}
