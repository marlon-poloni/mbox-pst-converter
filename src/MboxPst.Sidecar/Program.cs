using System.Text;
using MboxPst.Sidecar;

var input = new StreamReader(Console.OpenStandardInput(), new UTF8Encoding(false));
var output = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false));
new SidecarHost(input, output).Run();
