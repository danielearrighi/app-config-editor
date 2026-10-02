namespace AppConfigEditor.Core;

/// <summary>
/// Insieme dei file AppSettings.config caricati a partire da una cartella base,
/// con l'indice chiave -> file che la contengono.
/// </summary>
public sealed class ConfigWorkspace
{
    private readonly List<ConfigFileInfo> _files = new();
    private readonly Dictionary<string, AppSettingsDocument> _documents = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<string>> _pathsByKey = new(StringComparer.Ordinal);
    private readonly List<string> _keys = new();

    public string BasePath { get; }
    public IReadOnlyList<ConfigFileInfo> Files => _files;
    public IReadOnlyList<string> Keys => _keys;
    public IReadOnlyDictionary<string, AppSettingsDocument> Documents => _documents;

    private ConfigWorkspace(string basePath)
    {
        BasePath = basePath;
    }

    public AppSettingsDocument Document(string path) => _documents[path];

    /// <summary>Percorsi dei file che contengono la chiave indicata.</summary>
    public IReadOnlyList<string> FilesContainingKey(string key)
        => _pathsByKey.TryGetValue(key, out var list) ? list : Array.Empty<string>();

    public ConfigFileInfo? FindFile(string path)
        => _files.FirstOrDefault(f => string.Equals(f.Path, path, StringComparison.Ordinal));

    /// <summary>File con modifiche non ancora salvate.</summary>
    public IEnumerable<AppSettingsDocument> DirtyDocuments()
        => _documents.Values.Where(d => d.IsDirty);

    public static ConfigWorkspace Load(string basePath)
    {
        var workspace = new ConfigWorkspace(basePath);
        if (!Directory.Exists(basePath))
            return workspace;

        foreach (string fullPath in Directory
                     .EnumerateFiles(basePath, "AppSettings.config", SearchOption.AllDirectories)
                     .OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            string? directory = System.IO.Path.GetDirectoryName(fullPath);
            string relativeDir = directory is null
                ? string.Empty
                : System.IO.Path.GetRelativePath(basePath, directory);
            if (relativeDir == ".")
                relativeDir = string.Empty;

            string relativePath = System.IO.Path.GetRelativePath(basePath, fullPath);
            var info = new ConfigFileInfo(fullPath, relativeDir.Replace('\\', '/'), relativePath.Replace('\\', '/'));
            workspace._files.Add(info);

            try
            {
                workspace._documents[fullPath] = AppSettingsDocument.Load(fullPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // File non leggibile: lo saltiamo senza interrompere il caricamento.
            }
        }

        workspace.RebuildIndex();
        return workspace;
    }

    /// <summary>Ricalcola l'indice chiave -> file e l'elenco ordinato delle chiavi.</summary>
    public void RebuildIndex()
    {
        _pathsByKey.Clear();
        _keys.Clear();
        var known = new HashSet<string>(StringComparer.Ordinal);

        foreach (var file in _files)
        {
            if (!_documents.TryGetValue(file.Path, out var doc))
                continue;

            foreach (var entry in doc.Entries)
            {
                if (!_pathsByKey.TryGetValue(entry.Key, out var list))
                {
                    list = new List<string>();
                    _pathsByKey[entry.Key] = list;
                }

                if (!list.Contains(file.Path, StringComparer.Ordinal))
                    list.Add(file.Path);

                if (known.Add(entry.Key))
                    _keys.Add(entry.Key);
            }
        }

        _keys.Sort(StringComparer.OrdinalIgnoreCase);
    }
}
