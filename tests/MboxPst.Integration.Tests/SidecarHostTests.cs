using System.Diagnostics;
using System.IO.Pipes;
using System.Text.Json;
using Mail2Pst.Core.Models;
using Mail2Pst.Core.Writing;
using MboxPst.Sidecar;
using PSTFileFormat;

namespace MboxPst.Integration.Tests;

public sealed class SidecarHostTests : IDisposable
{
    private readonly string _out = Path.Combine(Path.GetTempPath(), "mboxpst-host-" + Guid.NewGuid());
    private readonly AnonymousPipeServerStream _toHost = new(PipeDirection.Out);
    private readonly AnonymousPipeServerStream _fromHost = new(PipeDirection.In);
    private readonly StreamWriter _send;
    private readonly StreamReader _receive;
    private readonly Task _host;

    public SidecarHostTests()
    {
        Directory.CreateDirectory(_out);
        var hostIn = new StreamReader(new AnonymousPipeClientStream(PipeDirection.In, _toHost.ClientSafePipeHandle));
        var hostOut = new StreamWriter(new AnonymousPipeClientStream(PipeDirection.Out, _fromHost.ClientSafePipeHandle));
        _host = Task.Run(() => new SidecarHost(hostIn, hostOut) { WriterFactory = () => new DelayWriter(this) }.Run());
        _send = new StreamWriter(_toHost) { AutoFlush = true };
        _receive = new StreamReader(_fromHost);
    }

    private int _delayMs;
    private readonly ManualResetEventSlim _firstWrite = new();

    public void Dispose()
    {
        _send.Dispose();
        _host.Wait(TimeSpan.FromSeconds(10));
        if (Directory.Exists(_out)) Directory.Delete(_out, true);
    }

    private void Send(string json) => _send.WriteLine(json);

    private JsonElement Next(string eventName)
    {
        var deadline = Stopwatch.StartNew();
        while (true)
        {
            Task<string?> read = _receive.ReadLineAsync();
            TimeSpan left = TimeSpan.FromSeconds(20) - deadline.Elapsed;
            if (left <= TimeSpan.Zero || !read.Wait(left))
                throw new TimeoutException($"'{eventName}' não chegou");
            string? line = read.Result;
            Assert.NotNull(line);
            JsonElement e = JsonDocument.Parse(line).RootElement;
            string name = e.GetProperty("event").GetString()!;
            if (name == eventName) return e;
            if (name == "error" && eventName != "error") Assert.Fail(e.GetProperty("message").GetString());
            if (name is "convertDone" or "scanDone" or "cancelled" or "preview") Assert.Fail($"chegou '{name}' quando se esperava '{eventName}'");
        }
    }

    private string Fixture => JsonSerializer.Serialize(PipelineTests.Fixture("gmail-pt.mbox"));

    private JsonElement Scan()
    {
        Send($$"""{"cmd":"scan","path":{{Fixture}}}""");
        return Next("scanDone");
    }

    [Fact]
    public void Scan_devolve_marcadores_mapeamento_e_previa()
    {
        JsonElement done = Scan();

        Assert.Equal(12, done.GetProperty("messages").GetInt32());
        Assert.Equal("gmail-pt.mbox", done.GetProperty("fileName").GetString());
        Assert.Equal(15, done.GetProperty("labels").GetArrayLength());
        Assert.Equal("folder", done.GetProperty("mapping").GetProperty("decisions").GetProperty("Spam").GetProperty("kind").GetString());
        Assert.Equal(15, done.GetProperty("preview").GetProperty("messagesInPst").GetInt32());
        Assert.Equal("Todos os e-mails", done.GetProperty("preview").GetProperty("root").GetProperty("name").GetString());
    }

    [Fact]
    public void Previa_antes_do_scan_da_erro()
    {
        Send("""{"cmd":"preview","mapping":{"storeName":"x","copyPolicy":"eachFolder","decisions":{}}}""");
        Assert.Equal("Escolha e leia um arquivo antes.", Next("error").GetProperty("message").GetString());
    }

    [Fact]
    public void Previa_reflete_a_mudanca_no_de_para()
    {
        JsonElement mapping = Scan().GetProperty("mapping");
        string changed = mapping.GetRawText().Replace("\"Lixeira\":{\"kind\":\"folder\",\"folderPath\":[\"Itens Excluídos\"]}", "\"Lixeira\":{\"kind\":\"exclude\",\"folderPath\":[]}");
        Assert.NotEqual(mapping.GetRawText(), changed);

        Send($$"""{"cmd":"preview","mapping":{{changed}}}""");
        Assert.Equal(1, Next("preview").GetProperty("preview").GetProperty("excluded").GetInt32());
    }

    [Fact]
    public void Converte_e_devolve_verificacao()
    {
        JsonElement mapping = Scan().GetProperty("mapping");
        Send($$"""{"cmd":"convert","outputDirectory":{{JsonSerializer.Serialize(_out)}},"outputName":"teste","maxPartMB":20480,"mapping":{{mapping.GetRawText()}}}""");

        JsonElement done = Next("convertDone");
        Assert.True(done.GetProperty("verified").GetBoolean());
        Assert.Equal(12, done.GetProperty("converted").GetInt32());
        string file = done.GetProperty("outputFiles")[0].GetString()!;
        Assert.True(File.Exists(file));
        Assert.Equal(new FileInfo(file).Length, done.GetProperty("outputSizes")[0].GetInt64());
    }

    [Fact]
    public void Cancelar_durante_a_conversao_mantem_o_processo_vivo()
    {
        JsonElement mapping = Scan().GetProperty("mapping");
        _delayMs = 300;
        try
        {
            Send($$"""{"cmd":"convert","outputDirectory":{{JsonSerializer.Serialize(_out)}},"outputName":"teste","maxPartMB":20480,"mapping":{{mapping.GetRawText()}}}""");
            Assert.True(_firstWrite.Wait(TimeSpan.FromSeconds(20)), "nenhum e-mail começou a ser gravado");
            Send("""{"cmd":"cancel"}""");
            Next("cancelled");
        }
        finally { _delayMs = 0; }

        Assert.Empty(Directory.GetFiles(_out, "*.pst", SearchOption.AllDirectories));
        Assert.Empty(Directory.GetDirectories(_out));
        Send($$"""{"cmd":"preview","mapping":{{mapping.GetRawText()}}}""");
        Next("preview");
    }

    [Fact]
    public void Comando_durante_operacao_em_andamento_da_erro()
    {
        JsonElement mapping = Scan().GetProperty("mapping");
        _delayMs = 300;
        try
        {
            Send($$"""{"cmd":"convert","outputDirectory":{{JsonSerializer.Serialize(_out)}},"outputName":"teste","maxPartMB":20480,"mapping":{{mapping.GetRawText()}}}""");
            Send($$"""{"cmd":"scan","path":{{Fixture}}}""");
            Assert.Equal("Aguarde a operação em andamento terminar.", Next("error").GetProperty("message").GetString());
            Send("""{"cmd":"cancel"}""");
            Next("cancelled");
        }
        finally { _delayMs = 0; }
    }

    [Fact]
    public void Comando_invalido_da_erro_e_segue_vivo()
    {
        Send("isso não é json");
        Assert.Equal("Comando inválido.", Next("error").GetProperty("message").GetString());
        Scan();
    }

    [Fact]
    public void Converter_depois_de_uma_leitura_que_falhou_nao_usa_o_arquivo_anterior()
    {
        JsonElement mapping = Scan().GetProperty("mapping");
        Send("""{"cmd":"scan","path":"/nao/existe.mbox"}""");
        Next("error");
        Send($$"""{"cmd":"convert","outputDirectory":{{JsonSerializer.Serialize(_out)}},"outputName":"teste","maxPartMB":20480,"mapping":{{mapping.GetRawText()}}}""");
        Assert.Equal("Escolha e leia um arquivo antes.", Next("error").GetProperty("message").GetString());
    }

    [Fact]
    public void Arquivo_inexistente_da_erro_claro()
    {
        Send("""{"cmd":"scan","path":"/nao/existe.mbox"}""");
        Assert.Equal("Arquivo não encontrado: /nao/existe.mbox", Next("error").GetProperty("message").GetString());
    }

    private sealed class DelayWriter(SidecarHostTests test) : PstWriter
    {
        internal override void WriteMessageCore(PSTFile file, PSTFolder folder, MailMessage message)
        {
            Thread.Sleep(test._delayMs);
            base.WriteMessageCore(file, folder, message);
            test._firstWrite.Set();
        }
    }
}
