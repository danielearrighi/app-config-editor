using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using AppConfigEditor.App.ViewModels;
using AppConfigEditor.App.Views;

namespace AppConfigEditor.App;

public partial class App : Application
{
    // Su Linux Avalonia usa di default subpixel-antialiasing, che su questo stack
    // produce frange RGB e testo percepito come sfocato. Forziamo antialiasing in
    // scala di grigi + nessun hinting + baseline allineato al pixel per un testo
    // nitido e correttamente antialiasato anche in grassetto.
    // Su Windows/macOS lasciamo il default di sistema.
    private static bool UseCrispText => OperatingSystem.IsLinux();

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        if (!UseCrispText)
            return;

        // I popup (tooltip/menu) vivono in un TopLevel separato e non ereditano le
        // opzioni testuali impostate sulla Window: le applichiamo al loro contenuto.
        Popup.IsOpenProperty.Changed.AddClassHandler<Popup>(static (popup, e) =>
        {
            if (e.NewValue is true && popup.Child is { } child)
                ApplyCrispText(child);
        });
    }

    private static void ApplyCrispText(Visual visual)
    {
        // Grayscale antialiasing: niente frange RGB.
        TextOptions.SetTextRenderingMode(visual, TextRenderingMode.Antialias);
        // Nessun hinting: lo "Strong" di Avalonia su FreeType diventa
        // FT_LOAD_TARGET_MONO, che incastra i glifi sulla griglia dei pixel e fa
        // apparire il grassetto senza antialiasing. Senza hinting gli spigoli
        // restano antialiasati (il testo normale non peggiora).
        TextOptions.SetTextHintingMode(visual, TextHintingMode.Light);
        TextOptions.SetBaselinePixelAlignment(visual, BaselinePixelAlignment.Aligned);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new MainWindow
            {
                DataContext = new MainViewModel(),
            };

            if (UseCrispText)
                ApplyCrispText(window);

            desktop.MainWindow = window;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
