namespace AppConfigEditor.Core;

/// <summary>Informazioni su un file AppSettings.config individuato su disco.</summary>
/// <param name="Path">Percorso assoluto del file.</param>
/// <param name="Tenant">Cartella relativa che identifica il tenant.</param>
/// <param name="RelativePath">Percorso relativo alla base path.</param>
public sealed record ConfigFileInfo(string Path, string Tenant, string RelativePath)
{
    /// <summary>Etichetta da mostrare in UI (tenant, oppure nome file se in radice).</summary>
    public string DisplayName => string.IsNullOrEmpty(Tenant) ? Path : Tenant;
}
