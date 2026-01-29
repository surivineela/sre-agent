using System.Runtime.InteropServices;
using Adc.RemoteWorkspace.Protocol;
using Agent.Common.Services;
using Grpc.Core;

namespace Agent.Adc.RemoteWorkspace.Services;

public class FileSystemService : FileSystem.FileSystemBase
{
    private readonly ILogger<FileSystemService> _logger;
    private readonly LocalFileTools _fileTools;

    public FileSystemService(ILogger<FileSystemService> logger)
    {
        _logger = logger;
        _fileTools = new LocalFileTools(logger, GetSandboxRoot());
    }

    private static string GetSandboxRoot()
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "sreagent", "terminalRoot");

        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }

        return path;
    }

    #region Workspace Initialization

    public override Task<InitializeSandboxRootResponse> InitializeSandboxRoot(InitializeSandboxRootRequest request, ServerCallContext context)
    {
        _logger.LogInformation("InitializeSandboxRoot");

        try
        {
            var sandboxRoot = GetSandboxRoot();
            var codeRefsPath = Path.Combine(sandboxRoot, "codeRefs");
            var tmpPath = Path.Combine(sandboxRoot, "tmp");

            // Ensure directories exist
            if (!Directory.Exists(codeRefsPath))
            {
                Directory.CreateDirectory(codeRefsPath);
                _logger.LogInformation("Created codeRefs directory: {CodeRefsPath}", codeRefsPath);
            }

            if (!Directory.Exists(tmpPath))
            {
                Directory.CreateDirectory(tmpPath);
                _logger.LogInformation("Created tmp directory: {TmpPath}", tmpPath);
            }

            return Task.FromResult(new InitializeSandboxRootResponse
            {
                SandboxRoot = sandboxRoot,
                CodeRefsPath = codeRefsPath,
                TmpPath = tmpPath
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize sandbox root");
            return Task.FromResult(new InitializeSandboxRootResponse
            {
                Error = $"Failed to initialize sandbox root: {ex.Message}"
            });
        }
    }

    #endregion

    #region File Operations

    public override async Task<ReadFileResponse> ReadFile(ReadFileRequest request, ServerCallContext context)
    {
        _logger.LogInformation("ReadFile: {FilePath}, StartLine: {StartLine}, EndLine: {EndLine}",
            request.FilePath, request.StartLine, request.EndLine);

        var result = await _fileTools.ReadFileAsync(request.FilePath, request.StartLine, request.EndLine);
        return new ReadFileResponse { Result = result };
    }

    public override async Task<CreateFileResponse> CreateFile(CreateFileRequest request, ServerCallContext context)
    {
        _logger.LogInformation("CreateFile: {FilePath}", request.FilePath);

        var result = await _fileTools.CreateFileAsync(request.FilePath, request.Content);
        return new CreateFileResponse { Result = result };
    }

    public override Task<CreateDirectoryResponse> CreateDirectory(CreateDirectoryRequest request, ServerCallContext context)
    {
        _logger.LogInformation("CreateDirectory: {DirPath}", request.DirPath);

        var result = _fileTools.CreateDirectory(request.DirPath);
        return Task.FromResult(new CreateDirectoryResponse { Result = result });
    }

    public override Task<ListDirectoryResponse> ListDirectory(ListDirectoryRequest request, ServerCallContext context)
    {
        _logger.LogInformation("ListDirectory: {Path}", request.Path);

        var result = _fileTools.ListDirectory(request.Path);
        return Task.FromResult(new ListDirectoryResponse { Result = result });
    }

    #endregion

    #region Edit Operations

    public override async Task<ReplaceStringInFileResponse> ReplaceStringInFile(ReplaceStringInFileRequest request, ServerCallContext context)
    {
        _logger.LogInformation("ReplaceStringInFile: {FilePath}", request.FilePath);

        var result = await _fileTools.ReplaceStringInFileAsync(request.FilePath, request.OldString, request.NewString);
        return new ReplaceStringInFileResponse { Result = result };
    }

    public override async Task<MultiReplaceStringInFileResponse> MultiReplaceStringInFile(MultiReplaceStringInFileRequest request, ServerCallContext context)
    {
        _logger.LogInformation("MultiReplaceStringInFile: {Explanation}, {Count} replacements",
            request.Explanation, request.Replacements.Count);

        var replacements = request.Replacements
            .Select(r => new Common.Services.ReplaceOperation
            {
                Explanation = r.Explanation,
                FilePath = r.FilePath,
                OldString = r.OldString,
                NewString = r.NewString
            })
            .ToArray();

        var result = await _fileTools.MultiReplaceStringInFileAsync(request.Explanation, replacements);
        return new MultiReplaceStringInFileResponse { Result = result };
    }

    #endregion

    #region Search Operations

    public override Task<FileSearchResponse> FileSearch(FileSearchRequest request, ServerCallContext context)
    {
        _logger.LogInformation("FileSearch: {Query}, MaxResults: {MaxResults}", request.Query, request.MaxResults);

        int? maxResults = request.MaxResults > 0 ? request.MaxResults : null;
        var result = _fileTools.FileSearch(request.Query, maxResults);
        return Task.FromResult(new FileSearchResponse { Result = result });
    }

    public override async Task<GrepSearchResponse> GrepSearch(GrepSearchRequest request, ServerCallContext context)
    {
        _logger.LogInformation("GrepSearch: {Query}, IsRegexp: {IsRegexp}, IncludePattern: {IncludePattern}",
            request.Query, request.IsRegexp, request.IncludePattern);

        int? maxResults = request.MaxResults > 0 ? request.MaxResults : null;
        string? includePattern = string.IsNullOrEmpty(request.IncludePattern) ? null : request.IncludePattern;

        var result = await _fileTools.GrepSearchAsync(
            request.Query,
            request.IsRegexp,
            includePattern,
            maxResults,
            request.IncludeIgnoredFiles);

        return new GrepSearchResponse { Result = result };
    }

    #endregion
}
