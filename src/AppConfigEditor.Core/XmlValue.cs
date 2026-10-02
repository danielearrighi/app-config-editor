using System.Text;

namespace AppConfigEditor.Core;

/// <summary>
/// Codifica/decodifica dei valori XML preservando lo stile dei file .config
/// (le stringhe contengono entità come &amp;quot; e &amp;lt;).
/// </summary>
internal static class XmlValue
{
    private static readonly Dictionary<string, char> Entities = new(StringComparer.Ordinal)
    {
        ["quot"] = '"',
        ["apos"] = '\'',
        ["lt"] = '<',
        ["gt"] = '>',
        ["amp"] = '&',
    };

    public static string Decode(string raw)
    {
        if (raw.IndexOf('&') < 0)
            return raw;

        var sb = new StringBuilder(raw.Length);
        for (int i = 0; i < raw.Length; i++)
        {
            char c = raw[i];
            if (c != '&')
            {
                sb.Append(c);
                continue;
            }

            int semi = raw.IndexOf(';', i + 1);
            if (semi < 0)
            {
                sb.Append(c);
                continue;
            }

            string body = raw.Substring(i + 1, semi - i - 1);
            if (body.Length > 0 && body[0] == '#')
            {
                int code;
                bool parsed = body.Length > 1 && (body[1] == 'x' || body[1] == 'X')
                    ? int.TryParse(body.AsSpan(2), System.Globalization.NumberStyles.HexNumber, null, out code)
                    : int.TryParse(body.AsSpan(1), out code);
                if (parsed && code >= 0 && code <= 0x10FFFF)
                {
                    sb.Append(char.ConvertFromUtf32(code));
                    i = semi;
                    continue;
                }
            }
            else if (Entities.TryGetValue(body, out char entity))
            {
                sb.Append(entity);
                i = semi;
                continue;
            }

            sb.Append(c);
        }

        return sb.ToString();
    }

    public static string Encode(string value)
    {
        if (value.IndexOf('&') < 0 && value.IndexOf('<') < 0 &&
            value.IndexOf('>') < 0 && value.IndexOf('"') < 0)
            return value;

        var sb = new StringBuilder(value.Length + 8);
        foreach (char c in value)
        {
            switch (c)
            {
                case '&': sb.Append("&amp;"); break;
                case '<': sb.Append("&lt;"); break;
                case '>': sb.Append("&gt;"); break;
                case '"': sb.Append("&quot;"); break;
                default: sb.Append(c); break;
            }
        }

        return sb.ToString();
    }
}
