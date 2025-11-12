using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Plugins.Interface;
using Azure.Core;
using Azure.ResourceManager.AppContainers;
using Microsoft.Extensions.Logging;

namespace Agent.Plugins.Implementation.DiagnosticsPlugin.ComputeResourceDiagnosticStrategies;

internal sealed class ContainerAppDiagnosticStrategy : ComputeResourceDiagnosticStrategyBase
{
    private readonly ArmHelper _armHelper;
    private readonly IArmClientFactory _armClientFactory;

    public ContainerAppDiagnosticStrategy(ILogger<DiagnosticsPlugin> logger, ArmHelper armHelper, IArmClientFactory armClientFactory)
        : base(logger)
    {
        _armHelper = armHelper;
        _armClientFactory = armClientFactory;

        _analysisHandlers = new Dictionary<AnalysisType, Func<string, ComputeResourceInfo, AnalysisType, string, Task<string>>>
        {
            { AnalysisType.Memory, AnalyzeMemoryAsync },
            { AnalysisType.Latency, AnalyzeLatencyAsync },
            { AnalysisType.Cpu, AnalyzeCPUAsync },
        };
    }

    public override bool CanHandle(ComputeResourceInfo resourceInfo)
        => resourceInfo.ResourceType == ComputeResourceType.ContainerApp;

    private async Task<string> AnalyzeLatencyAsync(string resourceId, ComputeResourceInfo computeResourceInfo, AnalysisType type, string additionalProperties)
    {
        if (computeResourceInfo.LanguageStack == LanguageStack.Dotnet && await IsDotnetBased(resourceId))
        {
            _logger.LogInternalInformation($"[AnalyzeLatencyAsync] Getting latency analysis for {resourceId}");
            try
            {
                string commands = " apt-get update; apt-get install -y curl; curl https://dotnetanalysis.blob.core.windows.net/acascripts/dotnet-latency-analyze.sh -o /tmp/dotnet-latency-analyze.sh; chmod +x /tmp/dotnet-latency-analyze.sh; sh /tmp/dotnet-latency-analyze.sh";
                return await InvokeExecCommand(resourceId, commands);
            }
            catch (Exception ex)
            {
                _logger.LogInternalError($"[AnalyzeLatencyAsync] Error executing command: {ex.Message} for {resourceId}");
                throw;
            }
        }

        else
        {
            string errorMessage = $"Unsupported language stack for latency analysis: {resourceId}";
            _logger.LogInternalError(errorMessage);
            throw new ArgumentException(errorMessage);
        }
    }

    internal async Task<string> AnalyzeMemoryAsync(string resourceId, ComputeResourceInfo computeResourceInfo, AnalysisType analysisType, string additionalProperties)
    {
        if (computeResourceInfo.LanguageStack == LanguageStack.Dotnet && await IsDotnetBased(resourceId))
        {
            return await GetMemoryAnalysis(resourceId);
        }

        else
        {
            string errorMessage = $"Unsupported language stack for memory analysis: {resourceId}";
            _logger.LogInternalError(errorMessage);
            throw new ArgumentException(errorMessage);
        }
    }

    internal async Task<string> AnalyzeCPUAsync(string resourceId, ComputeResourceInfo computeResourceInfo, AnalysisType type, string additionalProperties)
    {
        if (computeResourceInfo.LanguageStack == LanguageStack.Dotnet && await IsDotnetBased(resourceId))
        {
            return await GetCPUAnalysis(resourceId);
        }

        else
        {
            string errorMessage = $"Unsupported language stack for CPU analysis: {resourceId}";
            _logger.LogInternalError(errorMessage);
            throw new ArgumentException(errorMessage);
        }
    }

    private async Task<string> ExecuteScriptFromUrl(string resourceId, string scriptName, string operationName)
    {
        try
        {
            string commands = $" apt-get update; apt-get install -y curl; curl https://dotnetanalysis.blob.core.windows.net/acascripts/{scriptName} -o /tmp/{scriptName}; chmod +x /tmp/{scriptName}; sh /tmp/{scriptName}";
            return await InvokeExecCommand(resourceId, commands);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError($"[{operationName}] Error executing command: {ex.Message} for {resourceId}");
            throw;
        }
    }

    private async Task<string> ExecuteLocalScript(string resourceId, string scriptFileName, string operationName)
    {
        try
        {
            string scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AgentsV2", "DiagnosticsAgents", "DiagnosticBinariesAndScripts", scriptFileName);
            string scriptContent = await File.ReadAllTextAsync(scriptPath);

            // Generate a unique temporary script name
            string tempScriptName = $"/tmp/{operationName}_{Guid.NewGuid().ToString("N")[..8]}.sh";

            // Create the script transfer and execution command using printf to handle special characters
            string escapedContent = scriptContent.Replace("'", "'\"'\"'"); // Escape single quotes
            string transferAndExecuteCommand = $"printf '%s' '{escapedContent}' > {tempScriptName} && chmod +x {tempScriptName}";
            var result = await InvokeExecCommand(resourceId, transferAndExecuteCommand);
            transferAndExecuteCommand = $"bash {tempScriptName}; rm -f {tempScriptName}";
            result = await InvokeExecCommand(resourceId, transferAndExecuteCommand);
            return CleanAnalysisResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError($"[{operationName}] Error executing command: {ex.Message} for {resourceId}");
            throw;
        }
    }

    private static List<string> ParseScriptCommands(string scriptContent)
    {
        var commands = new List<string>();
        var lines = scriptContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var currentCommand = new StringBuilder();

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();

            // Skip comments and empty lines
            if (string.IsNullOrWhiteSpace(trimmedLine) || trimmedLine.StartsWith('#'))
                continue;

            // Skip shebang and set commands
            if (trimmedLine.StartsWith("#!/") || trimmedLine.StartsWith("set "))
                continue;

            // Handle variable assignments and exports
            if (trimmedLine.Contains("=") && !trimmedLine.Contains("$(") && !trimmedLine.Contains("`"))
            {
                commands.Add(trimmedLine);
                continue;
            }

            // Handle line continuations with backslash
            if (trimmedLine.EndsWith('\\'))
            {
                currentCommand.Append(trimmedLine[..^1]); // Remove backslash
                currentCommand.Append(' ');
                continue;
            }

            // Add current line to command
            currentCommand.Append(trimmedLine);

            // If line ends with semicolon or is a complete command, finalize it
            if (trimmedLine.EndsWith(';') || IsCompleteCommand(trimmedLine))
            {
                var command = currentCommand.ToString().TrimEnd(';');
                if (!string.IsNullOrWhiteSpace(command))
                {
                    commands.Add(command);
                }
                currentCommand.Clear();
            }
        }

        // Add any remaining command
        if (currentCommand.Length > 0)
        {
            var command = currentCommand.ToString().TrimEnd(';');
            if (!string.IsNullOrWhiteSpace(command))
            {
                commands.Add(command);
            }
        }

        return commands;
    }

    private static bool IsCompleteCommand(string line)
    {
        // Commands that are typically complete on their own
        var completeCommandPrefixes = new[] { "mkdir", "curl", "export", "echo", "cat", "sort", "awk", "tail", "head" };
        return completeCommandPrefixes.Any(prefix => line.StartsWith(prefix + " ") || line == prefix);
    }

    private static string CleanAnalysisResult(string result)
    {
        return string.Join("\n",
            result.Split('\n')                            // split into lines
                  .Select(line => line.Trim())            // trim each line
                  .Where(line => !string.IsNullOrEmpty(line) && line != "<<") // remove empty lines and '<<'
                  .Select(line => Regex.Replace(line, @"\s+", " ")) // collapse multiple spaces to one
        );
    }

    private async Task<string> InvokeExecCommand(string resourceId, string command)
    {
        try
        {
            // Get Container App Details.
            ResourceIdentifier resourceIdentifer = new ResourceIdentifier(resourceId);
            string subscriptionId = resourceIdentifer.SubscriptionId ?? string.Empty;
            var armClient = await _armClientFactory.GetArmOperationClient();
            var containerAppResource = armClient.GetContainerAppResource(resourceIdentifer);
            var containerApp = await containerAppResource.GetAsync();
            var activeRevisions = containerAppResource.GetContainerAppRevisions();
            var firstActiveRevision = activeRevisions.FirstOrDefault(r => r.Data.IsActive == true);
            var firstReplica = firstActiveRevision?.GetContainerAppReplicas().FirstOrDefault() is { } first
                ? await first.GetAsync()
                : null;


            string execEndPoint = firstReplica?.Value?.Data?.Containers?.First().ExecEndpoint ?? string.Empty;

            var uriBuilder = new UriBuilder(execEndPoint);
            var query = HttpUtility.ParseQueryString(uriBuilder.Query);
            query.Add("command", "/bin/bash");
            uriBuilder.Query = query.ToString();

            string token = await _armHelper.GetProxyApiTokenAsync(subscriptionId, resourceIdentifer.ResourceGroupName ?? string.Empty, containerApp.Value.Data.Name);

            var webSocket = new ClientWebSocket();
            webSocket.Options.SetRequestHeader("Authorization", $"Bearer {token}");
            webSocket.Options.HttpVersion = HttpVersion.Version11;
            webSocket.Options.HttpVersionPolicy = HttpVersionPolicy.RequestVersionExact;
            webSocket.Options.UseDefaultCredentials = false;

            var resultBuilder = new StringBuilder();

            await webSocket.ConnectAsync(uriBuilder.Uri, CancellationToken.None);
            _logger.LogInternalInformation("Connected to WebSocket endpoint.");

            await SendResize(webSocket, 80, 24, CancellationToken.None);

            // Define completion marker and create a TaskCompletionSource for synchronization
            string completionMarker = ">>COMPLETED ANALYSIS<<";
            var completionSource = new TaskCompletionSource<bool>();

            var listeningTask = Task.Run(async () =>
            {
                try
                {
                    while (webSocket.State == WebSocketState.Open)
                    {
                        bool isCompleted = await Read(webSocket, CancellationToken.None, completionMarker, resultBuilder);
                        if (isCompleted)
                        {
                            completionSource.TrySetResult(true);
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    completionSource.TrySetException(ex);
                }
            });

            // Execute the command
            await Write(webSocket, command + "\n", CancellationToken.None);

            // Wait for completion signal or timeout
            var timeoutTask = Task.Delay(TimeSpan.FromMinutes(2));
            var completedTask = await Task.WhenAny(completionSource.Task, timeoutTask);

            if (completedTask == timeoutTask)
            {
                _logger.LogInternalWarning($"[InvokeExecCommand] Command execution timed out for {resourceId}: {command}");
            }

            await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);

            string result = resultBuilder.ToString();

            // Try to extract content between analysis markers
            string pattern = @">>STARTED ANALYSIS<<\s*(.*?)\s*>>COMPLETED ANALYSIS<<";
            Match match = Regex.Match(result, pattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);

            if (match.Success)
            {
                string analysisResult = match.Groups[1].Value.Trim();
                _logger.LogInternalInformation($"[InvokeExecCommand] Analysis extracted successfully for command");
                return analysisResult;
            }
            else
            {
                // If no analysis markers found, return cleaned output
                _logger.LogInternalInformation($"[InvokeExecCommand] No analysis markers found, returning full output");
                return CleanCommandOutput(result);
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalError($"[InvokeExecCommand] Error executing command: {ex.Message}");
            throw;
        }
    }

    private static string CleanCommandOutput(string output)
    {
        if (string.IsNullOrEmpty(output))
            return string.Empty;

        // Remove common shell prompt patterns and command echoes
        var lines = output.Split('\n')
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrEmpty(line))
            .Where(line => !line.StartsWith("$") && !line.StartsWith("#") && !line.StartsWith("root@"))
            .Where(line => !line.Contains("ERROR:") || line.Contains("INFO:"));

        return string.Join("\n", lines);
    }

    public async Task<string> GetMemoryAnalysis(string resourceId)
    {
        _logger.LogInternalInformation($"[GetMemoryAnalysis] Getting memory analysis for {resourceId}");
        return await ExecuteLocalScript(resourceId, "dotnet-memory-analyzer-aca.sh", "GetMemoryAnalysis");
    }

    public async Task<string> GetCPUAnalysis(string resourceId)
    {
        _logger.LogInternalInformation($"[GetCPUAnalysis] Getting CPU analysis for {resourceId}");
        return await ExecuteLocalScript(resourceId, "dotnet-cpu-analyzer-aca.sh", "GetCPUAnalysis");
    }

    public async Task<bool> IsDotnetBased(string resourceId)
    {
        _logger.LogInternalInformation($"[IsDotnetBased] Checking if .NET Based for resourceId: {resourceId}");
        try
        {
            // File read all the commands, separated by ;
            // If any of the commands return a result, then it is .NET based.
            var result = await ExecuteLocalScript(resourceId, "dotnet-detect.sh", "IsDotnetBased");
            return result.Trim().Any();
        }

        catch (Exception ex)
        {
            _logger.LogInternalError($"[IsDotnetBased] Error executing command: {ex.Message} for {resourceId}");
            throw;
        }
    }

    private static async Task SendResize(WebSocket socket, int x, int y, CancellationToken token)
    {
        List<byte> bytes = [];
        bytes.Add(0); //forward byte
        bytes.Add(4); //resize
        byte[] message = Encoding.UTF8.GetBytes(FormattableString.Invariant($"{{{{\"Width\": {x}, \"Height\": {y}}}}}"));
        foreach (byte b in message)
        {
            bytes.Add(b);
        }
        await socket.SendAsync(bytes.ToArray(), WebSocketMessageType.Text, true, token);
    }

    private static async Task Write(WebSocket socket, string line, CancellationToken token)
    {
        byte[] bytes = [0, 0, 0];

        byte[] message = Encoding.UTF8.GetBytes(line);

        foreach (byte b in message)
        {
            bytes[2] = b;
            await socket.SendAsync(bytes, WebSocketMessageType.Text, true, token);
        }
    }

    // Logic is from https://msazure.visualstudio.com/One/_git/AAPT-Antares-AzureFunctionsUx?path=%2Fclient-react%2Fsrc%2Fpages%2Fcontainer-app%2Fconsole%2FConsoleDataLoader.tsx&_a=contents&version=GBmaster
    private static async Task<bool> Read(WebSocket socket, CancellationToken token, string completionMarker, StringBuilder stdout)
    {
        Memory<byte> buffer = new Memory<byte>(new byte[8 * 1024]);
        var result = await socket.ReceiveAsync(buffer, token);

        var data = buffer[..result.Count].ToArray();
        var text = string.Empty;

        switch (data[0])
        {
            case 0: // forwarded from k8s cluster exec endpoint
                if (data[1] == 1 || data[1] == 2 || data[1] == 3)
                {
                    text = Encoding.UTF8.GetString(data, 2, data.Length - 2);
                    stdout.AppendLine(text);

                    // Check if the completion marker is in the output
                    if (!string.IsNullOrEmpty(completionMarker) && text.Contains(completionMarker))
                    {
                        //Console.WriteLine("Execution completed successfully.");
                        return true; // Signal completion
                    }
                }
                else if (data[1] == 4)
                {
                    // terminal resize
                }
                else
                {
                    throw new Exception($"Unknown Proxy API exec signal {data[1]}");
                }
                break;

            case 1: // info from Proxy API
                text = "INFO: " + Encoding.UTF8.GetString(data, 1, data.Length - 1) + "\r\n";
                //Console.WriteLine(text);
                break;

            case 2: // error from Proxy API
                text = "ERROR: " + Encoding.UTF8.GetString(data, 1, data.Length - 1) + "\r\n";
                //Console.WriteLine(text);
                break;

            default:
                throw new Exception($"Unknown Proxy API exec signal {data[0]}");
        }

        return false;
    }
}
