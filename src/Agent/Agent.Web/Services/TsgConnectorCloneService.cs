// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.RegularExpressions;
using Agent.Common.Services;
using Agent.Core;
using Agent.Core.Configuration;
using Agent.Core.Helpers;
using Agent.Data.DataModels;
using Agent.Data.Repositories;
using Agent.Framework;
using Agent.Plugins.Services;

namespace Agent.Web.Services;

/// <summary>
/// Background service that clones and syncs repositories (Azure DevOps and GitHub) for TSG connectors.
/// Uses a batch-with-debounce pattern: QueueCodeRepositoryUpdate() processes all connectors
/// that need work. If called while already running, sets a rerun flag.
/// </summary>
public class TsgConnectorCloneService : BackgroundService
{
    private readonly ITsgConnectorRepository _repository;
    private readonly TerminalSessionManager _terminalManager;
    private readonly ILogger<TsgConnectorCloneService> _logger;

    private readonly Lock _lock = new();
    private bool _isRunning;
    private bool _rerunRequested;

    private static readonly TimeSpan StaleThreshold = TimeSpan.FromDays(1);
    private static readonly TimeSpan MaintenanceInterval = TimeSpan.FromHours(1);
    private const int MaxParallelClones = 3;

    /// <summary>
    /// Represents the actual clone state observed from the filesystem.
    /// </summary>
    private record FilesystemState(
        CloneStatus Status,
        string? CommitHash,
        DateTime? CommitTime);

    public static string CodeRefsPath => new LocalSandboxPaths().SandboxPaths.CodeRefsPath;

    /// <summary>
    /// Gets whether this service is enabled. Cached once at startup based on workspace feature flag.
    /// </summary>
    public bool Enabled { get; }

    public TsgConnectorCloneService(
        ITsgConnectorRepository repository,
        ILogger<TsgConnectorCloneService> logger,
        ExperimentalSettings experimentalSettings,
        IExperimentLoader experimentLoader)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        var sandboxPaths = new LocalSandboxPaths();
        _terminalManager = new TerminalSessionManager(logger, sandboxPaths.GetSandboxPathsAsync().Result.SandboxRoot);
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Cache enabled state once - same pattern as WorkspaceToolsPlugin
        Enabled = experimentalSettings.EnableWorkspaceTools
            || experimentLoader.IsFeatureFlagEnabled(Constants.FeatureFlags.EnableWorkspaceTools);
    }

    /// <summary>
    /// Queue a full code repository update operation. Called by controller and timer.
    /// If already running, sets rerun flag and returns immediately.
    /// </summary>
    public void QueueCodeRepositoryUpdate(CancellationToken ct = default)
    {
        lock (_lock)
        {
            if (_isRunning)
            {
                _rerunRequested = true;
                _logger.LogInternalInformation($"Code repository update already running, will rerun after completion");
                return;
            }

            _isRunning = true;
        }

        _ = Task.Run(() => CodeRepositoryUpdateLoopAsync(ct), ct);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInternalInformation($"TsgConnectorCloneService starting, Enabled={Enabled}");

        if (!Enabled)
        {
            _logger.LogInternalInformation($"Workspace tools disabled, TsgConnectorCloneService will not run");
            return;
        }

        if (!Directory.Exists(CodeRefsPath))
        {
            Directory.CreateDirectory(CodeRefsPath);
            _logger.LogInternalInformation($"Created codeRefs directory: {CodeRefsPath}");
        }

        // Initial update (no delay)
        QueueCodeRepositoryUpdate(stoppingToken);

        // Hourly maintenance
        using var timer = new PeriodicTimer(MaintenanceInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                QueueCodeRepositoryUpdate(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInternalInformation($"TsgConnectorCloneService stopping");
        }
    }

    private async Task CodeRepositoryUpdateLoopAsync(CancellationToken ct)
    {
        try
        {
            while (true)
            {
                ct.ThrowIfCancellationRequested();

                await CodeRepositoryUpdateAsync(ct);

                lock (_lock)
                {
                    ct.ThrowIfCancellationRequested();

                    if (!_rerunRequested)
                    {
                        _isRunning = false;
                        return;
                    }

                    _rerunRequested = false;
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInternalInformation($"Code repository update cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"Code repository update failed");
        }
        finally
        {
            lock (_lock)
            {
                _isRunning = false;
            }
        }
    }

    private async Task CodeRepositoryUpdateAsync(CancellationToken cancellationToken)
    {
        var connectors = await _repository.GetAllAsync();

        // Phase 0: Reconcile document state with filesystem
        await ReconcileConnectorStatesAsync(connectors);

        cancellationToken.ThrowIfCancellationRequested();

        // Re-fetch to get updated states
        connectors = await _repository.GetAllAsync();

        // Clean up orphaned folders
        CleanupOrphanedCodeRefs(connectors);

        cancellationToken.ThrowIfCancellationRequested();

        var now = DateTime.UtcNow;

        // Phase 1: Needs clone (includes Failed for auto-retry)
        var needsClone = connectors
            .Where(c => c.Status == ConnectorStatus.Healthy
                        && c.CloneStatus is CloneStatus.NotStarted
                                         or CloneStatus.PendingCredentialUpdate
                                         or CloneStatus.Failed)
            .ToList();

        // Phase 2: Stale (>1 day), ordered by most stale first
        var stale = connectors
            .Where(c => c.Status == ConnectorStatus.Healthy &&
                        c.CloneStatus == CloneStatus.Ready &&
                        c.LastSuccessfulSync != null &&
                        (now - c.LastSuccessfulSync.Value) > StaleThreshold)
            .OrderBy(c => c.LastSuccessfulSync)
            .ToList();

        var toProcess = needsClone.Concat(stale).ToList();
        if (toProcess.Count == 0)
        {
            return;
        }

        _logger.LogInternalInformation($"Processing {toProcess.Count} connectors ({needsClone.Count} need clone, {stale.Count} stale)");

        await Parallel.ForEachAsync(toProcess,
            new ParallelOptions { MaxDegreeOfParallelism = MaxParallelClones, CancellationToken = cancellationToken },
            async (connector, ct) =>
            {
                var sessionId = Guid.NewGuid();
                try
                {
                    await ProcessConnectorAsync(connector, sessionId, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogInternalError(ex, $"Failed to process connector: {connector.Name}");
                }
                finally
                {
                    _terminalManager.DisposeSession(sessionId);
                }
            });
    }

    /// <summary>
    /// Removes codeRefs folders that don't have corresponding connectors.
    /// </summary>
    private void CleanupOrphanedCodeRefs(IReadOnlyList<TsgConnectorDocument> connectors)
    {
        if (!Directory.Exists(CodeRefsPath))
        {
            return;
        }

        var validFolderNames = connectors
            .Select(c => SanitizeFolderName(c.Name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var folderPath in Directory.GetDirectories(CodeRefsPath))
        {
            var folderName = Path.GetFileName(folderPath);

            if (!validFolderNames.Contains(folderName))
            {
                _logger.LogInternalWarning($"Found orphaned codeRefs folder: {folderName}, deleting");

                try
                {
                    ForceDeleteDirectory(folderPath);
                    _logger.LogInternalInformation($"Deleted orphaned folder: {folderPath}");
                }
                catch (Exception ex)
                {
                    _logger.LogInternalError(ex, $"Failed to delete orphaned folder: {folderPath}");
                }
            }
        }
    }

    private async Task ProcessConnectorAsync(
        TsgConnectorDocument connector, Guid sessionId, CancellationToken ct)
    {
        var localPath = Path.Combine(CodeRefsPath, SanitizeFolderName(connector.Name));
        var gitDirPath = Path.Combine(localPath, ".git");
        var isExisting = Directory.Exists(gitDirPath);

        // Clean up corrupt/incomplete directory before clone
        if (Directory.Exists(localPath) && !isExisting)
        {
            try
            {
                _logger.LogInternalInformation($"Cleaning up corrupt directory for {connector.Name}");
                ForceDeleteDirectory(localPath);
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, $"Failed to clean up directory for {connector.Name}");
                await _repository.UpdateCloneStatusAsync(connector.Name, CloneStatus.Failed, localPath,
                    errorMessage: $"Failed to clean up corrupt directory: {ex.Message}");
                return;
            }
        }

        // Determine the repository URL based on RepoType
        string repoUrl;
        if (connector.RepoType == RepoType.GitHub)
        {
            // GitHub URLs are used as-is
            repoUrl = connector.DataSource;
        }
        else
        {
            // Azure DevOps: validate URL format
            var (parsedUrl, _) = ParseAzureDevOpsUrl(connector.DataSource);
            if (string.IsNullOrEmpty(parsedUrl))
            {
                await _repository.UpdateCloneStatusAsync(connector.Name, CloneStatus.Failed,
                    errorMessage: "Invalid Azure DevOps URL");
                return;
            }
            repoUrl = parsedUrl;
        }

        var status = isExisting ? CloneStatus.Syncing : CloneStatus.Cloning;
        await _repository.UpdateCloneStatusAsync(connector.Name, status, localPath);

        bool success;
        string? error;

        if (isExisting)
        {
            (success, error) = await GitPullAsync(sessionId, localPath, connector.Pat, ct);
        }
        else
        {
            (success, error) = await GitCloneAsync(sessionId, repoUrl, localPath, connector.Pat, ct);
        }

        if (success)
        {
            await SetupGitCredentialsAsync(sessionId, localPath, repoUrl, connector.Pat, ct);
            var commitHash = await GetCommitHashAsync(sessionId, localPath, ct);
            await _repository.UpdateCloneStatusAsync(connector.Name, CloneStatus.Ready, localPath, commitHash);
            _logger.LogInternalInformation($"Successfully processed connector: {connector.Name}");
        }
        else
        {
            if (IsAuthError(error))
            {
                await _repository.UpdateStatusAsync(connector.Name, ConnectorStatus.Unhealthy,
                    "Authentication failed - PAT may be expired");
            }

            await _repository.UpdateCloneStatusAsync(connector.Name, CloneStatus.Failed, localPath,
                errorMessage: error);
            _logger.LogInternalError($"Failed to process connector: {connector.Name} - {error}");
        }
    }

    private async Task<(bool success, string? error)> GitCloneAsync(
        Guid sessionId, string repoUrl, string localPath, string? pat, CancellationToken ct)
    {
        var parentDir = Path.GetDirectoryName(localPath);
        if (!string.IsNullOrEmpty(parentDir) && !Directory.Exists(parentDir))
        {
            Directory.CreateDirectory(parentDir);
        }

        // Remove existing non-git directory
        if (Directory.Exists(localPath) && !Directory.Exists(Path.Combine(localPath, ".git")))
        {
            ForceDeleteDirectory(localPath);
        }

        var authUrl = BuildAuthenticatedUrl(repoUrl, pat);
        var cmd = $"git clone \"{authUrl}\" \"{localPath}\"";
        var (output, exitCode) = await _terminalManager.ExecuteCommandAsync(sessionId, cmd, ct);

        return exitCode == 0
            ? (true, null)
            : (false, RedactPat(output));
    }

    private async Task<(bool success, string? error)> GitPullAsync(
        Guid sessionId, string localPath, string? pat, CancellationToken ct)
    {
        // Update remote URL with current PAT before fetching
        if (!string.IsNullOrEmpty(pat))
        {
            var (currentUrl, _) = await _terminalManager.ExecuteCommandAsync(sessionId,
                $"git -C \"{localPath}\" config --get remote.origin.url", ct);

            if (!string.IsNullOrEmpty(currentUrl?.Trim()))
            {
                var authUrl = BuildAuthenticatedUrl(currentUrl.Trim(), pat);
                await _terminalManager.ExecuteCommandAsync(sessionId,
                    $"git -C \"{localPath}\" config remote.origin.url \"{authUrl}\"", ct);
            }
        }

        // Fetch
        var (output, exitCode) = await _terminalManager.ExecuteCommandAsync(sessionId,
            $"git -C \"{localPath}\" fetch origin", ct);
        if (exitCode != 0)
        {
            return (false, RedactPat(output));
        }

        // Get default branch
        var (branchOut, _) = await _terminalManager.ExecuteCommandAsync(sessionId,
            $"git -C \"{localPath}\" symbolic-ref refs/remotes/origin/HEAD --short", ct);
        var branch = branchOut?.Trim().Replace("origin/", "") ?? "main";

        // Reset to origin
        (output, exitCode) = await _terminalManager.ExecuteCommandAsync(sessionId,
            $"git -C \"{localPath}\" reset --hard origin/{branch}", ct);

        return exitCode == 0 ? (true, null) : (false, RedactPat(output));
    }

    private async Task<string?> GetCommitHashAsync(Guid sessionId, string localPath, CancellationToken ct)
    {
        var (output, exitCode) = await _terminalManager.ExecuteCommandAsync(sessionId,
            $"git -C \"{localPath}\" rev-parse HEAD", ct);
        return exitCode == 0 ? output?.Trim() : null;
    }

    private async Task SetupGitCredentialsAsync(
        Guid sessionId, string localPath, string cleanUrl, string? pat, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(pat))
        {
            return;
        }

        var credFile = Path.Combine(localPath, ".git", "git-credentials");

        // Configure credential helper for this repo
        await _terminalManager.ExecuteCommandAsync(sessionId,
            $"git -C \"{localPath}\" config credential.helper \"store --file {credFile}\"", ct);
        await _terminalManager.ExecuteCommandAsync(sessionId,
            $"git -C \"{localPath}\" config credential.useHttpPath true", ct);

        // Write credential entry
        var uri = new Uri(cleanUrl);
        var credLine = $"https://pat:{pat}@{uri.Host}{uri.AbsolutePath}";
        File.WriteAllText(credFile, credLine + Environment.NewLine);

        // Set remote URL to clean URL (remove embedded PAT)
        await _terminalManager.ExecuteCommandAsync(sessionId,
            $"git -C \"{localPath}\" remote set-url origin \"{cleanUrl}\"", ct);
    }

    private static bool IsAuthError(string? errorMessage)
    {
        if (string.IsNullOrEmpty(errorMessage))
        {
            return false;
        }

        return errorMessage.Contains("Authentication failed", StringComparison.OrdinalIgnoreCase)
            || errorMessage.Contains("401", StringComparison.Ordinal)
            || errorMessage.Contains("403", StringComparison.Ordinal)
            || errorMessage.Contains("could not read Username", StringComparison.OrdinalIgnoreCase);
    }

    private static (string repoUrl, string repoName) ParseAzureDevOpsUrl(string dataSource)
    {
        try
        {
            var uri = new Uri(dataSource);

            // Handle both formats:
            // https://dev.azure.com/{org}/{project}/_git/{repo}
            // https://{org}.visualstudio.com/{project}/_git/{repo}

            var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var gitIndex = Array.IndexOf(segments, "_git");

            if (gitIndex < 0 || gitIndex + 1 >= segments.Length)
            {
                return (string.Empty, string.Empty);
            }

            var repoName = segments[gitIndex + 1];

            // Return the original URL - git will handle authentication
            return (dataSource, repoName);
        }
        catch
        {
            return (string.Empty, string.Empty);
        }
    }

    private static string SanitizeFolderName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new System.Text.StringBuilder(name.Length);

        foreach (var c in name)
        {
            sanitized.Append(invalid.Contains(c) ? '_' : c);
        }

        return sanitized.ToString();
    }

    private static string BuildAuthenticatedUrl(string url, string? pat)
    {
        if (string.IsNullOrEmpty(pat))
        {
            return url;
        }

        try
        {
            var uri = new Uri(url);
            var builder = new UriBuilder(uri)
            {
                UserName = string.Empty,
                Password = pat
            };

            return builder.Uri.ToString();
        }
        catch
        {
            return url;
        }
    }

    private static string RedactPat(string? message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return string.Empty;
        }

        return Regex.Replace(
            message,
            @"(?<=[:@/])[A-Za-z0-9]{40,60}(?=@)",
            "***REDACTED***");
    }

    /// <summary>
    /// Reconciles document CloneStatus with actual filesystem state.
    /// </summary>
    private async Task ReconcileConnectorStatesAsync(IReadOnlyList<TsgConnectorDocument> connectors)
    {
        foreach (var connector in connectors)
        {
            // Skip non-healthy or actively processing connectors
            if (connector.Status != ConnectorStatus.Healthy ||
                connector.CloneStatus is CloneStatus.Cloning or CloneStatus.Syncing)
            {
                continue;
            }

            // Don't override explicit credential update intent
            if (connector.CloneStatus == CloneStatus.PendingCredentialUpdate)
            {
                continue;
            }

            var fsState = GetFilesystemState(connector.Name);

            // Check if status differs
            var statusChanged = fsState.Status != connector.CloneStatus;

            // Check if hash differs (only relevant when both are Ready)
            var hashChanged = fsState.Status == CloneStatus.Ready
                              && connector.CloneStatus == CloneStatus.Ready
                              && fsState.CommitHash != connector.LatestCommit;

            if (!statusChanged && !hashChanged)
            {
                continue;
            }

            if (statusChanged)
            {
                _logger.LogInternalInformation(
                    $"Reconciling connector {connector.Name}: {connector.CloneStatus} -> {fsState.Status}");
            }
            else if (hashChanged)
            {
                _logger.LogInternalInformation(
                    $"Updating commit hash for {connector.Name}: {connector.LatestCommit} -> {fsState.CommitHash}");
            }

            // Update document - use filesystem commit time as LastSuccessfulSync when hash changes
            var localPath = Path.Combine(CodeRefsPath, SanitizeFolderName(connector.Name));
            await _repository.UpdateCloneStatusAsync(
                connector.Name,
                fsState.Status,
                fsState.Status == CloneStatus.Ready ? localPath : null,
                fsState.CommitHash,
                lastSuccessfulSync: hashChanged ? fsState.CommitTime : null);
        }
    }

    /// <summary>
    /// Inspects the filesystem to determine the actual clone state for a connector.
    /// </summary>
    private FilesystemState GetFilesystemState(string connectorName)
    {
        var localPath = Path.Combine(CodeRefsPath, SanitizeFolderName(connectorName));
        var gitDir = Path.Combine(localPath, ".git");

        // No directory or no .git = not cloned
        if (!Directory.Exists(localPath) || !Directory.Exists(gitDir))
        {
            return new FilesystemState(CloneStatus.NotStarted, null, null);
        }

        // Try to read commit hash
        var commitHash = GetLocalCommitHash(localPath);
        if (commitHash == null)
        {
            // .git exists but corrupted
            return new FilesystemState(CloneStatus.NotStarted, null, null);
        }

        // Get commit time from HEAD ref file
        var commitTime = GetCommitTime(localPath);

        return new FilesystemState(CloneStatus.Ready, commitHash, commitTime);
    }

    /// <summary>
    /// Gets the commit time by reading the ref file's last modified time.
    /// </summary>
    private static DateTime? GetCommitTime(string localPath)
    {
        var gitDir = Path.Combine(localPath, ".git");
        var headPath = Path.Combine(gitDir, "HEAD");

        if (!File.Exists(headPath))
        {
            return null;
        }

        var headContent = File.ReadAllText(headPath).Trim();

        // If HEAD is a ref, get the ref file's time
        if (headContent.StartsWith("ref: ", StringComparison.Ordinal))
        {
            var refPath = Path.Combine(gitDir, headContent.Substring(5).Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(refPath))
            {
                return File.GetLastWriteTimeUtc(refPath);
            }
        }

        // Direct commit hash - use HEAD file time
        return File.GetLastWriteTimeUtc(headPath);
    }

    /// <summary>
    /// Read the current commit hash from the local git repository without spawning a git process.
    /// </summary>
    private static string? GetLocalCommitHash(string localPath)
    {
        var gitDir = Path.Combine(localPath, ".git");
        if (!Directory.Exists(gitDir))
        {
            return null;
        }

        var headPath = Path.Combine(gitDir, "HEAD");
        if (!File.Exists(headPath))
        {
            return null;
        }

        var headContent = File.ReadAllText(headPath).Trim();

        // If HEAD is a ref (e.g., "ref: refs/heads/main"), resolve it
        if (headContent.StartsWith("ref: ", StringComparison.Ordinal))
        {
            var refPath = Path.Combine(gitDir, headContent.Substring(5).Replace('/', Path.DirectorySeparatorChar));
            return File.Exists(refPath) ? File.ReadAllText(refPath).Trim() : null;
        }

        // HEAD is a direct commit hash
        return headContent;
    }

    /// <summary>
    /// Delete the local repository for a connector.
    /// Called when a connector is deleted to clean up disk space.
    /// </summary>
    public void DeleteLocalRepository(string connectorName)
    {
        var localPath = Path.Combine(CodeRefsPath, SanitizeFolderName(connectorName));
        if (Directory.Exists(localPath))
        {
            try
            {
                ForceDeleteDirectory(localPath);
                _logger.LogInternalInformation($"Deleted local repository for connector: {connectorName}");
            }
            catch (Exception ex)
            {
                _logger.LogInternalWarning(ex, $"Failed to delete local repository for connector: {connectorName}");
            }
        }
    }

    /// <summary>
    /// Deletes a directory, handling read-only files that block standard deletion.
    /// </summary>
    /// <remarks>
    /// Azure DevOps repositories often have files/folders with the ReadOnly attribute set,
    /// particularly in .azuredevops directories (e.g., pull_request_template/branches).
    /// This is a legacy behavior from TFS/TFVC where files were read-only until checked out.
    /// When cloning via PAT authentication, ADO preserves these attributes, causing
    /// Directory.Delete to fail with "Access denied". This method attempts a normal delete
    /// first, and only enumerates to clear attributes if that fails.
    /// </remarks>
    private static void ForceDeleteDirectory(string path)
    {
        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch (Exception) when (Directory.Exists(path))
        {
            // Clear read-only attributes and retry
            var di = new DirectoryInfo(path);
            foreach (var info in di.GetFileSystemInfos("*", SearchOption.AllDirectories))
            {
                info.Attributes = FileAttributes.Normal;
            }
            di.Attributes = FileAttributes.Normal;
            di.Delete(true);
        }
    }
}
