using System.Linq;
using AppConfigEditor.App.ViewModels;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace AppConfigEditor.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private async void OnBrowseClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel)
            return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null)
            return;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Seleziona la cartella base che contiene i file AppSettings.config",
            AllowMultiple = false,
        });

        var folder = folders.FirstOrDefault();
        string? path = folder?.TryGetLocalPath();
        if (!string.IsNullOrEmpty(path))
            viewModel.LoadFromPath(path);
    }
}
