namespace AppConfigEditor.Core;

/// <summary>
/// Operazioni di modifica applicabili a uno o più file.
/// Le operazioni sono già "propaganti": si passano i percorsi target e
/// l'operazione tocca solo i file che soddisfano la condizione.
/// </summary>
public static class ConfigOperations
{
    /// <summary>Imposta il valore di una chiave su tutti i target che la contengono.</summary>
    public static int SetValue(ConfigWorkspace workspace, string key, string value, IEnumerable<string> targetPaths)
        => Apply(workspace, targetPaths, doc => doc.SetValue(key, value));

    /// <summary>Aggiunge la chiave ai target selezionati in cui non è presente.</summary>
    public static int AddKey(ConfigWorkspace workspace, string key, string value, IEnumerable<string> targetPaths)
        => Apply(workspace, targetPaths, doc => doc.AddEntry(key, value));

    /// <summary>Elimina la chiave dai target che la contengono.</summary>
    public static int DeleteKey(ConfigWorkspace workspace, string key, IEnumerable<string> targetPaths)
        => Apply(workspace, targetPaths, doc => doc.RemoveEntry(key));

    private static int Apply(ConfigWorkspace workspace, IEnumerable<string> targetPaths, Func<AppSettingsDocument, bool> action)
    {
        int changed = 0;
        foreach (string path in targetPaths.Distinct(StringComparer.Ordinal))
        {
            if (workspace.Documents.TryGetValue(path, out var doc) && action(doc))
                changed++;
        }

        if (changed > 0)
            workspace.RebuildIndex();

        return changed;
    }
}
