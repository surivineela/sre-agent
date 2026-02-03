// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Interfaces;
using Agent.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Agent.Core.Services;

/// <summary>
/// Background service that downloads workspace memory files from remote blob storage on startup.
/// Uploads are handled explicitly via the specific upload methods on IAgentFileStorageService.
/// </summary>
public class WorkspaceSyncService : IHostedService
{
    private readonly IAgentFileStorageService _fileStorageService;
    private readonly ILogger<WorkspaceSyncService> _logger;

    public WorkspaceSyncService(
        IAgentFileStorageService fileStorageService,
        ILogger<WorkspaceSyncService> logger)
    {
        _fileStorageService = fileStorageService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInternalInformation("Starting workspace sync service...");

        try
        {
            // Download all memory files from blob storage on startup
            var downloadedCount = await _fileStorageService.DownloadMemoryFilesAsync(cancellationToken);
            _logger.LogInternalInformation("Downloaded {Count} memory file(s) on startup", downloadedCount);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to download memory files on startup");
            // Don't throw - memories are optional and shouldn't block startup
        }

        _logger.LogInternalInformation("Workspace sync service started");
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInternalInformation("Stopped workspace sync service");
        return Task.CompletedTask;
    }
}
