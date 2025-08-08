using Agent.Framework;

namespace Agent.Runtime.Services;

public class CustomAgentFileService : ICustomAgentFileService
{
    private readonly TaskCompletionSource<CustomAgentFiles?> _tcs = new();

    public async Task<CustomAgentFiles?> GetFilesAsync()
    {
        return await _tcs.Task;
    }

    public CustomAgentFiles? GetDownloadedFiles()
    {
        return _tcs.Task.IsCompleted ? _tcs.Task.Result : null;
    }

    public bool IsReady => _tcs.Task.IsCompleted;

    internal void SetFiles(CustomAgentFiles? files)
    {
        _tcs.TrySetResult(files);
    }

    internal void SetError(Exception ex)
    {
        _tcs.TrySetResult(null);
    }
}
