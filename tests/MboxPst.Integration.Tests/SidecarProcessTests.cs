using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace MboxPst.Integration.Tests;

public class SidecarProcessTests
{
    [Fact]
    public void Processo_real_le_o_arquivo_responde_erro_e_sai_quando_a_entrada_fecha()
    {
        var psi = new ProcessStartInfo("dotnet", new[] { Path.Combine(AppContext.BaseDirectory, "mboxpst-core.dll") })
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            StandardInputEncoding = new UTF8Encoding(false),
            StandardOutputEncoding = Encoding.UTF8,
        };
        using Process process = Process.Start(psi)!;

        process.StandardInput.WriteLine($$"""{"cmd":"scan","path":{{JsonSerializer.Serialize(PipelineTests.Fixture("gmail-pt.mbox"))}}}""");
        JsonElement done = ReadUntil(process, "scanDone");
        Assert.Equal(12, done.GetProperty("messages").GetInt32());
        Assert.Contains(done.GetProperty("labels").EnumerateArray(), l => l.GetProperty("name").GetString() == "Não lidas");

        process.StandardInput.WriteLine("{");
        Assert.Equal("Comando inválido.", ReadUntil(process, "error").GetProperty("message").GetString());

        process.StandardInput.Close();
        Assert.True(process.WaitForExit(10_000));
        Assert.Equal(0, process.ExitCode);
    }

    private static JsonElement ReadUntil(Process process, string eventName)
    {
        while (process.StandardOutput.ReadLine() is { } line)
        {
            JsonElement e = JsonDocument.Parse(line).RootElement;
            if (e.GetProperty("event").GetString() == eventName)
                return e;
        }
        throw new InvalidOperationException($"o processo terminou antes de '{eventName}'");
    }
}
