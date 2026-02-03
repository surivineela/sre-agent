// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Interfaces;
using Agent.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Agent.Core.Services;

/// <summary>
/// Background service that periodically uploads local memory files to remote blob storage.
/// This ensures that session insights, synthesized knowledge, and repo instructions
/// are synced to remote storage for persistence across container restarts.
/// </summary>
public class MemoryUploadBackgroundService : BackgroundService
{
    private readonly IAgentFileStorageService _fileStorageService;
    private readonly ILogger<MemoryUploadBackgroundService> _logger;

    /// <summary>
    /// Interval between upload cycles. Default is 5 minutes.
    /// </summary>
    private static readonly TimeSpan UploadInterval = TimeSpan.FromMinutes(5);

    public MemoryUploadBackgroundService(
        IAgentFileStorageService fileStorageService,
        ILogger<MemoryUploadBackgroundService> logger)
    {
        _fileStorageService = fileStorageService;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInternalInformation("Memory upload background service started. Upload interval: {Interval}", UploadInterval);

        // Wait a bit before the first upload to let the application fully start
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var uploadedCount = await _fileStorageService.UploadAllMemoriesAsync(stoppingToken);

                if (uploadedCount > 0)
                {
                    _logger.LogInternalInformation("Periodic memory upload completed. Uploaded {Count} file(s)", uploadedCount);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Expected during shutdown
                break;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Error during periodic memory upload");
            }

            try
            {
                await Task.Delay(UploadInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Expected during shutdown
                break;
            }
        }

        // Final upload before shutdown to ensure all changes are persisted
        try
        {
            _logger.LogInternalInformation("Performing final memory upload before shutdown...");
            var finalCount = await _fileStorageService.UploadAllMemoriesAsync(CancellationToken.None);
            _logger.LogInternalInformation("Final memory upload completed. Uploaded {Count} file(s)", finalCount);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error during final memory upload");
        }

        _logger.LogInternalInformation("Memory upload background service stopped");
    }
}
