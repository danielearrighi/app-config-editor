using System.IO.Compression;

namespace AppConfigEditor.Core;

/// <summary>
/// Crea nella cartella <c>Backups</c> (sotto la cartella base) un unico archivio
/// zip contenente copia dei file che stanno per essere modificati, mantenendo la
/// stessa struttura di sottocartelle. Evita di spargere file <c>.bak</c> accanto
/// agli originali.
/// </summary>
public static class BackupArchive
{
    public const string FolderName = "Backups";

    /// <summary>
    /// Crea l'archivio di backup per i percorsi indicati e ne restituisce il
    /// percorso completo. I nomi delle voci sono relativi a <paramref name="basePath"/>.
    /// </summary>
    public static string Create(string basePath, IReadOnlyList<string> filePaths)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(basePath);
        ArgumentNullException.ThrowIfNull(filePaths);

        string backupDir = Path.Combine(basePath, FolderName);
        Directory.CreateDirectory(backupDir);

        string archive = NextArchivePath(backupDir);

        using var zip = ZipFile.Open(archive, ZipArchiveMode.Create);
        foreach (string path in filePaths)
        {
            string entryName = Path.GetRelativePath(basePath, path).Replace('\\', '/');
            zip.CreateEntryFromFile(path, entryName, CompressionLevel.Optimal);
        }

        return archive;
    }

    private static string NextArchivePath(string backupDir)
    {
        string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        string archive = Path.Combine(backupDir, $"AppSettings-backup-{stamp}.zip");
        int counter = 1;
        while (File.Exists(archive))
            archive = Path.Combine(backupDir, $"AppSettings-backup-{stamp}-{counter++}.zip");

        return archive;
    }
}
