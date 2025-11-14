using System.Diagnostics;
using Agent.Core.Models.Session;
using Agent.Core.Services;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

namespace Session.Cli.Services;

public class ShellService : IShellService
{
    private readonly ILogger<ShellService> _logger;
    private readonly ITokenService _tokenService;
    private readonly string _msiEndpoint;

    public ShellService(ILogger<ShellService> logger, ITokenService tokenService, IServer server)
    {
        _logger = logger;
        _tokenService = tokenService;
        var feature = server.Features.Get<IServerAddressesFeature>();
        var addresses = feature?.Addresses;
        var httpAddress = addresses?.FirstOrDefault(a => a.StartsWith("http"));
        if (string.IsNullOrEmpty(httpAddress))
        {
            _msiEndpoint = "http://localhost:8080/msi/token";
        }
        else
        {
            _msiEndpoint = $"http://localhost:{new Uri(httpAddress).Port}/msi/token";
        }
    }

    public async Task<ShellExecuteResponse> ExecuteAzCli(AzCliExecutionRequest request, string identifier, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        _logger.LogInformation($"Executing shell scripts. Identifier: {identifier}. Script: {request.ShellScripts}, Stdin Length: {request.Stdin?.Length}. " +
            $"Access token scopes: {string.Join(", ", request.AccessTokens.Keys)}");

        // No requirement to support non-Linux OS at this time.
        if (!OperatingSystem.IsLinux())
        {
            throw new NotSupportedException("This service is only expected to run on Linux.");
        }

        // add static tokens
        await _tokenService.AddTokensAsync(request.AccessTokens);

        var configDir = Path.Join(Path.GetTempPath(), $"azcli-{Path.GetRandomFileName()}");
        var commonEnvs = new Dictionary<string, string>()
        {
            { "IDENTITY_ENDPOINT", _msiEndpoint }, // redirect token requests
            { "IDENTITY_HEADER", identifier }, // can be any value
            { "AZURE_CONFIG_DIR", configDir },
        };

        try
        {
            var subscriptionId = ExtractSubscriptionId(request.ShellScripts);
            _logger.LogInformation($"Start to run az login, Subscription ID: {subscriptionId}, MSI endpoint: {_msiEndpoint}");

            var command = "az login -i";
            if (!string.IsNullOrEmpty(subscriptionId))
            {
                command += $" && az account set --subscription {subscriptionId}";
            }

            var pCmd = new ExternalProcessCommand(_logger, "/bin/bash", ["-c", command], timeout: TimeSpan.FromSeconds(10), envs: commonEnvs);
            var (exitCode, stdout, stderr) = await pCmd.ExecuteAsync(cancellationToken);
            if (exitCode != 0)
            {
                return new ShellExecuteResponse
                {
                    Identifier = identifier,
                    ExitCode = exitCode,
                    Result = new CommandResult
                    {
                        Stdout = stdout,
                        Stderr = stderr,
                        ExecutionTimeInMilliseconds = sw.ElapsedMilliseconds,
                    },
                    ErrorMessage = $"Failed to login to az cli."
                };
            }
            _logger.LogInformation($"Successfully logged in to az cli.");
        }
        catch (TimeoutException ex) // timeout
        {
            _logger.LogError(ex, "Timeout occurred while logging in to az cli.");
            return new ShellExecuteResponse
            {
                Identifier = identifier,
                ExitCode = -1,
                Result = new CommandResult
                {
                    ExecutionTimeInMilliseconds = sw.ElapsedMilliseconds
                },
                ErrorMessage = "Time out executing the command."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to login to az cli.");
            return new ShellExecuteResponse
            {
                Identifier = identifier,
                ExitCode = -1,
                Result = new CommandResult
                {
                    ExecutionTimeInMilliseconds = sw.ElapsedMilliseconds
                },
                ErrorMessage = $"An error occurred when login to az cli: {ex.Message}"
            };
        }

        var scriptPath = Path.Join(Path.GetTempPath(), $"script-{identifier}-{Guid.NewGuid().ToString("N").Substring(0, 8)}.sh");
        _logger.LogInformation($"Writing script to temporary file: {scriptPath}");
        File.WriteAllText(scriptPath, request.ShellScripts);
        File.SetUnixFileMode(scriptPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        try
        {
            _logger.LogInformation("Starting to execute shell script.");
            var pCmd = new ExternalProcessCommand(_logger, "/bin/bash", [scriptPath], request.Stdin, TimeSpan.FromSeconds(request.TimeoutInSeconds), commonEnvs);
            var (exitCode, stdout, stderr) = await pCmd.ExecuteAsync(cancellationToken);

            _logger.LogInformation("Executed shell script.");

            return new ShellExecuteResponse
            {
                Identifier = identifier,
                ExitCode = exitCode,
                Result = new CommandResult
                {
                    Stdout = stdout,
                    Stderr = stderr,
                    ExecutionTimeInMilliseconds = sw.ElapsedMilliseconds
                },
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while executing shell script.");
            return new ShellExecuteResponse
            {
                Identifier = identifier,
                ExitCode = -1,
                Result = new CommandResult
                {
                    ExecutionTimeInMilliseconds = sw.ElapsedMilliseconds
                },
                ErrorMessage = $"An error occurred while executing the shell script: {ex.Message}"
            };
        }
    }

    public Task<ShellExecuteResponse> ExecuteKubectl(KubectlExecutionRequest request, string identifier, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    private string? ExtractSubscriptionId(string scripts)
    {
        var lines = scripts.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            if (trimmedLine.StartsWith("az ", StringComparison.OrdinalIgnoreCase) &&
                trimmedLine.Contains("--subscription"))
            {
                var parts = trimmedLine.Split(' ');
                for (int i = 0; i < parts.Length; i++)
                {
                    if (parts[i] == "--subscription" && i + 1 < parts.Length)
                    {
                        return parts[i + 1];
                    }
                    else if (parts[i].StartsWith("--subscription="))
                    {
                        return parts[i].Substring("--subscription=".Length);
                    }
                }
            }
        }
        return null;
    }
}
