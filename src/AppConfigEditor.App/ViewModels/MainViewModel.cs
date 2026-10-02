using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using AppConfigEditor.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AppConfigEditor.App.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly AppSettingsStore _settingsStore = new();
    private ConfigWorkspace? _workspace;
    private readonly Dictionary<string, FileNode> _nodesByPath = new(StringComparer.Ordinal);

    public MainViewModel()
    {
        var settings = _settingsStore.Load();
        if (!string.IsNullOrWhiteSpace(settings.BasePath))
            BasePath = settings.BasePath!;

        Log("Applicazione avviata.");

        if (!string.IsNullOrWhiteSpace(BasePath) && Directory.Exists(BasePath))
            LoadWorkspace();
        else
            StatusMessage = "Seleziona la cartella base che contiene i file AppSettings.config.";
    }

    public ObservableCollection<FileNode> FileTree { get; } = new();
    public ObservableCollection<KeyItemViewModel> Keys { get; } = new();
    public ObservableCollection<KeyItemViewModel> FilteredKeys { get; } = new();
    public ObservableCollection<FileValueRow> DetailRows { get; } = new();
    public ObservableCollection<string> Changes { get; } = new();
    public ObservableCollection<string> Logs { get; } = new();
    public ObservableCollection<string> Backups { get; } = new();

    [ObservableProperty] public partial string BasePath { get; set; } = string.Empty;
    [ObservableProperty] public partial string SearchText { get; set; } = string.Empty;
    [ObservableProperty] public partial string StatusMessage { get; set; } = "Pronto";
    [ObservableProperty] public partial string EditValue { get; set; } = string.Empty;
    [ObservableProperty] public partial string NewKeyName { get; set; } = string.Empty;
    [ObservableProperty] public partial string NewKeyValue { get; set; } = string.Empty;
    [ObservableProperty] public partial KeyItemViewModel? SelectedKey { get; set; }
    [ObservableProperty] public partial FileNode? SelectedNode { get; set; }
    [ObservableProperty] public partial int ModifiedCount { get; set; }
    [ObservableProperty] public partial int SelectedFileCount { get; set; }
    [ObservableProperty] public partial string DetailHeader { get; set; } = "(nessuna chiave selezionata)";
    [ObservableProperty] public partial bool HasWorkspace { get; set; }
    [ObservableProperty] public partial int FileCount { get; set; }
    [ObservableProperty] public partial bool HasChanges { get; set; }

    // ---------------------------------------------------------------- Caricamento

    public void LoadFromPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        BasePath = path;
        try
        {
            _settingsStore.Save(new AppSettings { BasePath = path });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log($"Impossibile salvare le impostazioni: {ex.Message}");
        }

        LoadWorkspace();
    }

    private void LoadWorkspace()
    {
        try
        {
            _workspace = ConfigWorkspace.Load(BasePath);
        }
        catch (Exception ex)
        {
            _workspace = null;
            HasWorkspace = false;
            StatusMessage = $"Errore nel caricamento: {ex.Message}";
            Log($"Errore: {ex.Message}");
            return;
        }

        HasWorkspace = true;
        FileCount = _workspace.Files.Count;
        Log($"Caricati {FileCount} file AppSettings.config da '{BasePath}'.");

        BuildTree();
        CheckAll();
        RefreshKeys();
        RefreshDetail();
        RefreshPendingChanges();
        SelectedNode = null;
    }

    [RelayCommand]
    private void Reload()
    {
        if (!Directory.Exists(BasePath))
        {
            StatusMessage = "Cartella non valida.";
            return;
        }

        LoadWorkspace();
    }

    private void BuildTree()
    {
        FileTree.Clear();
        _nodesByPath.Clear();
        if (_workspace is null)
            return;

        foreach (var file in _workspace.Files)
        {
            ObservableCollection<FileNode> level = FileTree;
            string path = string.Empty;

            foreach (string segment in file.Tenant.Split('/', StringSplitOptions.RemoveEmptyEntries))
            {
                path = path.Length == 0 ? segment : path + "/" + segment;
                var folder = level.FirstOrDefault(n => n.IsFolder && n.Name == segment);
                if (folder is null)
                {
                    folder = new FileNode(segment, path, null);
                    level.Add(folder);
                }

                level = folder.Children;
            }

            var leaf = new FileNode(Path.GetFileName(file.Path), file.RelativePath, file, OnTreeCheckChanged);
            leaf.PropertyChanged += OnFileNodePropertyChanged;
            _nodesByPath[file.Path] = leaf;
            level.Add(leaf);
        }
    }

    /// <summary>
    /// Mantiene allineate le checkbox del pannello "Presenza per file" quando la
    /// selezione cambia dall'albero dei file a sinistra.
    /// </summary>
    private void OnFileNodePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(FileNode.IsChecked) || sender is not FileNode node || node.File is null)
            return;

        var row = DetailRows.FirstOrDefault(r => string.Equals(r.File.Path, node.File.Path, StringComparison.Ordinal));
        if (row is not null && row.IsFileSelected != node.IsChecked)
            row.IsFileSelected = node.IsChecked;
    }

    private void OnTreeCheckChanged()
    {
        SelectedFileCount = CheckedLeaves().Count();
        StatusMessage = SelectedFileCount == 0
            ? "Nessun file selezionato: le azioni agiscono sul file corrente o su tutti."
            : $"{SelectedFileCount} file selezionati.";
    }

    private IEnumerable<FileNode> AllLeaves()
    {
        var stack = new Stack<FileNode>(FileTree);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (node.IsFile)
                yield return node;
            foreach (var child in node.Children)
                stack.Push(child);
        }
    }

    private IEnumerable<FileNode> AllNodes()
    {
        var stack = new Stack<FileNode>(FileTree);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            yield return node;
            foreach (var child in node.Children)
                stack.Push(child);
        }
    }

    private IEnumerable<FileNode> CheckedLeaves() => AllLeaves().Where(n => n.IsChecked);

    [RelayCommand]
    private void ExpandAll()
    {
        foreach (var node in AllNodes())
            node.IsExpanded = true;
    }

    [RelayCommand]
    private void CollapseAll()
    {
        foreach (var node in AllNodes())
            node.IsExpanded = false;
    }

    [RelayCommand]
    private void CheckAll()
    {
        foreach (var leaf in AllLeaves())
            leaf.IsChecked = true;
    }

    [RelayCommand]
    private void CheckNone()
    {
        foreach (var leaf in AllLeaves())
            leaf.IsChecked = false;
    }

    // Seleziona/deseleziona solo i file elencati nel pannello di destra
    // (quelli che contengono la chiave selezionata).
    [RelayCommand]
    private void CheckAllDetail()
    {
        foreach (var row in DetailRows)
            row.IsFileSelected = true;
    }

    [RelayCommand]
    private void CheckNoneDetail()
    {
        foreach (var row in DetailRows)
            row.IsFileSelected = false;
    }

    // ---------------------------------------------------------------- Elenco chiavi

    private void RefreshKeys()
    {
        Keys.Clear();
        if (_workspace is null)
        {
            FilteredKeys.Clear();
            return;
        }

        foreach (string key in _workspace.Keys)
        {
            var paths = _workspace.FilesContainingKey(key);
            string? first = null;
            bool divergent = false;
            string preview = string.Empty;

            foreach (string path in paths)
            {
                string value = _workspace.Document(path).Find(key)?.Value ?? string.Empty;
                if (first is null)
                {
                    first = value;
                    preview = value;
                }
                else if (!string.Equals(first, value, StringComparison.Ordinal))
                {
                    divergent = true;
                }
            }

            if (preview.Length > 90)
                preview = preview[..87] + "...";

            Keys.Add(new KeyItemViewModel(key, paths.Count, divergent, preview));
        }

        ApplyFilter();
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        FilteredKeys.Clear();
        string query = SearchText?.Trim() ?? string.Empty;
        foreach (var key in Keys)
        {
            if (query.Length == 0 || key.Key.Contains(query, StringComparison.OrdinalIgnoreCase))
                FilteredKeys.Add(key);
        }
    }

    // ---------------------------------------------------------------- Dettaglio

    partial void OnSelectedKeyChanged(KeyItemViewModel? value) => RefreshDetail();

    private void RefreshDetail()
    {
        DetailRows.Clear();
        if (_workspace is null || SelectedKey is null)
        {
            DetailHeader = "(nessuna chiave selezionata)";
            return;
        }

        string key = SelectedKey.Key;
        DetailHeader = key;

        var values = new List<(ConfigFileInfo Info, string Value)>();
        foreach (string path in _workspace.FilesContainingKey(key))
        {
            var info = _workspace.FindFile(path);
            if (info is null)
                continue;

            string value = _workspace.Document(path).Find(key)?.Value ?? string.Empty;
            values.Add((info, value));
        }

        string mode = values
            .GroupBy(v => v.Value, StringComparer.Ordinal)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefault() ?? string.Empty;

        foreach (var (info, value) in values.OrderBy(v => v.Info.DisplayName, StringComparer.OrdinalIgnoreCase))
        {
            _nodesByPath.TryGetValue(info.Path, out var node);
            DetailRows.Add(new FileValueRow(info, value, !string.Equals(value, mode, StringComparison.Ordinal), node));
        }

        // Precompila il campo valore con il contenuto del file selezionato, o con la moda.
        var selected = SelectedNode?.File;
        string? initial = selected is not null
            ? values.FirstOrDefault(v => v.Info.Path == selected.Path).Value
            : null;

        EditValue = initial ?? (values.Count > 0 ? values[0].Value : string.Empty);
    }

    // ---------------------------------------------------------------- Operazioni

    [RelayCommand]
    private void ApplyValue() => ApplyValueTo(all: false);

    [RelayCommand]
    private void ApplyValueAll() => ApplyValueTo(all: true);

    private void ApplyValueTo(bool all)
    {
        if (_workspace is null || SelectedKey is null)
        {
            StatusMessage = "Seleziona una chiave.";
            return;
        }

        var targets = ResolveTargets(all, SelectedKey.Key);
        if (targets.Count == 0)
        {
            StatusMessage = "Nessun file di destinazione: seleziona i file (o usa 'a tutti').";
            return;
        }

        int changed = ConfigOperations.SetValue(_workspace, SelectedKey.Key, EditValue, targets);
        Log($"'{SelectedKey.Key}' impostata su {changed} file: {EditValue}");
        RefreshAfterEdit();
    }

    [RelayCommand]
    private void AddKey()
    {
        if (_workspace is null)
        {
            StatusMessage = "Nessun workspace caricato.";
            return;
        }

        string name = NewKeyName.Trim();
        if (name.Length == 0)
        {
            StatusMessage = "Specifica il nome della nuova chiave.";
            return;
        }

        if (_workspace.Keys.Contains(name, StringComparer.Ordinal))
        {
            StatusMessage = $"La chiave '{name}' esiste già. Usa 'Applica' per modificarne il valore.";
            return;
        }

        var targets = ResolveTargets(all: false, key: null);
        if (targets.Count == 0)
        {
            StatusMessage = "Seleziona almeno un file di destinazione.";
            return;
        }

        int changed = ConfigOperations.AddKey(_workspace, name, NewKeyValue, targets);
        Log($"Aggiunta chiave '{name}' a {changed} file.");
        NewKeyName = string.Empty;
        NewKeyValue = string.Empty;
        RefreshAfterEdit();
    }

    [RelayCommand]
    private void DeleteKey() => DeleteKeyCore(all: false);

    [RelayCommand]
    private void DeleteKeyAll() => DeleteKeyCore(all: true);

    private void DeleteKeyCore(bool all)
    {
        if (_workspace is null || SelectedKey is null)
        {
            StatusMessage = "Seleziona una chiave da eliminare.";
            return;
        }

        var targets = ResolveTargets(all, SelectedKey.Key);
        if (targets.Count == 0)
        {
            StatusMessage = "Nessun file di destinazione: seleziona i file (o usa 'da tutti').";
            return;
        }

        int changed = ConfigOperations.DeleteKey(_workspace, SelectedKey.Key, targets);
        Log($"Eliminata chiave '{SelectedKey.Key}' da {changed} file.");
        RefreshAfterEdit();
    }

    private List<string> ResolveTargets(bool all, string? key)
    {
        if (_workspace is null)
            return new List<string>();

        IEnumerable<string> pool;
        if (all)
        {
            pool = _workspace.Files.Select(f => f.Path);
        }
        else
        {
            var checkedPaths = CheckedLeaves().Select(n => n.File!.Path).ToList();
            if (checkedPaths.Count > 0)
            {
                pool = checkedPaths;
            }
            else if (SelectedNode?.File is { } current)
            {
                pool = new[] { current.Path };
            }
            else
            {
                // Nessuna selezione esplicita: non tocchiamo nulla per evitare
                // di propagare per errore su tutti i file.
                return new List<string>();
            }
        }

        if (key is not null)
        {
            var containing = _workspace.FilesContainingKey(key).ToHashSet(StringComparer.Ordinal);
            pool = pool.Where(containing.Contains);
        }

        return pool.Distinct(StringComparer.Ordinal).ToList();
    }

    // ---------------------------------------------------------------- Salvataggio

    [RelayCommand]
    private void Save()
    {
        if (_workspace is null)
            return;

        var pending = _workspace.DirtyDocuments()
            .OrderBy(d => d.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (pending.Count == 0)
        {
            StatusMessage = "Nessuna modifica da salvare.";
            return;
        }

        // Backup unico (zip) di tutti i file che stanno per essere modificati,
        // così da non sporcare le cartelle con un .bak accanto a ogni file.
        string backup;
        try
        {
            backup = CreateBackupArchive(pending);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log($"Errore creando il backup: {ex.Message}. Salvataggio annullato.");
            StatusMessage = "Salvataggio annullato: backup non riuscito.";
            return;
        }

        int saved = 0;
        foreach (var doc in pending)
        {
            try
            {
                doc.Save();
                saved++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Log($"Errore salvando '{doc.Path}': {ex.Message}");
            }
        }

        Backups.Add(backup);
        Log($"Salvati {saved} file (backup: {Path.GetFileName(backup)}).");
        StatusMessage = $"Salvati {saved} file.";
        RefreshAfterEdit();
    }

    [RelayCommand]
    private void Discard()
    {
        if (_workspace is null)
            return;

        foreach (var doc in _workspace.Documents.Values)
            doc.Reload();

        _workspace.RebuildIndex();
        Log("Modifiche scartate: file ricaricati dal disco.");
        RefreshAfterEdit();
    }

    /// <summary>
    /// Crea nella cartella <c>Backups</c> (sotto la cartella base) un unico archivio
    /// zip con copia di tutti i file che stanno per essere salvati. Restituisce il
    /// percorso dell'archivio.
    /// </summary>
    private string CreateBackupArchive(IReadOnlyList<AppSettingsDocument> documents)
        => BackupArchive.Create(_workspace!.BasePath, documents.Select(d => d.Path).ToList());

    // ---------------------------------------------------------------- Refresh

    private void RefreshAfterEdit()
    {
        string? keep = SelectedKey?.Key;

        _workspace?.RebuildIndex();
        RefreshKeys();

        if (keep is not null)
            SelectedKey = Keys.FirstOrDefault(k => k.Key == keep);

        RefreshDetail();
        RefreshPendingChanges();
    }

    private void RefreshPendingChanges()
    {
        Changes.Clear();
        if (_workspace is null)
        {
            ModifiedCount = 0;
            return;
        }

        foreach (var doc in _workspace.DirtyDocuments().OrderBy(d => d.Path, StringComparer.OrdinalIgnoreCase))
        {
            var diff = TextDiff.Diff(doc.OriginalText, doc.Text);
            int added = diff.Count(d => d.Kind == DiffKind.Added);
            int removed = diff.Count(d => d.Kind == DiffKind.Removed);
            Changes.Add($"{Relative(doc.Path)}    +{added} / -{removed}");
        }

        ModifiedCount = Changes.Count;
        HasChanges = ModifiedCount > 0;
        StatusMessage = ModifiedCount == 0
            ? "Nessuna modifica in sospeso."
            : $"{ModifiedCount} file con modifiche non salvate.";
    }

    private string Relative(string path)
        => string.IsNullOrEmpty(BasePath) ? path : Path.GetRelativePath(BasePath, path);

    private void Log(string message)
    {
        Logs.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {message}");
        while (Logs.Count > 300)
            Logs.RemoveAt(Logs.Count - 1);
    }
}
