namespace AppConfigEditor.Core;

public enum DiffKind
{
    Context,
    Added,
    Removed,
}

public sealed record DiffLine(DiffKind Kind, string Text);

/// <summary>Diff riga per riga basato su LCS, sufficiente per l'anteprima dei file .config.</summary>
public static class TextDiff
{
    public static IReadOnlyList<DiffLine> Diff(string oldText, string newText)
    {
        string[] a = SplitLines(oldText);
        string[] b = SplitLines(newText);
        int n = a.Length;
        int m = b.Length;

        int[,] lcs = new int[n + 1, m + 1];
        for (int i = n - 1; i >= 0; i--)
        {
            for (int j = m - 1; j >= 0; j--)
            {
                lcs[i, j] = a[i] == b[j]
                    ? lcs[i + 1, j + 1] + 1
                    : Math.Max(lcs[i + 1, j], lcs[i, j + 1]);
            }
        }

        var result = new List<DiffLine>();
        int x = 0;
        int y = 0;
        while (x < n && y < m)
        {
            if (a[x] == b[y])
            {
                result.Add(new DiffLine(DiffKind.Context, a[x]));
                x++;
                y++;
            }
            else if (lcs[x + 1, y] >= lcs[x, y + 1])
            {
                result.Add(new DiffLine(DiffKind.Removed, a[x]));
                x++;
            }
            else
            {
                result.Add(new DiffLine(DiffKind.Added, b[y]));
                y++;
            }
        }

        while (x < n)
            result.Add(new DiffLine(DiffKind.Removed, a[x++]));
        while (y < m)
            result.Add(new DiffLine(DiffKind.Added, b[y++]));

        return result;
    }

    private static string[] SplitLines(string text)
        => text.Replace("\r\n", "\n").Split('\n');
}
