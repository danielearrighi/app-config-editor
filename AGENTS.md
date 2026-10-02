# AGENTS.md — AppConfig Editor

Istruzioni persistenti per gli agenti che lavorano su questo repository.

## Cos'è

App desktop **cross-platform** (Linux/Windows, macOS opzionale) per modificare in
contemporanea più file `AppSettings.config` (uno per tenant) che condividono le
stesse chiavi. Modificando una chiave, il valore può essere propagato ai file che
la contengono; si possono anche aggiungere/eliminare chiavi su singoli file o in bulk.

## Stack

- **.NET 10 LTS** (`net10.0`), C#.
- **Avalonia 12.1.x** + `CommunityToolkit.Mvvm` (MVVM) per la UI.
- Test con **xUnit**.
- UI in tema scuro con accento viola (`#7C5CFF`), font Inter.

## Ambiente

Il progetto richiede la **SDK 10**. La SDK **10.0.401** è disponibile sia in
`~/.dotnet` sia a livello di sistema (`/usr/share/dotnet`). `global.json` nel
root fissa la versione (`10.0.401`, `rollForward: latestFeature`), così la build
non ricade per errore su una SDK precedente.

Se un terminale o un IDE usa una SDK più vecchia, esporta la SDK utente prima di
qualsiasi comando:

```bash
export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$HOME/.dotnet:$PATH"
```

**VS Code / C# Dev Kit**: il PATH di VS Code **non include `~/.dotnet`**, quindi
C# Dev Kit usa il `dotnet` di sistema (`/usr/share/dotnet`). Gli errori
`NETSDK1045: The current .NET SDK does not support targeting .NET 10.0` che
compaiono nel pannello NuGet/Problemi indicano che lì è in uso una SDK 9. Rimedi:
assicurarsi che `/usr/bin/dotnet` sia ≥ 10, poi eseguire
`Developer: Reload Window` (i processi C# Dev Kit tengono in cache la SDK con cui
sono stati avviati). In alternativa, forzare il percorso in
`.vscode/settings.json`: `{ "dotnet.dotnetPath": "/usr/share/dotnet/dotnet" }`.

## Comandi

```bash
# build (la solution usa il nuovo formato .slnx)
dotnet build AppConfigEditor.slnx

# test
dotnet test AppConfigEditor.slnx

# avvio
dotnet run --project src/AppConfigEditor.App

# publish self-contained
dotnet publish src/AppConfigEditor.App -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true
dotnet publish src/AppConfigEditor.App -c Release -r win-x64   --self-contained -p:PublishSingleFile=true
```

Dopo ogni modifica a C#/XAML: esegui `dotnet build AppConfigEditor.slnx -c Release`
e `dotnet test AppConfigEditor.slnx`; entrambi devono essere **puliti (0 warning, 0 errori)**.

## Struttura

```
AppConfigEditor.slnx
global.json                 fissa la SDK a 10.0.401 (rollForward: latestFeature)
src/AppConfigEditor.Core/   logica pura, nessuna dipendenza UI (testabile)
  AppSettingsDocument.cs      parsing + scrittura chirurgica + add/remove
  ConfigWorkspace.cs          discovery ricorsiva + indice chiave -> file
  ConfigOperations.cs         set/add/delete con propagazione sui target
  TextDiff.cs                 diff per anteprima
  AppSettingsStore.cs         persistenza base path
src/AppConfigEditor.App/    UI Avalonia
  Views/MainWindow.axaml      layout master-detail
  ViewModels/MainViewModel.cs orchestrazione
  Styles/AppTheme.axaml       palette scura + icone + stili
tests/AppConfigEditor.Core.Tests/
```

Tutta la logica delicata sta in `Core` e deve restare **testabile e senza dipendenze UI**.
La UI (`App`) resta sottile e chiama `Core`.

## Invarianti da non rompere

1. **Scrittura chirurgica**: si modifica solo lo span del valore (`value="..."`) nel
   testo originale. Commenti (`<!--SECURITY-->`), indentazione a tab, ordine,
   newline (CRLF/LF), BOM e spaziatura strana (`value="x"/>` vs `value="x" />`)
   devono restare **byte-identici**. Mai ri-serializzare l'XML intero.
2. **Propagazione conservativa**: `SetValue`/`DeleteKey` toccano solo i file che
   **contengono già** la chiave. `AddKey` aggiunge solo ai file target esplicitamente
   selezionati.
3. **Nessuna selezione = nessuna modifica**: le azioni "ai selezionati" non devono
   ricadere su "tutti" se non c'è una selezione esplicita. Per il caso opposto
   esistono i comandi "a tutti"/"da tutti".
4. **Backup prima di salvare**: ogni file salvato riceve una copia
   `*.yyyyMMdd-HHmmss.bak`. Mai salvare senza backup.
5. **Entità XML**: usare `XmlValue.Encode/Decode` (gestisce `&amp; &lt; &gt; &quot;`
   e riferimenti numerici); non usare encoder HTML generici.
6. Solo file di nome **`AppSettings.config`** (esclusi `ConnectionStrings.config`,
   `.ps1`, `.sync`).

## Dati di test

`Configs/` contiene **configurazioni reali** (tenant RMB/RMI/RMB_DEV...) con
credenziali/URL. Durante lo sviluppo e nei test:

- **non** scrivere mai su questi file; usa copie in directory temporanea;
- non introdurre segreti nei test;
- i test devono essere autosufficienti (creano i propri file in `Path.GetTempPath()`).

## Convenzioni UI

- Testo in italiano.
- `TextBox.Watermark` è deprecato in Avalonia 12: usare `PlaceholderText`.
- Bindings compilati: ogni `DataTemplate` deve dichiarare `x:DataType`.
- La UI deve restare coerente con `Styles/AppTheme.axaml` (palette e classi
  `accent`, `ghost`, `icon`, `rail`, `section`, `secondary`, `hint`).
- Gli stati dei controlli (`:pressed`, `:pointerover`, `:disabled`, `:focus`)
  arrivano dal **`ControlTheme` di Fluent**. Per cambiarli aggiungi uno `Style`
  in `AppTheme.axaml` (ha priorità sul `ControlTheme`), es.
  `<Style Selector="Button:pressed"><Setter Property="RenderTransform" Value="none" /></Style>`.

## Note operative

- **Prima leggi il codice/tema, poi (forse) verifica a video.** Per capire il
  comportamento di default di un controllo Avalonia (es. perché a bottone premuto
  il testo si rimpicciolisce) la fonte primaria è il **`ControlTheme` di Fluent**:
  `AvaloniaUI/Avalonia` → `src/Avalonia.Themes.Fluent/Controls/<Controllo>.xaml`
  (per il `Button`: `^:pressed → RenderTransform=scale(0.98)`). Non avviare l'app,
  non fare screenshot e non iniettare pseudo-classi per scoprirlo: è già scritto
  nel tema. Vale anche per i default di `TextBox`, `ComboBox`, `ListBox`, ecc.
- **Modifiche UI: nessuna verifica visiva richiesta.** Quando l'utente chiede un
  ritocco all'interfaccia, non serve avviare l'app né fare screenshot per
  controllare il risultato: l'utente verifica di persona. Limitarsi a
  `dotnet build -c Release` + `dotnet test` puliti (0 warning, 0 errori).
- Il progetto **non è un repo git** attualmente; `.gitignore` è già pronto.
- Le impostazioni dell'app sono in `~/.config/AppConfigEditor/settings.json`
  (Windows: `%APPDATA%\AppConfigEditor\settings.json`).
- Verifica grafica rapida su KDE Wayland: `spectacle -b -n -f -o /tmp/opencode/shot.png`
  mentre l'app è in esecuzione (display `:1`).
- Dopo un cambio/aggiornamento di SDK, se restano errori `NETSDK1045` o build
  incoerenti, esegui `dotnet build-server shutdown` per eliminare i nodi
  MSBuild/VBCSCompiler stale, poi riavvia l'IDE.
