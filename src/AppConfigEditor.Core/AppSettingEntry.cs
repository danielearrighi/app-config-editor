namespace AppConfigEditor.Core;

/// <summary>
/// Una voce &lt;add key="..." value="..." /&gt; all'interno di un file AppSettings.config.
/// Vengono conservati gli offset nel testo originale per poter applicare
/// modifiche "chirurgiche" senza riscrivere tutto il file.
/// </summary>
/// <param name="Key">Nome della chiave (entità decodificata).</param>
/// <param name="Value">Valore attuale (entità decodificata).</param>
/// <param name="LineNumber">Numero di riga 1-based in cui inizia l'elemento.</param>
/// <param name="Start">Offset del carattere iniziale di &lt;add nel documento.</param>
/// <param name="End">Offset (esclusivo) successivo al '&gt;' dell'elemento.</param>
/// <param name="ValueStart">Offset dell'inizio del contenuto dell'attributo value (dopo l'apice).</param>
/// <param name="ValueEnd">Offset della fine del contenuto dell'attributo value (prima dell'apice).</param>
public sealed record AppSettingEntry(
    string Key,
    string Value,
    int LineNumber,
    int Start,
    int End,
    int ValueStart,
    int ValueEnd);
