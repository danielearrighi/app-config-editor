using System.Text;
using System.Text.RegularExpressions;

namespace AppConfigEditor.Core;

/// <summary>
/// Rappresenta un singolo file AppSettings.config in memoria.
/// Le modifiche agiscono sui soli span interessati, preservando commenti,
/// indentazione, ordine e spaziatura originali.
/// </summary>
public sealed class AppSettingsDocument
{
    private static readonly Regex AttributeRegex = new(
        "(?<name>[A-Za-z_:][A-Za-z0-9_:.\\-]*)\\s*=\\s*(?<q>[\"'])(?<val>.*?)\\k<q>",
        RegexOptions.Compiled | RegexOptions.Singleline);

    private readonly List<AppSettingEntry> _entries = new();
    private int[] _lineStarts = [0];
    private readonly bool _hasBom;

    public string Path { get; }
    public string OriginalText { get; private set; }
    public string Text { get; private set; }
    public IReadOnlyList<AppSettingEntry> Entries => _entries;
    public bool IsDirty => !string.Equals(Text, OriginalText, StringComparison.Ordinal);

    private AppSettingsDocument(string path, string text, bool hasBom)
    {
        Path = path;
        Text = text;
        OriginalText = text;
        _hasBom = hasBom;
        Reindex();
    }

    public static AppSettingsDocument Load(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        bool hasBom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
        int offset = hasBom ? 3 : 0;
        string text = new UTF8Encoding(false).GetString(bytes, offset, bytes.Length - offset);
        return new AppSettingsDocument(path, text, hasBom);
    }

    public static AppSettingsDocument Parse(string path, string text, bool hasBom = false)
        => new(path, text, hasBom);

    public bool ContainsKey(string key)
        => _entries.Any(e => string.Equals(e.Key, key, StringComparison.Ordinal));

    public AppSettingEntry? Find(string key)
        => _entries.FirstOrDefault(e => string.Equals(e.Key, key, StringComparison.Ordinal));

    /// <summary>Imposta il valore di una chiave esistente.</summary>
    public bool SetValue(string key, string value)
    {
        var entry = Find(key);
        if (entry is null)
            return false;

        string encoded = XmlValue.Encode(value);
        Text = string.Concat(
            Text.AsSpan(0, entry.ValueStart),
            encoded.AsSpan(),
            Text.AsSpan(entry.ValueEnd));
        Reindex();
        return true;
    }

    /// <summary>Elimina una chiave (e la sua riga, se contiene solo l'elemento).</summary>
    public bool RemoveEntry(string key)
    {
        var entry = Find(key);
        if (entry is null)
            return false;

        int lineStart = Text.LastIndexOf('\n', Math.Max(0, entry.Start - 1)) + 1;
        int lineEnd = Text.IndexOf('\n', entry.End);
        int removeEnd = lineEnd < 0 ? Text.Length : lineEnd + 1;

        string before = Text.Substring(lineStart, entry.Start - lineStart);
        string after = lineEnd < 0 ? string.Empty : Text.Substring(entry.End, lineEnd - entry.End);
        bool onlyElement = before.Trim().Length == 0 && after.Trim().Length == 0;

        Text = onlyElement
            ? string.Concat(Text.AsSpan(0, lineStart), Text.AsSpan(removeEnd))
            : string.Concat(Text.AsSpan(0, entry.Start), Text.AsSpan(entry.End));

        Reindex();
        return true;
    }

    /// <summary>Aggiunge una nuova chiave, se non già presente.</summary>
    public bool AddEntry(string key, string value)
    {
        if (ContainsKey(key))
            return false;

        string newline = Text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        string indent = DetectIndent();
        string element = $"<add key=\"{XmlValue.Encode(key)}\" value=\"{XmlValue.Encode(value)}\" />";

        int closing = Text.IndexOf("</appSettings>", StringComparison.Ordinal);
        if (closing >= 0)
        {
            int lineStart = Text.LastIndexOf('\n', Math.Max(0, closing - 1)) + 1;
            bool ownLine = Text.AsSpan(lineStart, closing - lineStart).Trim().Length == 0;
            if (ownLine)
                Text = Text.Insert(lineStart, indent + element + newline);
            else
                Text = Text.Insert(closing, newline + indent + element);
        }
        else
        {
            if (Text.Length > 0 && !Text.EndsWith('\n'))
                Text += newline;
            Text += indent + element + newline;
        }

        Reindex();
        return true;
    }

    /// <summary>Ripristina il contenuto dal disco, scartando le modifiche in sospeso.</summary>
    public void Reload()
    {
        byte[] bytes = File.ReadAllBytes(Path);
        bool hasBom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
        int offset = hasBom ? 3 : 0;
        string text = new UTF8Encoding(false).GetString(bytes, offset, bytes.Length - offset);
        Text = text;
        OriginalText = text;
        Reindex();
    }

    /// <summary>Accetta le modifiche correnti come nuovo stato salvato.</summary>
    public void MarkSaved() => OriginalText = Text;

    public void Save()
    {
        var encoding = new UTF8Encoding(_hasBom);
        File.WriteAllText(Path, Text, encoding);
        OriginalText = Text;
    }

    private string DetectIndent()
    {
        if (_entries.Count > 0)
        {
            int start = _entries[0].Start;
            int lineStart = Text.LastIndexOf('\n', Math.Max(0, start - 1)) + 1;
            int p = lineStart;
            while (p < Text.Length && (Text[p] == ' ' || Text[p] == '\t'))
                p++;
            return Text.Substring(lineStart, p - lineStart);
        }

        return "\t";
    }

    private void Reindex()
    {
        _entries.Clear();
        _lineStarts = ComputeLineStarts(Text);

        int i = 0;
        while (i < Text.Length)
        {
            int addStart = Text.IndexOf("<add", i, StringComparison.Ordinal);
            if (addStart < 0)
                break;

            int afterName = addStart + 4;
            if (afterName < Text.Length && !IsNameBoundary(Text[afterName]))
            {
                i = afterName;
                continue;
            }

            int elementEnd = FindElementEnd(afterName);
            string element = Text.Substring(addStart, elementEnd - addStart);
            var attributes = ParseAttributes(element, addStart);

            bool hasKey = attributes.TryGetValue("key", out var keySpan);
            if (!hasKey)
                hasKey = attributes.TryGetValue("name", out keySpan);

            if (hasKey && attributes.TryGetValue("value", out var valueSpan))
            {
                _entries.Add(new AppSettingEntry(
                    Key: XmlValue.Decode(Slice(keySpan)),
                    Value: XmlValue.Decode(Slice(valueSpan)),
                    LineNumber: GetLineNumber(addStart),
                    Start: addStart,
                    End: elementEnd < Text.Length ? elementEnd + 1 : elementEnd,
                    ValueStart: valueSpan.Start,
                    ValueEnd: valueSpan.End));
            }

            i = elementEnd < Text.Length ? elementEnd + 1 : Text.Length;
        }
    }

    private static bool IsNameBoundary(char c)
        => char.IsWhiteSpace(c) || c == '>' || c == '/';

    private int FindElementEnd(int from)
    {
        char quote = '\0';
        for (int pos = from; pos < Text.Length; pos++)
        {
            char c = Text[pos];
            if (quote != '\0')
            {
                if (c == quote)
                    quote = '\0';
            }
            else if (c is '"' or '\'')
            {
                quote = c;
            }
            else if (c == '>')
            {
                return pos;
            }
        }

        return Text.Length;
    }

    private string Slice(AttrSpan span)
        => Text.Substring(span.Start, span.End - span.Start);

    private static Dictionary<string, AttrSpan> ParseAttributes(string element, int elementOffset)
    {
        var result = new Dictionary<string, AttrSpan>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in AttributeRegex.Matches(element))
        {
            var value = match.Groups["val"];
            int start = elementOffset + value.Index;
            result[match.Groups["name"].Value] = new AttrSpan(start, start + value.Length);
        }

        return result;
    }

    private static int[] ComputeLineStarts(string text)
    {
        var starts = new List<int> { 0 };
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
                starts.Add(i + 1);
        }

        return starts.ToArray();
    }

    private int GetLineNumber(int offset)
    {
        int index = Array.BinarySearch(_lineStarts, offset);
        if (index < 0)
            index = ~index - 1;
        return index + 1;
    }

    private readonly record struct AttrSpan(int Start, int End);
}
