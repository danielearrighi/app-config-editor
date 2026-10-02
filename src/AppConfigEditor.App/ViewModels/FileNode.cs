using System.Collections.ObjectModel;
using AppConfigEditor.Core;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AppConfigEditor.App.ViewModels;

/// <summary>Nodo dell'albero dei file (cartella del tenant oppure file AppSettings.config).</summary>
public partial class FileNode : ViewModelBase
{
    private readonly Action? _onCheckChanged;

    public string Name { get; }
    public string RelativePath { get; }
    public ConfigFileInfo? File { get; }
    public ObservableCollection<FileNode> Children { get; } = new();

    public bool IsFile => File is not null;
    public bool IsFolder => File is null;

    [ObservableProperty]
    public partial bool IsExpanded { get; set; } = true;

    [ObservableProperty]
    public partial bool IsChecked { get; set; }

    public FileNode(string name, string relativePath, ConfigFileInfo? file, Action? onCheckChanged = null)
    {
        Name = name;
        RelativePath = relativePath;
        File = file;
        _onCheckChanged = onCheckChanged;
    }

    partial void OnIsCheckedChanged(bool value) => _onCheckChanged?.Invoke();
}
