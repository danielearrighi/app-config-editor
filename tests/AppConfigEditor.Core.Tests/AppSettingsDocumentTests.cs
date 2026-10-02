using AppConfigEditor.Core;

namespace AppConfigEditor.Core.Tests;

public class AppSettingsDocumentTests : IDisposable
{
    private readonly string _dir;

    public AppSettingsDocumentTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "appconfigeditor-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* ignore */ }
    }

    private string WriteFile(string name, string content)
    {
        string path = Path.Combine(_dir, name);
        File.WriteAllText(path, content);
        return path;
    }

    private const string Sample =
        "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
        "\n" +
        "<appSettings>\n" +
        "\t<!--SECURITY-->\n" +
        "\t<add key=\"AESKey\" value=\"secret\" />\n" +
        "\t<!--PORTALE-->\n" +
        "\t<add key=\"WebsiteName\" value=\"RMB Gestione Rese\" />\n" +
        "\t<add key=\"WebsiteFooter\" value=\"&lt;a href=&quot;http://x&quot;&gt;X&lt;/a&gt;\" />\n" +
        "</appSettings>\n";

    [Fact]
    public void Parses_all_entries_with_offsets()
    {
        string path = WriteFile("AppSettings.config", Sample);
        var doc = AppSettingsDocument.Load(path);

        Assert.Equal(3, doc.Entries.Count);
        Assert.Equal("AESKey", doc.Entries[0].Key);
        Assert.Equal("secret", doc.Entries[0].Value);
        Assert.Equal(5, doc.Entries[0].LineNumber);
        Assert.Equal("RMB Gestione Rese", doc.Entries[1].Value);
        Assert.Equal("<a href=\"http://x\">X</a>", doc.Entries[2].Value);
    }

    [Fact]
    public void Setting_same_value_leaves_text_untouched()
    {
        string path = WriteFile("AppSettings.config", Sample);
        var doc = AppSettingsDocument.Load(path);

        Assert.True(doc.SetValue("WebsiteName", "RMB Gestione Rese"));
        Assert.Equal(Sample, doc.Text);
        Assert.False(doc.IsDirty);
    }

    [Fact]
    public void SetValue_changes_only_the_value_and_preserves_comments()
    {
        string path = WriteFile("AppSettings.config", Sample);
        var doc = AppSettingsDocument.Load(path);

        Assert.True(doc.SetValue("WebsiteName", "Nuovo Nome"));

        Assert.Equal(Sample.Replace("RMB Gestione Rese", "Nuovo Nome"), doc.Text);
        Assert.Contains("<!--SECURITY-->", doc.Text);
        Assert.Contains("\t<add key=\"AESKey\" value=\"secret\" />", doc.Text);
        Assert.True(doc.IsDirty);
    }

    [Fact]
    public void SetValue_encodes_special_characters()
    {
        string path = WriteFile("AppSettings.config", Sample);
        var doc = AppSettingsDocument.Load(path);

        doc.SetValue("WebsiteName", "a & b < c > d \"e\"");

        Assert.Contains("value=\"a &amp; b &lt; c &gt; d &quot;e&quot;\"", doc.Text);
        Assert.Equal("a & b < c > d \"e\"", doc.Find("WebsiteName")!.Value);
    }

    [Fact]
    public void AddEntry_inserts_before_closing_tag_with_tab_indent()
    {
        string path = WriteFile("AppSettings.config", Sample);
        var doc = AppSettingsDocument.Load(path);

        Assert.True(doc.AddEntry("NewKey", "NewValue"));
        Assert.False(doc.AddEntry("NewKey", "other"));

        Assert.Contains("\t<add key=\"NewKey\" value=\"NewValue\" />\n</appSettings>", doc.Text);
        Assert.Equal(4, doc.Entries.Count);
    }

    [Fact]
    public void RemoveEntry_removes_the_whole_line()
    {
        string path = WriteFile("AppSettings.config", Sample);
        var doc = AppSettingsDocument.Load(path);

        Assert.True(doc.RemoveEntry("WebsiteName"));

        Assert.DoesNotContain("WebsiteName", doc.Text);
        Assert.DoesNotContain("RMB Gestione Rese", doc.Text);
        Assert.Contains("<!--PORTALE-->", doc.Text);
        Assert.Equal(2, doc.Entries.Count);
    }

    [Fact]
    public void Save_then_reload_roundtrips_byte_exact()
    {
        string path = WriteFile("AppSettings.config", Sample);
        var doc = AppSettingsDocument.Load(path);
        doc.SetValue("AESKey", "nuovo");
        doc.AddEntry("Zeta", "1");
        doc.Save();

        string onDisk = File.ReadAllText(path);
        Assert.Equal(doc.Text, onDisk);

        var reloaded = AppSettingsDocument.Load(path);
        Assert.Equal("nuovo", reloaded.Find("AESKey")!.Value);
        Assert.False(reloaded.IsDirty);
    }
}
