// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Collections.Concurrent;
using System.Text;
using Adc.RemoteWorkspace.Protocol;
using Agent.Common.Services;
using Agent.Core.Interfaces;
using Agent.Core.Services;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;

namespace Agent.Plugins.Services;

public class AdcRemoteWorkspaceService : IFileTool, IBashTool, ISandboxPaths, IWorkspaceContext
{
    private readonly ILogger<AdcRemoteWorkspaceService> _logger;
    private readonly IAgentSandboxService _sandboxService;
    private readonly ConcurrentDictionary<string, SessionClients> _clients = new();
    private readonly ConcurrentDictionary<string, SandboxPaths> _sandboxPaths = new();

    private record SessionClients(
        FileSystem.FileSystemClient FileSystem,
        Execution.ExecutionClient Execution,
        AmbientContext.AmbientContextClient AmbientContext,
        GrpcChannel Channel);

    /// <summary>
    /// Gets the current thread ID from ThreadContextAccessor, or falls back to Guid.Empty if not set.
    /// </summary>
    private static string CurrentThreadId => (ThreadContextAccessor.CurrentThreadId ?? Guid.Empty).ToString();

    /// <inheritdoc />
    public async Task<SandboxPaths> GetSandboxPathsAsync()
    {
        var threadId = CurrentThreadId;
        if (_sandboxPaths.TryGetValue(threadId, out var existing))
        {
            return existing;
        }

        var response = await InitializeSandboxRootAsync(threadId);
        if (!string.IsNullOrEmpty(response.Error))
        {
            throw new InvalidOperationException($"Failed to initialize sandbox paths for thread {threadId}: {response.Error}");
        }

        var paths = new SandboxPaths(response.SandboxRoot, response.CodeRefsPath, response.TmpPath, response.MemoriesPath);
        return _sandboxPaths.GetOrAdd(threadId, paths);
    }

    public AdcRemoteWorkspaceService(
        ILogger<AdcRemoteWorkspaceService> logger,
        IAgentSandboxService sandboxService)
    {
        _logger = logger;
        _sandboxService = sandboxService;
    }

    private async Task<SessionClients> GetClientsAsync(string threadId, CancellationToken ct)
    {
        if (_clients.TryGetValue(threadId, out var existing))
        {
            return existing;
        }

        var endpoint = await _sandboxService.GetSandboxEndpointAsync(threadId, ct);
        _logger.LogInternalInformation("Connecting to sandbox for thread {ThreadId} at {Endpoint}", threadId, endpoint);

        var channel = GrpcChannel.ForAddress(endpoint);

        var clients = new SessionClients(
            new FileSystem.FileSystemClient(channel),
            new Execution.ExecutionClient(channel),
            new AmbientContext.AmbientContextClient(channel),
            channel
        );

        return _clients.GetOrAdd(threadId, clients);
    }

    #region Workspace Initialization

    public async Task<InitializeSandboxRootResponse> InitializeSandboxRootAsync(string threadId, CancellationToken ct = default)
    {
        _logger.LogInternalInformation("InitializeSandboxRootAsync called for thread {ThreadId}", threadId);
        try
        {
            var clients = await GetClientsAsync(threadId, ct);
            var response = await clients.FileSystem.InitializeSandboxRootAsync(new InitializeSandboxRootRequest(), cancellationToken: ct);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "InitializeSandboxRootAsync failed for thread {ThreadId}", threadId);
            return new InitializeSandboxRootResponse
            {
                Error = $"Failed to initialize sandbox root: {ex.Message}"
            };
        }
    }

    #endregion

    #region File Operations (IFileTool)

    public async Task<string> ReadFileAsync(string filePath, int startLine, int endLine)
    {
        var threadId = CurrentThreadId;
        _logger.LogInternalInformation("ReadFileAsync called for thread {ThreadId}, path {Path}", threadId, filePath);
        try
        {
            var clients = await GetClientsAsync(threadId, CancellationToken.None);
            var response = await clients.FileSystem.ReadFileAsync(new ReadFileRequest
            {
                FilePath = filePath,
                StartLine = startLine,
                EndLine = endLine
            });
            return response.Result;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "ReadFileAsync failed for thread {ThreadId}, path {Path}", threadId, filePath);
            return $"Tool call failed: {ex.Message}";
        }
    }

    public async Task<string> CreateFileAsync(string filePath, string content)
    {
        var threadId = CurrentThreadId;
        _logger.LogInternalInformation("CreateFileAsync called for thread {ThreadId}, path {Path}", threadId, filePath);
        try
        {
            var clients = await GetClientsAsync(threadId, CancellationToken.None);
            var response = await clients.FileSystem.CreateFileAsync(new CreateFileRequest
            {
                FilePath = filePath,
                Content = content
            });
            return response.Result;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "CreateFileAsync failed for thread {ThreadId}, path {Path}", threadId, filePath);
            return $"Tool call failed: {ex.Message}";
        }
    }

    public async Task<string> CreateDirectoryAsync(string dirPath)
    {
        var threadId = CurrentThreadId;
        _logger.LogInternalInformation("CreateDirectoryAsync called for thread {ThreadId}, path {Path}", threadId, dirPath);
        try
        {
            var clients = await GetClientsAsync(threadId, CancellationToken.None);
            var response = await clients.FileSystem.CreateDirectoryAsync(new CreateDirectoryRequest
            {
                DirPath = dirPath
            });
            return response.Result;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "CreateDirectoryAsync failed for thread {ThreadId}, path {Path}", threadId, dirPath);
            return $"Tool call failed: {ex.Message}";
        }
    }

    public async Task<string> ListDirectoryAsync(string path)
    {
        var threadId = CurrentThreadId;
        _logger.LogInternalInformation("ListDirectoryAsync called for thread {ThreadId}, path {Path}", threadId, path);
        try
        {
            var clients = await GetClientsAsync(threadId, CancellationToken.None);
            var response = await clients.FileSystem.ListDirectoryAsync(new ListDirectoryRequest
            {
                Path = path
            });
            return response.Result;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "ListDirectoryAsync failed for thread {ThreadId}, path {Path}", threadId, path);
            return $"Tool call failed: {ex.Message}";
        }
    }

    #endregion

    #region Edit Operations (IFileTool)

    public async Task<string> ReplaceStringInFileAsync(string filePath, string oldString, string newString)
    {
        var threadId = CurrentThreadId;
        _logger.LogInternalInformation("ReplaceStringInFileAsync called for thread {ThreadId}, path {Path}", threadId, filePath);
        try
        {
            var clients = await GetClientsAsync(threadId, CancellationToken.None);
            var response = await clients.FileSystem.ReplaceStringInFileAsync(new ReplaceStringInFileRequest
            {
                FilePath = filePath,
                OldString = oldString,
                NewString = newString
            });
            return response.Result;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "ReplaceStringInFileAsync failed for thread {ThreadId}, path {Path}", threadId, filePath);
            return $"Tool call failed: {ex.Message}";
        }
    }

    public async Task<string> MultiReplaceStringInFileAsync(
        string explanation,
        Common.ApiModels.ReplaceOperation[] replacements)
    {
        var threadId = CurrentThreadId;
        _logger.LogInternalInformation("MultiReplaceStringInFileAsync called for thread {ThreadId}", threadId);
        try
        {
            var clients = await GetClientsAsync(threadId, CancellationToken.None);
            var request = new MultiReplaceStringInFileRequest
            {
                Explanation = explanation
            };
            foreach (var r in replacements)
            {
                request.Replacements.Add(new Adc.RemoteWorkspace.Protocol.ReplaceOperation
                {
                    Explanation = r.Explanation,
                    FilePath = r.FilePath,
                    OldString = r.OldString,
                    NewString = r.NewString
                });
            }
            var response = await clients.FileSystem.MultiReplaceStringInFileAsync(request);
            return response.Result;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "MultiReplaceStringInFileAsync failed for thread {ThreadId}", threadId);
            return $"Tool call failed: {ex.Message}";
        }
    }

    #endregion

    #region Search Operations (IFileTool)

    public async Task<string> FileSearchAsync(string query, int? maxResults = null)
    {
        var threadId = CurrentThreadId;
        _logger.LogInternalInformation("FileSearchAsync called for thread {ThreadId}, query {Query}", threadId, query);
        try
        {
            var clients = await GetClientsAsync(threadId, CancellationToken.None);
            var response = await clients.FileSystem.FileSearchAsync(new FileSearchRequest
            {
                Query = query,
                MaxResults = maxResults ?? 0
            });
            return response.Result;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "FileSearchAsync failed for thread {ThreadId}, query {Query}", threadId, query);
            return $"Tool call failed: {ex.Message}";
        }
    }

    public async Task<string> GrepSearchAsync(
        string query,
        bool isRegexp,
        string? includePattern = null,
        int? maxResults = null,
        bool includeIgnoredFiles = false)
    {
        var threadId = CurrentThreadId;
        _logger.LogInternalInformation("GrepSearchAsync called for thread {ThreadId}, query {Query}", threadId, query);
        try
        {
            var clients = await GetClientsAsync(threadId, CancellationToken.None);
            var response = await clients.FileSystem.GrepSearchAsync(new GrepSearchRequest
            {
                Query = query,
                IsRegexp = isRegexp,
                IncludePattern = includePattern ?? "",
                MaxResults = maxResults ?? 0,
                IncludeIgnoredFiles = includeIgnoredFiles
            });
            return response.Result;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "GrepSearchAsync failed for thread {ThreadId}, query {Query}", threadId, query);
            return $"Tool call failed: {ex.Message}";
        }
    }

    #endregion

    #region Terminal Operations (IBashTool)

    public async Task<string> RunInTerminalAsync(string command, string explanation, bool isBackground)
    {
        var threadId = CurrentThreadId;
        _logger.LogInternalInformation("RunInTerminalAsync called for thread {ThreadId}, command length {Length}, background {Background}", threadId, command.Length, isBackground);
        try
        {
            var clients = await GetClientsAsync(threadId, CancellationToken.None);
            var response = await clients.Execution.ExecuteBashAsync(new ExecuteBashRequest
            {
                Command = command,
                RunInBackground = isBackground,
                Timeout = 120000
            });

            if (!string.IsNullOrEmpty(response.Error))
            {
                return $"Tool call failed: {response.Error}";
            }

            if (isBackground)
            {
                return $"Background process started. TaskId: {response.SessionId}";
            }

            return FormatForegroundOutput(response.Stdout, response.ExitCode);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "RunInTerminalAsync failed for thread {ThreadId}", threadId);
            return $"Tool call failed: {ex.Message}";
        }
    }

    public Task<string> GetTerminalLastCommandAsync()
    {
        // Remote workspace does not track last command
        return Task.FromResult("Tool call failed: Last command tracking is not available for remote workspaces");
    }

    public async Task<string> GetBackgroundTaskOutputAsync(string taskId, bool block = true, int timeout = 30000)
    {
        if (string.IsNullOrEmpty(taskId))
        {
            return "Tool call failed: taskId is required";
        }

        var threadId = CurrentThreadId;
        _logger.LogInternalInformation("GetBackgroundTaskOutputAsync called for thread {ThreadId}, taskId {TaskId}", threadId, taskId);
        try
        {
            var clients = await GetClientsAsync(threadId, CancellationToken.None);
            var response = await clients.Execution.GetBashOutputAsync(new GetBashOutputRequest
            {
                TaskId = taskId,
                Block = block,
                Timeout = timeout
            });

            if (!string.IsNullOrEmpty(response.Error))
            {
                return $"Tool call failed: {response.Error}";
            }

            var result = new StringBuilder();
            if (!string.IsNullOrEmpty(response.Output))
            {
                result.AppendLine(response.Output);
            }

            if (response.ExitCode != 0)
            {
                result.AppendLine($"\nExit code: {response.ExitCode}");
            }

            if (response.Status == "running")
            {
                result.AppendLine("\nTask is still running.");
            }

            return result.Length > 0 ? result.ToString().Trim() : "No output available.";
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "GetBackgroundTaskOutputAsync failed for thread {ThreadId}, taskId {TaskId}", threadId, taskId);
            return $"Tool call failed: {ex.Message}";
        }
    }

    public string GetTerminalStateForContext()
    {
        // Remote workspace - return minimal state
        return "Remote terminal session active";
    }

    public async Task<KillTaskResponse> KillTaskAsync(string taskId, CancellationToken ct = default)
    {
        var threadId = CurrentThreadId;
        _logger.LogInternalInformation("KillTaskAsync called for thread {ThreadId}, taskId {TaskId}", threadId, taskId);
        var clients = await GetClientsAsync(threadId, ct);
        return await clients.Execution.KillTaskAsync(new KillTaskRequest
        {
            TaskId = taskId
        }, cancellationToken: ct);
    }

    private static string FormatForegroundOutput(string output, int exitCode)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return $"Command completed with exit code {exitCode}";
        }

        return $"{output}\n\nExit code: {exitCode}";
    }

    #endregion

    #region IWorkspaceContext Implementation

    /// <inheritdoc />
    public async Task<string> GetInstructionsContextAsync(CancellationToken ct = default)
    {
        var threadId = CurrentThreadId;
        _logger.LogInternalDebug("GetInstructionsContextAsync called for thread {ThreadId}", threadId);

        try
        {
            var clients = await GetClientsAsync(threadId, ct);
            var response = await clients.AmbientContext.GetInstructionsContextAsync(
                new GetInstructionsContextRequest(),
                cancellationToken: ct);

            if (!string.IsNullOrEmpty(response.Error))
            {
                _logger.LogInternalWarning("GetInstructionsContextAsync returned error for thread {ThreadId}: {Error}", threadId, response.Error);
                return string.Empty;
            }

            return response.Context;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "GetInstructionsContextAsync failed for thread {ThreadId}", threadId);
            return string.Empty;
        }
    }

    /// <inheritdoc />
    public async Task<string> GetEnvironmentContextAsync(CancellationToken ct = default)
    {
        var threadId = CurrentThreadId;
        _logger.LogInternalDebug("GetEnvironmentContextAsync called for thread {ThreadId}", threadId);

        try
        {
            var clients = await GetClientsAsync(threadId, ct);
            var response = await clients.AmbientContext.GetEnvironmentContextAsync(
                new GetEnvironmentContextRequest(),
                cancellationToken: ct);

            if (!string.IsNullOrEmpty(response.Error))
            {
                _logger.LogInternalWarning("GetEnvironmentContextAsync returned error for thread {ThreadId}: {Error}", threadId, response.Error);
                return string.Empty;
            }

            return response.Context;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "GetEnvironmentContextAsync failed for thread {ThreadId}", threadId);
            return string.Empty;
        }
    }

    /// <inheritdoc />
    public async Task<string> GetPreUserQueryContextAsync(
        string? terminalState,
        IReadOnlyList<WorkspaceTodoItemDto>? todoList,
        CancellationToken ct = default)
    {
        var threadId = CurrentThreadId;
        _logger.LogInternalDebug("GetPreUserQueryContextAsync called for thread {ThreadId}", threadId);

        try
        {
            var clients = await GetClientsAsync(threadId, ct);
            var request = new GetPreQueryContextRequest
            {
                TerminalState = terminalState ?? string.Empty
            };

            // Convert WorkspaceTodoItemDto to proto TodoItem
            if (todoList != null)
            {
                foreach (var todo in todoList)
                {
                    request.TodoList.Add(new TodoItem
                    {
                        Id = todo.Id,
                        Title = todo.Title,
                        Description = todo.Description ?? string.Empty,
                        Status = todo.Status
                    });
                }
            }

            var response = await clients.AmbientContext.GetPreQueryContextAsync(request, cancellationToken: ct);

            if (!string.IsNullOrEmpty(response.Error))
            {
                _logger.LogInternalWarning("GetPreUserQueryContextAsync returned error for thread {ThreadId}: {Error}", threadId, response.Error);
                return string.Empty;
            }

            return response.Context;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "GetPreUserQueryContextAsync failed for thread {ThreadId}", threadId);
            return string.Empty;
        }
    }

    #endregion

    public void Dispose()
    {
        foreach (var client in _clients.Values)
        {
            client.Channel.Dispose();
        }
        _clients.Clear();
    }
}
