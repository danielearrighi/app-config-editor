using AppConfigEditor.Core;

namespace AppConfigEditor.Core.Tests;

public class ConfigWorkspaceTests : IDisposable
{
    private readonly string _root;

    public ConfigWorkspaceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "appconfigeditor-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* ignore */ }
    }

    private void WriteConfig(string relativeDir, params (string Key, string Value)[] entries)
    {
        string dir = Path.Combine(_root, relativeDir);
        Directory.CreateDirectory(dir);
        string body = string.Join("\n", entries.Select(e => $"\t<add key=\"{e.Key}\" value=\"{e.Value}\" />"));
        File.WriteAllText(
            Path.Combine(dir, "AppSettings.config"),
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<appSettings>\n" + body + "\n</appSettings>\n");
    }

    [Fact]
    public void Load_discovers_files_recursively_and_builds_index()
    {
        WriteConfig("RMB", ("A", "1"), ("B", "2"));
        WriteConfig("RMB_DEV/SVILUPPO", ("A", "9"), ("C", "3"));
        File.WriteAllText(Path.Combine(_root, "RMB", "Other.config"), "ignored");

        var ws = ConfigWorkspace.Load(_root);

        Assert.Equal(2, ws.Files.Count);
        Assert.Equal(new[] { "A", "B", "C" }, ws.Keys);
        Assert.Single(ws.FilesContainingKey("B"));
        Assert.Equal(2, ws.FilesContainingKey("A").Count);
        Assert.Empty(ws.FilesContainingKey("Z"));
        Assert.Contains(ws.Files, f => f.Tenant == "RMB_DEV/SVILUPPO");
    }

    [Fact]
    public void SetValue_propagates_only_to_files_containing_key()
    {
        WriteConfig("RMB", ("A", "1"), ("B", "2"));
        WriteConfig("RM2", ("A", "9"));
        var ws = ConfigWorkspace.Load(_root);

        int changed = ConfigOperations.SetValue(ws, "A", "X", ws.Files.Select(f => f.Path));

        Assert.Equal(2, changed);
        Assert.All(ws.Files, f => Assert.Equal("X", ws.Document(f.Path).Find("A")!.Value));
        string rmb = ws.Files.Single(f => f.Tenant == "RMB").Path;
        Assert.Equal("2", ws.Document(rmb).Find("B")!.Value);
    }

    [Fact]
    public void AddKey_targets_only_selected_files()
    {
        WriteConfig("RMB", ("A", "1"));
        WriteConfig("RM2", ("A", "9"));
        var ws = ConfigWorkspace.Load(_root);
        string target = ws.Files.Single(f => f.Tenant == "RM2").Path;

        int changed = ConfigOperations.AddKey(ws, "Nuova", "v", new[] { target });

        Assert.Equal(1, changed);
        Assert.Null(ws.Document(ws.Files.Single(f => f.Tenant == "RMB").Path).Find("Nuova"));
        Assert.Equal("v", ws.Document(target).Find("Nuova")!.Value);
        Assert.Single(ws.FilesContainingKey("Nuova"));
    }

    [Fact]
    public void DeleteKey_removes_from_selected_files_only()
    {
        WriteConfig("RMB", ("A", "1"), ("B", "2"));
        WriteConfig("RM2", ("A", "9"), ("B", "8"));
        var ws = ConfigWorkspace.Load(_root);
        string target = ws.Files.Single(f => f.Tenant == "RMB").Path;

        int changed = ConfigOperations.DeleteKey(ws, "B", new[] { target });

        Assert.Equal(1, changed);
        Assert.Null(ws.Document(target).Find("B"));
        Assert.NotNull(ws.Document(ws.Files.Single(f => f.Tenant == "RM2").Path).Find("B"));
        Assert.Single(ws.FilesContainingKey("B"));
    }

    [Fact]
    public void TextDiff_reports_changed_line()
    {
        var diff = TextDiff.Diff("a\nb\nc", "a\nB\nc");

        Assert.Contains(diff, d => d.Kind == DiffKind.Removed && d.Text == "b");
        Assert.Contains(diff, d => d.Kind == DiffKind.Added && d.Text == "B");
        Assert.Contains(diff, d => d.Kind == DiffKind.Context && d.Text == "a");
    }
}
