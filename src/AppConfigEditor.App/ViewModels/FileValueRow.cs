using AppConfigEditor.Core;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AppConfigEditor.App.ViewModels;

/// <summary>Riga del pannello dettaglio: valore della chiave selezionata in un file.</summary>
public partial class FileValueRow : ViewModelBase
{
    private readonly FileNode? _node;

    public ConfigFileInfo File { get; }

    [ObservableProperty]
    public partial string Value { get; set; }

    [ObservableProperty]
    public partial bool IsOutlier { get; set; }

    // Selezione del file: rispecchia (e aggiorna) la checkbox dell'albero a sinistra.
    [ObservableProperty]
    public partial bool IsFileSelected { get; set; }

    public string DisplayName => File.DisplayName;
    public string FullPath => File.Path;

    public FileValueRow(ConfigFileInfo file, string value, bool isOutlier, FileNode? node = null)
    {
        File = file;
        Value = value;
        IsOutlier = isOutlier;
        _node = node;
        IsFileSelected = node?.IsChecked ?? false;
    }

    partial void OnIsFileSelectedChanged(bool value)
    {
        if (_node is not null)
            _node.IsChecked = value;
    }
}
