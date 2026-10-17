using System.Globalization;
using MboxPst.Generator;

string? Arg(string name) => args.SkipWhile(a => a != name).Skip(1).FirstOrDefault();

string? output = Arg("--out");
double sizeGb = double.Parse(Arg("--size-gb") ?? "1", CultureInfo.InvariantCulture);
int seed = int.Parse(Arg("--seed") ?? "1", CultureInfo.InvariantCulture);
if (output is null || args.FirstOrDefault() != "generate")
{
    Console.Error.WriteLine("uso: mboxpst-generator generate --out <arquivo.mbox> --size-gb <n> --seed <n>");
    return 1;
}
GeneratedMbox result = MboxGenerator.Generate(output, (long)(sizeGb * 1024 * 1024 * 1024), seed);
Console.WriteLine($"{result.Messages.Count} e-mails em {output}");
return 0;
