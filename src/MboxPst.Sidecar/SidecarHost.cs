using Mail2Pst.Core.Writing;
using MboxPst.Core.Conversion;
using MboxPst.Core.Gmail;
using MboxPst.Core.Mapping;
using MboxPst.Core.Previewing;
using MboxPst.Core.Scanning;
using MboxPst.Sidecar.Protocol;

namespace MboxPst.Sidecar;

public sealed class SidecarHost(TextReader input, TextWriter output)
{
    public const string DefaultStoreName = "Todos os e-mails";

    private readonly object _writeLock = new();
    private readonly object _stateLock = new();
    private Task _current = Task.CompletedTask;
    private bool _busy;
    private CancellationTokenSource? _cts;
    private string? _path;
    private LabelInventory? _inventory;

    internal Func<PstWriter>? WriterFactory { get; init; }

    public void Run()
    {
        while (input.ReadLine() is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;
            Command? command = Wire.ParseCommand(line);
            switch (command)
            {
                case null:
                    Emit(new ErrorEvent("Comando inválido."));
                    break;
                case CancelCommand:
                    lock (_stateLock) _cts?.Cancel();
                    break;
                case PreviewCommand preview:
                    Preview(preview);
                    break;
                default:
                    Start(command);
                    break;
            }
        }
        lock (_stateLock) _cts?.Cancel();
        try { _current.Wait(); } catch (AggregateException) { }
    }

    private void Start(Command command)
    {
        lock (_stateLock)
        {
            if (_busy)
            {
                Emit(new ErrorEvent("Aguarde a operação em andamento terminar."));
                return;
            }
            _busy = true;
            if (command is ScanCommand)
            {
                _path = null;
                _inventory = null;
            }
            _cts = new CancellationTokenSource();
            CancellationToken ct = _cts.Token;
            _current = Task.Run(() => Finish(Guard(() => command switch
            {
                ScanCommand scan => Scan(scan, ct),
                ConvertCommand convert => Convert(convert, ct),
                _ => new ErrorEvent("Comando inválido."),
            })));
        }
    }

    private void Finish(Event final)
    {
        lock (_stateLock) _busy = false;
        Emit(final);
    }

    private static Event Guard(Func<Event> action)
    {
        try
        {
            return action();
        }
        catch (OperationCanceledException)
        {
            return new CancelledEvent();
        }
        catch (ConversionException ex)
        {
            return new ErrorEvent(ex.Message);
        }
        catch (Exception ex)
        {
            return new ErrorEvent($"Erro inesperado: {ex.Message}");
        }
    }

    private Event Scan(ScanCommand command, CancellationToken ct)
    {
        if (!File.Exists(command.Path))
            throw new ConversionException($"Arquivo não encontrado: {command.Path}");

        long sourceBytes = new FileInfo(command.Path).Length;
        long lastReported = -1;
        LabelInventory inventory = InventoryScanner.Scan(command.Path, read =>
        {
            if (read - lastReported >= 8 * 1024 * 1024 || read == sourceBytes)
            {
                lastReported = read;
                Emit(new ScanProgressEvent(read, sourceBytes));
            }
        }, ct);

        MappingConfig mapping = DefaultMapping.For(inventory.CountByLabel, DefaultStoreName);
        Preview preview = PreviewBuilder.Build(inventory, mapping, Fallback(command.Path));
        lock (_stateLock)
        {
            _path = command.Path;
            _inventory = inventory;
        }

        LabelDto[] labels = inventory.CountByLabel
            .OrderByDescending(p => p.Value).ThenBy(p => p.Key, StringComparer.Ordinal)
            .Select(p => new LabelDto(p.Key, KindName(LabelCatalog.Classify(p.Key)), p.Value))
            .ToArray();
        return new ScanDoneEvent(command.Path, Path.GetFileName(command.Path), inventory.Entries.Count, sourceBytes, labels,
            MappingDto.From(mapping), PreviewDto.From(preview));
    }

    private void Preview(PreviewCommand command)
    {
        string? path;
        LabelInventory? inventory;
        lock (_stateLock)
        {
            path = _path;
            inventory = _inventory;
        }
        if (inventory is null || path is null)
        {
            Emit(new ErrorEvent("Escolha e leia um arquivo antes."));
            return;
        }
        Emit(Guard(() => new PreviewEvent(PreviewDto.From(PreviewBuilder.Build(inventory, command.Mapping.ToConfig(), Fallback(path))))));
    }

    private Event Convert(ConvertCommand command, CancellationToken ct)
    {
        string? path;
        LabelInventory? inventory;
        lock (_stateLock)
        {
            path = _path;
            inventory = _inventory;
        }
        if (inventory is null || path is null)
            throw new ConversionException("Escolha e leia um arquivo antes.");

        var options = new ConversionOptions(path, command.OutputDirectory, command.OutputName, command.Mapping.ToConfig(), command.MaxPartMB * 1024 * 1024)
        {
            WriterFactory = WriterFactory,
        };
        ConversionResult result = ConversionPipeline.Run(options, inventory,
            p => Emit(new ConvertProgressEvent(p.Done, p.Total, p.BytesRead, p.SourceBytes, Math.Round(p.MegabytesPerSecond, 1), p.Remaining?.TotalSeconds)),
            ct);
        return ConvertDoneEvent.From(result);
    }

    private static string Fallback(string path) => Path.GetFileNameWithoutExtension(path);

    private static string KindName(LabelKind kind) => System.Text.Json.JsonNamingPolicy.CamelCase.ConvertName(kind.ToString());

    private void Emit(Event e)
    {
        string line = Wire.Serialize(e);
        lock (_writeLock)
        {
            output.WriteLine(line);
            output.Flush();
        }
    }
}
