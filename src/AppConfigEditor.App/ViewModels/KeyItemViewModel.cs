using CommunityToolkit.Mvvm.ComponentModel;

namespace AppConfigEditor.App.ViewModels;

/// <summary>Una chiave nell'elenco centrale, con informazioni di diffusione e divergenza.</summary>
public partial class KeyItemViewModel : ViewModelBase
{
    public string Key { get; }

    [ObservableProperty]
    public partial int PresentCount { get; set; }

    [ObservableProperty]
    public partial bool IsDivergent { get; set; }

    [ObservableProperty]
    public partial string Preview { get; set; }

    public KeyItemViewModel(string key, int presentCount, bool isDivergent, string preview)
    {
        Key = key;
        PresentCount = presentCount;
        IsDivergent = isDivergent;
        Preview = preview;
    }
}
