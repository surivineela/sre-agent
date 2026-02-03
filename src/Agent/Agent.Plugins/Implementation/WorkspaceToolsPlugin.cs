// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;
using Agent.Common.ApiModels;
using Agent.Common.Services;
using Agent.Core;
using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Core.Models;
using Agent.Core.Services;
using Agent.Framework;
using Agent.Plugins.Services;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;

namespace Agent.Plugins.Implementation;

/// <summary>
/// Implementation of VS Code-like agent tools and ambient context provider.
/// Clones behavior from CopilotChat Node.js implementation.
/// </summary>
public partial class WorkspaceToolsPlugin : IWorkspaceToolsPlugin, IAmbientContextProvider, IDisposable
{
    private readonly ILogger<WorkspaceToolsPlugin> _logger;
    private readonly IFileTool _fileTool;
    private readonly IBashTool _bashTool;
    private readonly ISandboxPaths _sandboxPaths;
    private readonly IWorkspaceContext _workspaceContext;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly bool _useAdcRemoteWorkspace;

    // Todo list storage keyed by ThreadId (persisted across requests for the same thread)
    private static readonly ConcurrentDictionary<Guid, List<WorkspaceTodoItem>> _todoLists = new();

    /// <summary>
    /// Gets the current thread ID from ThreadContextAccessor, or falls back to Guid.Empty if not set.
    /// </summary>
    private static Guid CurrentThreadKey => ThreadContextAccessor.CurrentThreadId ?? Guid.Empty;

    /// <inheritdoc />
    public bool Enabled { get; }

    public WorkspaceToolsPlugin(
        ILogger<WorkspaceToolsPlugin> logger,
        IHttpClientFactory httpClientFactory,
        ExperimentalSettings experimentalSettings,
        IExperimentLoader experimentLoader,
        CoreSettings coreSettings,
        ISandboxPaths sandboxPaths,
        AdcRemoteWorkspaceService adcRemoteWorkspaceService)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _useAdcRemoteWorkspace = coreSettings.Azure.Adc.Enabled;

        // Enable if either the setting is true OR the experiment feature flag is enabled
        var enabled = experimentalSettings.EnableWorkspaceTools
            || experimentLoader.IsFeatureFlagEnabled(Constants.FeatureFlags.EnableWorkspaceTools);
        Enabled = enabled;

        // Choose implementation based on ADC configuration
        if (_useAdcRemoteWorkspace)
        {
            // ADC mode: use remote workspace service (lazy per-thread init)
            _fileTool = adcRemoteWorkspaceService;
            _bashTool = adcRemoteWorkspaceService;
            _sandboxPaths = adcRemoteWorkspaceService;
            _workspaceContext = adcRemoteWorkspaceService;
        }
        else
        {
            // Local mode: use local implementations via DI-provided sandbox paths
            _sandboxPaths = sandboxPaths;
            // LocalSandboxPaths.GetSandboxPathsAsync returns synchronously via Task.FromResult,
            // so this is safe to call synchronously here for local mode only
            var sandboxRoot = sandboxPaths.GetSandboxPathsAsync().GetAwaiter().GetResult().SandboxRoot;
            _fileTool = new LocalFileTools(logger, sandboxRoot);
            _bashTool = new LocalBashTools(logger, sandboxRoot);
            _workspaceContext = new LocalWorkspaceContext(logger, sandboxPaths);
        }
    }

    #region State Accessors (for IAmbientContextProvider)

    /// <inheritdoc />
    public IReadOnlyList<WorkspaceTodoItem>? GetTodoList()
    {
        var threadKey = ThreadContextAccessor.CurrentThreadId ?? Guid.Empty;
        return _todoLists.TryGetValue(threadKey, out var todos) ? todos : null;
    }

    /// <inheritdoc />
    public string GetTerminalStateForContext() => _bashTool.GetTerminalStateForContext();

    /// <inheritdoc />
    public Task<string> GetInstructionsContextAsync(CancellationToken ct = default)
        => _workspaceContext.GetInstructionsContextAsync(ct);

    /// <inheritdoc />
    public Task<string> GetEnvironmentContextAsync(CancellationToken ct = default)
        => _workspaceContext.GetEnvironmentContextAsync(ct);

    /// <inheritdoc />
    public Task<string> GetPreUserQueryContextAsync(CancellationToken ct = default)
    {
        var terminalState = GetTerminalStateForContext();
        var todoList = GetTodoList();
        var todoListDto = todoList?.Select(t => new WorkspaceTodoItemDto(t.Id, t.Title, t.Description, t.Status)).ToList();
        return _workspaceContext.GetPreUserQueryContextAsync(terminalState, todoListDto, ct);
    }

    #endregion

    #region File Operations

    public Task<string> ReadFileAsync(string filePath, int startLine, int endLine)
        => _fileTool.ReadFileAsync(filePath, startLine, endLine);

    public Task<string> CreateFileAsync(string filePath, string content)
        => _fileTool.CreateFileAsync(filePath, content);

    public Task<string> CreateDirectoryAsync(string dirPath)
        => _fileTool.CreateDirectoryAsync(dirPath);

    public Task<string> ListDirectoryAsync(string path)
        => _fileTool.ListDirectoryAsync(path);

    #endregion

    #region Edit Operations

    public Task<string> ReplaceStringInFileAsync(string filePath, string oldString, string newString)
        => _fileTool.ReplaceStringInFileAsync(filePath, oldString, newString);

    public async Task<string> MultiReplaceStringInFileAsync(string explanation, ReplaceOperation[] replacements)
    {
        if (replacements == null || replacements.Length == 0)
        {
            return "Tool call failed: No replacement operations provided";
        }

        return await _fileTool.MultiReplaceStringInFileAsync(explanation, replacements);
    }

    #endregion

    #region Search Operations

    public Task<string> FileSearchAsync(string query, int? maxResults = null)
        => _fileTool.FileSearchAsync(query, maxResults);

    public Task<string> GrepSearchAsync(string query, bool isRegexp, string? includePattern = null,
                                         int? maxResults = null, bool includeIgnoredFiles = false)
        => _fileTool.GrepSearchAsync(query, isRegexp, includePattern, maxResults, includeIgnoredFiles);

    #endregion

    #region Terminal Operations

    public Task<string> RunInTerminalAsync(string command, string explanation, bool isBackground)
        => _bashTool.RunInTerminalAsync(command, explanation, isBackground);

    public Task<string> GetTerminalLastCommandAsync()
        => _bashTool.GetTerminalLastCommandAsync();

    public Task<string> GetBackgroundTaskOutputAsync(string taskId, bool block = true, int timeout = 30000)
    {
        if (string.IsNullOrEmpty(taskId))
        {
            return Task.FromResult("Tool call failed: taskId is required");
        }

        return _bashTool.GetBackgroundTaskOutputAsync(taskId, block, timeout);
    }

    #endregion

    #region Task Management

    public Task<string> ManageTodoListAsync(string operation, WorkspaceTodoItem[]? todoList = null)
    {
        try
        {
            if (string.Equals(operation, "read", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(HandleTodoRead());
            }

            if (string.Equals(operation, "write", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(HandleTodoWrite(todoList));
            }

            return Task.FromResult($"Tool call failed: Invalid operation: {operation}. Must be 'read' or 'write'.");
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error managing todo list");
            return Task.FromResult($"Tool call failed: {ex.Message}");
        }
    }

    private string HandleTodoRead()
    {
        var threadKey = CurrentThreadKey;
        if (!_todoLists.TryGetValue(threadKey, out var todos) || todos.Count == 0)
        {
            return "No todo list found.";
        }

        return FormatTodoListAsMarkdown(todos);
    }

    private string HandleTodoWrite(WorkspaceTodoItem[]? todoList)
    {
        // Match VS Code: todoList is required for write operation
        if (todoList == null)
        {
            return "Tool call failed: todoList is required for write operation";
        }

        var warnings = new List<string>();

        // Validate and warn as per VS Code implementation
        if (todoList.Length < 3)
        {
            warnings.Add("Warning: Small todo list (<3 items). This task might not need a todo list.");
        }
        else if (todoList.Length > 10)
        {
            warnings.Add("Warning: Large todo list (>10 items). Consider keeping the list focused and actionable.");
        }

        var inProgressCount = todoList.Count(t => t.Status == "in-progress");
        if (inProgressCount > 1)
        {
            warnings.Add("Warning: Multiple todos marked as in-progress. Consider focusing on one at a time.");
        }

        // Validate sequential completion order
        for (int i = 0; i < todoList.Length; i++)
        {
            var current = todoList[i];

            // Warn if not-started task has any subsequent task that's started
            if (current.Status == "not-started")
            {
                var nextStartedTask = todoList.Skip(i + 1).FirstOrDefault(t => t.Status != "not-started");
                if (nextStartedTask != null)
                {
                    warnings.Add($"Warning: Todo #{current.Id} is 'not-started' but todo #{nextStartedTask.Id} is '{nextStartedTask.Status}'. Complete previous steps first, or update todos if step #{current.Id} is no longer needed.");
                }
            }

            // Warn if in-progress task has any subsequent task that's not not-started
            if (current.Status == "in-progress")
            {
                var nextStartedTask = todoList.Skip(i + 1).FirstOrDefault(t => t.Status != "not-started");
                if (nextStartedTask != null)
                {
                    warnings.Add($"Warning: Todo #{current.Id} is 'in-progress' but todo #{nextStartedTask.Id} is '{nextStartedTask.Status}'. Complete all tasks sequentially.");
                }
            }
        }

        var threadKey = CurrentThreadKey;

        // Check for many simultaneous updates
        if (_todoLists.TryGetValue(threadKey, out var existing) && existing.Count > 0)
        {
            var changedCount = CalculateTodoChanges(existing, todoList.ToList());
            if (changedCount > 3)
            {
                warnings.Add("Warning: Did you mean to update so many todos at the same time? Consider working on them one by one.");
            }
        }

        _todoLists[threadKey] = todoList.ToList();

        var result = "Successfully wrote todo list";
        if (warnings.Count > 0)
        {
            result += "\n\n" + string.Join("\n", warnings);
        }

        return result;
    }

    /// <summary>
    /// Calculate number of changes between old and new todo lists.
    /// Matches VS Code implementation: compares index-by-index.
    /// </summary>
    private static int CalculateTodoChanges(List<WorkspaceTodoItem> oldList, List<WorkspaceTodoItem> newList)
    {
        // Assume arrays are equivalent in order; compare index-by-index
        var modified = 0;
        var minLen = Math.Min(oldList.Count, newList.Count);

        for (var i = 0; i < minLen; i++)
        {
            var o = oldList[i];
            var n = newList[i];
            if (o.Title != n.Title ||
                (o.Description ?? "") != (n.Description ?? "") ||
                o.Status != n.Status)
            {
                modified++;
            }
        }

        var added = Math.Max(0, newList.Count - oldList.Count);
        var removed = Math.Max(0, oldList.Count - newList.Count);
        var totalChanges = added + removed + modified;
        return totalChanges;
    }

    private static string FormatTodoListAsMarkdown(List<WorkspaceTodoItem> todos)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Todo List");
        sb.AppendLine();

        foreach (var todo in todos.OrderBy(t => t.Id))
        {
            var checkbox = todo.Status switch
            {
                "completed" => "[x]",
                "in-progress" => "[-]",
                _ => "[ ]"
            };

            sb.AppendLine($"- {checkbox} {todo.Title}");
            if (!string.IsNullOrWhiteSpace(todo.Description))
            {
                sb.AppendLine($"  - {todo.Description}");
            }
        }

        return sb.ToString().Trim();
    }

    #endregion

    #region Web Operations

    public async Task<string> FetchWebpageAsync(string[] urls, string query)
    {
        if (urls == null || urls.Length == 0)
        {
            return "Tool call failed: No URLs provided";
        }

        var results = new List<string>();

        foreach (var url in urls)
        {
            try
            {
                var content = await FetchSinglePageAsync(url);
                results.Add($"Content from {url}:\n{content}");
            }
            catch (Exception ex)
            {
                results.Add($"Content from {url}:\nError fetching page: {ex.Message}");
            }
        }

        return string.Join("\n\n---\n\n", results);
    }

    private async Task<string> FetchSinglePageAsync(string url)
    {
        using var httpClient = _httpClientFactory.CreateClient();
        var response = await httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync();

        // Parse HTML and extract main content
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        // Remove script and style elements
        var nodesToRemove = doc.DocumentNode.SelectNodes("//script|//style|//nav|//header|//footer|//aside");
        if (nodesToRemove != null)
        {
            foreach (var node in nodesToRemove.ToList())
            {
                node.Remove();
            }
        }

        // Try to find main content area
        var mainContent = doc.DocumentNode.SelectSingleNode("//main") ??
                          doc.DocumentNode.SelectSingleNode("//article") ??
                          doc.DocumentNode.SelectSingleNode("//div[@id='content']") ??
                          doc.DocumentNode.SelectSingleNode("//div[@class='content']") ??
                          doc.DocumentNode.SelectSingleNode("//body");

        if (mainContent == null)
        {
            return "Could not extract content from page";
        }

        // Get text content and clean it up
        var text = mainContent.InnerText;

        // Clean up whitespace
        text = Regex.Replace(text, @"\s+", " ");
        text = text.Trim();

        // Truncate if too long (10KB limit for web content)
        const int maxLength = 10 * 1024;
        if (text.Length > maxLength)
        {
            text = text.Substring(0, maxLength) + "\n\n[Content truncated...]";
        }

        return text;
    }

    #endregion

    public void Dispose()
    {
        _bashTool.Dispose();
        GC.SuppressFinalize(this);
    }
}
