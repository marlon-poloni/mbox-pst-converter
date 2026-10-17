using System.Text;

namespace MboxPst.Core.Text;

public static class Cp1252Repair
{
    private static readonly char[] Map = BuildMap();

    public static string? Fix(string? text)
    {
        if (text is null || !text.Any(IsC1))
            return text;

        var builder = new StringBuilder(text.Length);
        foreach (char c in text)
            builder.Append(IsC1(c) ? Map[c - 0x80] : c);
        return builder.ToString();
    }

    private static bool IsC1(char c) => c >= '\u0080' && c <= '\u009F';

    private static char[] BuildMap()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Encoding cp1252 = Encoding.GetEncoding(1252, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
        var map = new char[32];
        for (int i = 0; i < map.Length; i++)
        {
            try
            {
                map[i] = cp1252.GetChars(new[] { (byte)(0x80 + i) })[0];
            }
            catch (DecoderFallbackException)
            {
                map[i] = (char)(0x80 + i);
            }
        }
        return map;
    }
}
