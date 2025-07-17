using Agent.Framework;

namespace Agent.Runtime.Services;

public interface ICustomAgentFileService
{
    CustomAgentFiles? GetDownloadedFiles();
    bool IsReady { get; }
    Task<CustomAgentFiles?> GetFilesAsync();
}
