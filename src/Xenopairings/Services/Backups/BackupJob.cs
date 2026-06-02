using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Xenopairings.Services.Backups;

/// <summary>
/// Hangfire job that copies the live SQLite database file to Azure Blob Storage.
///
/// Scheduled nightly at 03:00 UTC. Uses DefaultAzureCredential (managed identity
/// on Azure App Service, local developer credentials in dev) — no stored secret.
///
/// The destination blob is named  xenopairings-{yyyy-MM-dd}.db  so blobs are naturally
/// sorted by date and old ones can be cleaned up by a Blob lifecycle rule.
/// </summary>
public sealed class BackupJob
{
    private readonly BackupSettings _settings;
    private readonly ILogger<BackupJob> _logger;

    // DATA_DIR is injected so we know where the live database file lives.
    // Matches the env var that Program.cs uses to override the connection string.
    private readonly string _dataDir;

    public BackupJob(
        IOptions<BackupSettings> settings,
        ILogger<BackupJob> logger,
        string? dataDir = null)
    {
        _settings = settings.Value;
        _logger = logger;
        _dataDir = dataDir ?? Environment.GetEnvironmentVariable("DATA_DIR") ?? string.Empty;
    }

    /// <summary>
    /// Entry point called by Hangfire. Safe to call manually for a one-shot backup.
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.StorageAccountUri))
        {
            _logger.LogWarning("BackupJob skipped: Backup:StorageAccountUri is not configured.");
            return;
        }

        if (string.IsNullOrWhiteSpace(_dataDir))
        {
            _logger.LogWarning("BackupJob skipped: DATA_DIR is not set — database path is unknown.");
            return;
        }

        var dbPath = Path.Combine(_dataDir, "xenopairings.db");
        if (!File.Exists(dbPath))
        {
            _logger.LogWarning("BackupJob skipped: database file not found at {DbPath}.", dbPath);
            return;
        }

        var blobName = $"xenopairings-{DateTimeOffset.UtcNow:yyyy-MM-dd}.db";
        _logger.LogInformation("BackupJob starting: {DbPath} → {BlobName}", dbPath, blobName);

        try
        {
            var serviceUri = new Uri(_settings.StorageAccountUri);
            var credential = new DefaultAzureCredential();
            var blobServiceClient = new BlobServiceClient(serviceUri, credential);
            var containerClient = blobServiceClient.GetBlobContainerClient(_settings.ContainerName);

            // Read the database file into memory before uploading so we hold the
            // file open for the minimum possible time (SQLite may still be writing).
            var dbBytes = await File.ReadAllBytesAsync(dbPath, cancellationToken);

            using var stream = new MemoryStream(dbBytes, writable: false);
            await containerClient.UploadBlobAsync(blobName, stream, cancellationToken);

            _logger.LogInformation(
                "BackupJob complete: {Bytes} bytes uploaded to {Container}/{BlobName}.",
                dbBytes.Length, _settings.ContainerName, blobName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BackupJob failed for {BlobName}.", blobName);
            throw;  // Let Hangfire record the failure and retry if configured.
        }
    }
}
