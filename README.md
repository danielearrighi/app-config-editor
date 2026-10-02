# AppConfig Editor

Piccola app desktop per modificare **in contemporanea** più file `AppSettings.config`
(uno per tenant) che condividono le stesse chiavi.

Quando si modifica una chiave, il nuovo valore può essere propagato a tutti i file
che **contengono già** quella chiave; è inoltre possibile aggiungere o eliminare una
chiave su un singolo file oppure in bulk sui file selezionati.

## Funzionalità

- Ricerca **ricorsiva** dei soli file `AppSettings.config` a partire da una cartella base configurabile.
- UI **master-detail** in tema scuro: albero dei tenant, elenco chiavi, pannello dettaglio.
- **Divergenze**: le chiavi con valori diversi tra i file vengono evidenziate.
- Operazioni:
  - modifica del valore (singolo file, file selezionati o tutti);
  - **aggiunta** nuova chiave (ai file selezionati);
  - **eliminazione** chiave (dai file selezionati o da tutti).
- **Scrittura chirurgica**: viene modificato solo lo span dell'attributo `value`; commenti,
  indentazione, ordine e spaziatura del file restano identici.
- **Backup automatico** (`.AAAA...HHmmss.bak`) prima di ogni salvataggio, con anteprima delle
  modifiche in sospeso nella tab *MODIFICHE*.
- Nessun masking dei valori.

## Requisiti

- **.NET 10 SDK**. Su questa macchina è installato in `~/.dotnet` (non nel sistema):

  ```bash
  export DOTNET_ROOT="$HOME/.dotnet"
  export PATH="$HOME/.dotnet:$PATH"
  ```

- Linux con un ambiente grafico (testato su KDE Plasma Wayland). Windows e macOS supportati da Avalonia.

## Compilare, eseguire, testare

```bash
export DOTNET_ROOT="$HOME/.dotnet"; export PATH="$HOME/.dotnet:$PATH"

# build
dotnet build AppConfigEditor.slnx

# test del Core (parsing, propagazione, diff)
dotnet test AppConfigEditor.slnx

# avvio
dotnet run --project src/AppConfigEditor.App
```

La cartella base viene scelta con il pulsante **Sfoglia** e salvata in
`~/.config/AppConfigEditor/settings.json` (su Windows `%APPDATA%\AppConfigEditor\settings.json`).

## Publish (eseguibili autonomi)

```bash
# Windows (da eseguire su/ per Windows)
dotnet publish src/AppConfigEditor.App -c Release -r win-x64 --self-contained \
  -p:PublishSingleFile=true

# Linux
dotnet publish src/AppConfigEditor.App -c Release -r linux-x64 --self-contained \
  -p:PublishSingleFile=true
```

### Integrazione desktop (Linux)

Il binario pubblicato ha già l'icona di finestra/taskbar. Per farlo comparire nel
**menu applicazioni** con la sua icona serve una voce `.desktop` e i PNG nel tema
hicolor: lo script `packaging/linux/install.sh` li installa per l'utente corrente.

```bash
# copia il binario in ~/.local/bin e registra icona + voce di menu
packaging/linux/install.sh publish/AppConfigEditor.App

# oppure, se l'eseguibile è già nel PATH:
packaging/linux/install.sh
```

Lo script scrive in `~/.local/share/icons/hicolor/*/apps/appconfigeditor.png` e in
`~/.local/share/applications/AppConfigEditor.desktop`.

## Struttura

```
AppConfigEditor.slnx
src/
  AppConfigEditor.Core/   logica pura e testabile (nessuna dipendenza UI)
    AppSettingsDocument.cs   parsing + scrittura chirurgica + add/remove
    ConfigWorkspace.cs       discovery ricorsiva + indice chiave -> file
    ConfigOperations.cs      set/add/delete con propagazione sui target
    TextDiff.cs              diff per l'anteprima
    AppSettingsStore.cs      persistenza base path
  AppConfigEditor.App/    UI Avalonia (MVVM, CommunityToolkit)
    Views/MainWindow.axaml
    ViewModels/MainViewModel.cs
    Styles/AppTheme.axaml    palette scura + accento viola
tests/
  AppConfigEditor.Core.Tests/
packaging/
  linux/                   icona + voce .desktop per il menu applicazioni
```

## Flusso tipico

1. **Sfoglia** → scegli la cartella che contiene i tenant.
2. Seleziona una chiave nell'elenco centrale (il badge *divergente* segnala valori diversi).
3. Spunta i file target nell'albero (oppure usa *Applica a tutti*).
4. Modifica il campo **VALORE** e premi **Applica ai selezionati** / **Applica a tutti**.
5. Controlla la tab **MODIFICHE**, poi **Salva** (viene creato un backup per ogni file).
