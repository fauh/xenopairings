namespace Xenopairings.Services.Backups;

public sealed class BackupSettings
{
    public const string SectionName = "Backup";

    /// <summary>
    /// URI of the Azure Blob Storage account, e.g. https://xenopairingsstorage.blob.core.windows.net
    /// Set via the Backup__StorageAccountUri environment variable / app setting.
    /// </summary>
    public string StorageAccountUri { get; init; } = string.Empty;

    /// <summary>
    /// Name of the blob container to upload backups into. Default: "backups".
    /// </summary>
    public string ContainerName { get; init; } = "backups";
}
