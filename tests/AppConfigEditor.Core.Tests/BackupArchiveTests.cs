using System.IO.Compression;
using AppConfigEditor.Core;

namespace AppConfigEditor.Core.Tests;

public class BackupArchiveTests : IDisposable
{
    private readonly string _root;

    public BackupArchiveTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "appconfigeditor-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* ignore */ }
    }

    private string WriteFile(string relativePath, string content)
    {
        string full = Path.Combine(_root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
        return full;
    }

    [Fact]
    public void Create_stores_all_files_in_backups_folder_with_relative_paths()
    {
        string a = WriteFile(Path.Combine("RMB", "AppSettings.config"), "contenuto-A");
        string b = WriteFile(Path.Combine("RMB_DEV", "SVILUPPO", "AppSettings.config"), "contenuto-B");

        string archive = BackupArchive.Create(_root, new[] { a, b });

        Assert.True(File.Exists(archive));
        Assert.Equal(
            Path.Combine(_root, BackupArchive.FolderName),
            Path.GetDirectoryName(archive));

        using var zip = ZipFile.OpenRead(archive);
        Assert.Equal(2, zip.Entries.Count);
        Assert.Equal("contenuto-A", ReadEntry(zip, "RMB/AppSettings.config"));
        Assert.Equal("contenuto-B", ReadEntry(zip, "RMB_DEV/SVILUPPO/AppSettings.config"));
    }

    [Fact]
    public void Create_does_not_touch_original_files_and_avoids_duplicate_name()
    {
        string a = WriteFile("AppSettings.config", "originale");
        string first = BackupArchive.Create(_root, new[] { a });
        string second = BackupArchive.Create(_root, new[] { a });

        Assert.NotEqual(first, second);
        Assert.Equal("originale", File.ReadAllText(a));
        Assert.False(File.Exists(a + ".bak"));
    }

    private static string ReadEntry(ZipArchive zip, string name)
    {
        var entry = zip.GetEntry(name);
        Assert.NotNull(entry);
        using var reader = new StreamReader(entry!.Open());
        return reader.ReadToEnd();
    }
}
