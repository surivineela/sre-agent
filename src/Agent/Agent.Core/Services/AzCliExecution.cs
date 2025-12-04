// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace Agent.Core.Services;

public class AzCliExecution
{
    private const string AzExecutablePath = "/opt/az/bin/python3";
    private readonly ILogger _logger;
    // The command is the full az command without the 'az ' prefix
    private readonly string _command;
    private readonly string? _accessToken;
    private readonly Dictionary<string, string>? _additionalTokens;
    private readonly string _configDir;
    private readonly bool _isDevelopment;
    private readonly SessionPoolSettings _sessionPoolSettings;
    private readonly ISessionPoolService _sessionPoolService;
    public AzCliExecution(ILogger logger,
        string command,
        SessionPoolSettings sessionPoolSettings,
        ISessionPoolService sessionPoolService,
        string? accessToken = null,
        bool isDevelopment = false,
        Dictionary<string, string>? additionalTokens = null)
    {
        _logger = logger;
        _command = command.Trim();
        _sessionPoolSettings = sessionPoolSettings;
        _sessionPoolService = sessionPoolService;
        _accessToken = accessToken;
        _additionalTokens = additionalTokens;
        _configDir = isDevelopment ? string.Empty : Path.Join(Path.GetTempPath(), $"azcli-{Path.GetRandomFileName()}");
        _isDevelopment = isDevelopment;
    }

    public async Task<string> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            int exitCode;
            string stdout;
            string stderr;
            if (_sessionPoolSettings.Enabled
                && !string.IsNullOrEmpty(_sessionPoolSettings.PoolManagementEndpoint))
            {
                (exitCode, stdout, stderr) = await _sessionPoolService.ExecuteCliAsync(_command, _sessionPoolService.BuildSessionIdentifier(randomSuffix: true), _additionalTokens);
            }
            else
            {
                var cmd = _command.Substring("az ".Length);
                var envs = new Dictionary<string, string>();

                if (!string.IsNullOrEmpty(_configDir))
                {
                    envs["AZURE_CONFIG_DIR"] = _configDir;
                }

                if (!string.IsNullOrEmpty(_accessToken))
                {
                    envs["AZURE_CLI_ACCESS_TOKEN"] = _accessToken;
                }

                var (exe, args) = GetExecuableAndArgs([_command]);
                var pCmd = new ExternalProcessCommand(
                    logger: _logger,
                    exe: exe,
                    arguments: [args],
                    timeout: Constants.AzCliDefaultTimeout,
                    envs: envs);
                (exitCode, stdout, stderr) = await pCmd.ExecuteAsync(cancellationToken);
            }

            if (exitCode != 0)
            {
                stderr = string.IsNullOrEmpty(stderr) ? "Unknown error occurred." : stderr;
                throw new InvalidOperationException(stderr);
            }

            return stdout;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError($"AzCliExecution failed for command '{_command}': {ex}");
            throw;
        }
    }

    private (string, string) GetExecuableAndArgs(string[] commands)
    {
        if (_isDevelopment)
        {
            var exe = OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/bash";
            var args = OperatingSystem.IsWindows() ? $"/c {string.Join(" ", commands)}" : $"-c \"az {string.Join(" ", commands)}\"";
            return (exe, args);
        }
        else
        {
            // from az bash file
            return (AzExecutablePath, $"-Im azure.cli {string.Join(" ", commands)}");
        }
    }
}
