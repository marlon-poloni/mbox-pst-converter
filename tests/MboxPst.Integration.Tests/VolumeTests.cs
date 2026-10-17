using System.Diagnostics;
using System.Text.Json;
using MboxPst.Core.Conversion;
using MboxPst.Core.Mapping;
using MboxPst.Core.Scanning;
using MboxPst.Generator;

namespace MboxPst.Integration.Tests;

[Trait("Category", "Volume")]
public sealed class VolumeTests : IDisposable
{
    private readonly string _dir = Path.Combine(Environment.GetEnvironmentVariable("MBOXPST_VOLUME_DIR") ?? Path.GetTempPath(), "mboxpst-volume-" + Guid.NewGuid());

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }

    [SkippableFact]
    public void Seis_gigas_em_menos_de_dois_minutos_e_menos_de_tres_gigas_de_memoria()
    {
        Skip.IfNot(Environment.GetEnvironmentVariable("MBOXPST_VOLUME") == "1", "defina MBOXPST_VOLUME=1 para rodar o teste de volume");
        Directory.CreateDirectory(_dir);
        string mbox = Path.Combine(_dir, "volume.mbox");
        GeneratedMbox generated = MboxGenerator.Generate(mbox, 6L * 1024 * 1024 * 1024, seed: 2026);

        long peak = 0;
        using var sampler = new Timer(_ => peak = Math.Max(peak, Process.GetCurrentProcess().WorkingSet64), null, 0, 250);
        var clock = Stopwatch.StartNew();
        LabelInventory inventory = InventoryScanner.Scan(mbox, null, CancellationToken.None);
        MappingConfig mapping = DefaultMapping.For(inventory.CountByLabel, "Todos os e-mails");
        ConversionResult result = ConversionPipeline.Run(new ConversionOptions(mbox, Path.Combine(_dir, "saida"), "volume", mapping, 20480L * 1024 * 1024), inventory, null, CancellationToken.None);
        clock.Stop();
        sampler.Dispose();
        peak = Math.Max(peak, Process.GetCurrentProcess().PeakWorkingSet64);

        File.WriteAllText(Path.Combine(_dir, "medicao.txt"), $"{generated.Messages.Count} e-mails, {clock.Elapsed.TotalSeconds:F1} s, pico {peak / 1024 / 1024} MB");
        Console.WriteLine(File.ReadAllText(Path.Combine(_dir, "medicao.txt")));
        Assert.True(result.Verified);
        Assert.Equal(generated.Messages.Count, result.Converted);
        Assert.True(clock.Elapsed < TimeSpan.FromMinutes(2), $"levou {clock.Elapsed}");
        Assert.True(peak < 3L * 1024 * 1024 * 1024, $"pico de {peak / 1024 / 1024} MB");

        string? python = Environment.GetEnvironmentVariable("MBOXPST_PYTHON");
        if (python is null) return;
        var sample = generated.Messages.Where((_, i) => i % Math.Max(1, generated.Messages.Count / 500) == 0).Select(m => new
        {
            messageId = m.MessageId,
            folders = LabelRouter.For(m.Labels, mapping, "volume").Folders.Select(f => string.Join("/", f)).ToArray(),
            subject = m.Subject,
        });
        string expected = Path.Combine(_dir, "amostra.json");
        File.WriteAllText(expected, JsonSerializer.Serialize(sample));
        var psi = new ProcessStartInfo(python, new[] { "-I", Path.Combine(AppContext.BaseDirectory, "independent", "verify_pst.py"), "--amostra", expected }.Concat(result.OutputFiles)) { RedirectStandardOutput = true };
        using Process verify = Process.Start(psi)!;
        string output = verify.StandardOutput.ReadToEnd();
        verify.WaitForExit();
        Assert.True(verify.ExitCode == 0, output);
    }
}
