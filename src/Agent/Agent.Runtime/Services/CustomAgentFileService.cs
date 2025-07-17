using Agent.Framework;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.Services;

public class CustomAgentFileService : ICustomAgentFileService
{
    private readonly ILogger<CustomAgentFileService> _logger;
    private CustomAgentFiles? _cachedFiles;
    private readonly object _lock = new();
    private bool _isReady = false;

    public CustomAgentFileService(ILogger<CustomAgentFileService> logger)
    {
        _logger = logger;
    }

    public CustomAgentFiles? GetDownloadedFiles()
    {
        lock (_lock)
        {
            return _cachedFiles;
        }
    }

    public bool IsReady
    {
        get
        {
            lock (_lock)
            {
                return _isReady;
            }
        }
    }

    public async Task<CustomAgentFiles?> GetFilesAsync()
    {
        lock (_lock)
        {
            if (_isReady)
                return _cachedFiles;
        }

        // If not ready, wait a bit and try again (or implement proper async waiting)
        await Task.Delay(100);
        return GetDownloadedFiles();
    }

    public void SetFiles(CustomAgentFiles? files)
    {
        lock (_lock)
        {
            _cachedFiles = files;
            _isReady = true;
        }
        _logger.LogInternalInformation("Custom agent files loaded");
    }

    public void SetError(Exception ex)
    {
        lock (_lock)
        {
            _cachedFiles = null;
            _isReady = true; // Mark as ready even on error
        }
        _logger.LogInternalError(ex, "Failed to load custom agent files");
    }
}
