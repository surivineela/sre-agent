using Agent.Core.Configuration;
using Agent.Core.Helpers;
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
    private readonly string? _identity;
    private readonly string _configDir;
    private readonly bool _isDevelopment;
    private readonly SessionPoolSettings _sessionPoolSettings;
    private readonly ISessionPoolService _sessionPoolService;
    public AzCliExecution(ILogger logger,
        string command,
        SessionPoolSettings sessionPoolSettings,
        ISessionPoolService sessionPoolService,
        string? accessToken = null,
        string? identity = null,
        bool isDevelopment = false)
    {
        _logger = logger;
        _command = command.Trim();
        if (_command.StartsWith("az ", StringComparison.OrdinalIgnoreCase))
        {
            _command = _command.Substring("az ".Length).Trim();
        }
        _sessionPoolSettings = sessionPoolSettings;
        _sessionPoolService = sessionPoolService;
        _accessToken = accessToken;
        _identity = identity;
        _configDir = isDevelopment ? string.Empty : Path.Join(Path.GetTempPath(), $"azcli-{Path.GetRandomFileName()}");
        _isDevelopment = isDevelopment;
    }

    public async Task<string> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (_sessionPoolSettings.Enabled
                && !string.IsNullOrEmpty(_sessionPoolSettings.PoolManagementEndpoint)
                && !string.IsNullOrEmpty(_accessToken))
            {
                return await _sessionPoolService.ExecuteCliAsync(_command, _accessToken, $"{AgentNameHelper.GetAgentName(!_isDevelopment)}-{Guid.NewGuid().ToString("N").Substring(0, 8)}");
            }

            // az login does not support access token
            // the token is consumed using Environment variable AZURE_CLI_ACCESS_TOKEN
            if (string.IsNullOrEmpty(_accessToken))
            {
                var login = await AzLogin(cancellationToken);
                if (!login)
                {
                    return "[Exception encountered]: Failed to login to Azure CLI";
                }
            }
            else
            {
                _logger.LogInternalInformation($"[AzCliExecution] Skip Az login and use OBO token for command execution.");
            }

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
            var pCmd = new ExternalProcessCommand(_logger, exe, [args], envs: envs);
            return await pCmd.ExecuteAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError($"AzCliExecution failed for command '{_command}': {ex}");
            throw;
        }
    }

    private async Task<bool> AzLogin(CancellationToken cancellationToken)
    {
        if (_isDevelopment)
        {
            return true;
        }

        string[] loginCommands;
        if (string.IsNullOrEmpty(_identity))
        {
            throw new InvalidOperationException("No managed identity provided for az login.");
        }
        else if (string.Equals(Constants.SystemManagedIdentityName, _identity))
        {
            loginCommands = ["login", "--identity", "--allow-no-subscriptions"];
        }
        else
        {
            loginCommands = ["login", "--identity", "--allow-no-subscriptions", "--resource-id", _identity];
        }

        _logger.LogInternalInformation($"Az login with managed identity {_identity}");
        try
        {
            var envs = new Dictionary<string, string>();

            if (!string.IsNullOrEmpty(_configDir))
            {
                envs["AZURE_CONFIG_DIR"] = _configDir;
            }

            var (exe, args) = GetExecuableAndArgs(loginCommands);
            var cmd = new ExternalProcessCommand(_logger, exe, [args], envs: envs);
            await cmd.ExecuteAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError($"Az login failed: {ex}");
            return false;
        }

        return true;
    }

    private (string, string) GetExecuableAndArgs(string[] commands)
    {
        if (_isDevelopment)
        {
            var exe = OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/bash";
            var args = OperatingSystem.IsWindows() ? $"/c az {string.Join(" ", commands)}" : $"-c \"az {string.Join(" ", commands)}\"";
            return (exe, args);
        }
        else
        {
            // from az bash file
            return (AzExecutablePath, $"-Im azure.cli {string.Join(" ", commands)}");
        }
    }
}
