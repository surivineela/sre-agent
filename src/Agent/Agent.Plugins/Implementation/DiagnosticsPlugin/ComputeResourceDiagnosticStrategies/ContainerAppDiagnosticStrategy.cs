using System.Net.WebSockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using Agent.Core.Helpers;
using Azure.Core;
using Agent.Logging;
using Microsoft.Extensions.Logging;
using Agent.Core.Interfaces;
using System.Net;
using Azure.ResourceManager.AppContainers;
using Agent.Plugins.Interface;

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
            string errorMessage = $"Unsupported language stack for memory analysis: {resourceId}";
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

    private async Task<string> InvokeExecCommand(string resourceId, string command)
    {
        try
        {
            // Get Container App Details.
            ResourceIdentifier resourceIdentifer = new ResourceIdentifier(resourceId);
            string subscriptionId = resourceIdentifer.SubscriptionId;
            var armClient = await _armClientFactory.GetArmOperationClient();
            var containerAppResource = armClient.GetContainerAppResource(resourceIdentifer);
            var containerApp = await containerAppResource.GetAsync();
            var activeRevisions = containerAppResource.GetContainerAppRevisions();
            var firstActiveRevision = activeRevisions.FirstOrDefault(r => r.Data.IsActive == true);
            var firstReplica = await firstActiveRevision.GetContainerAppReplicas().FirstOrDefault().GetAsync();

            string execEndPoint = firstReplica.Value.Data.Containers.First().ExecEndpoint;

            var uriBuilder = new UriBuilder(execEndPoint);
            var query = HttpUtility.ParseQueryString(uriBuilder.Query);
            query.Add("command", "/bin/bash");
            uriBuilder.Query = query.ToString();

            string token = await _armHelper.GetProxyApiTokenAsync(subscriptionId, resourceIdentifer.ResourceGroupName, containerApp.Value.Data.Name);

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
            string completionMarker = "COMPLETED ANALYSIS";
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
                            bool setResult = completionSource.TrySetResult(true);
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    completionSource.TrySetException(ex);
                }
            });

            // Setup.
            await Write(webSocket, command + "\n", CancellationToken.None);

            // Wait for the completion signal or timeout after a reasonable period
            var timeoutTask = Task.Delay(TimeSpan.FromMinutes(5));
            var completedTask = await Task.WhenAny(completionSource.Task, timeoutTask);

            if (completedTask == timeoutTask)
            {
                _logger.LogInternalError($"[InvokeCommand] Command execution timed out for {resourceId}.");
            }

            await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);

            string result = resultBuilder.ToString();
            string pattern = @"STARTED ANALYSIS\s*(.*?)\s*COMPLETED ANALYSIS";
            Match match = Regex.Match(result, pattern, RegexOptions.Singleline);

            if (match.Success)
            {
                string analysisResult = match.Groups[1].Value.Trim();
                _logger.LogInternalError($"[InvokeExecCommand] InvokeExecCommand for command: {command} - {analysisResult}.");
                return analysisResult;
            }
            else
            {
                _logger.LogInternalError($"[InvokeExecCommand] No Analysis found: {command}.");
                return result; // TODO:FIX
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalError($"[InvokeExecCommand] Error executing command: {command}: {ex.Message}");
            throw;
        }
    }

    public async Task<string> GetMemoryAnalysis(string resourceId)
    {
        _logger.LogInternalInformation($"[GetMemoryAnalysis] Getting memory analysis for {resourceId}");
        try
        {
            string commands = " apt-get update; apt-get install -y curl; curl https://dotnetanalysis.blob.core.windows.net/acascripts/dotnet-dump-analyze.sh -o /tmp/dotnet-dump-analyze.sh; chmod +x /tmp/dotnet-dump-analyze.sh; sh /tmp/dotnet-dump-analyze.sh";
            return await InvokeExecCommand(resourceId, commands);
        }

        catch (Exception ex)
        {
            _logger.LogInternalError($"[GetMemoryAnalysis] Error executing command: {ex.Message} for {resourceId}");
            throw;
        }
    }

    public async Task<string> GetCPUAnalysis(string resourceId)
    {
        _logger.LogInternalInformation($"[GetCPUAnalysis] Getting CPU analysis for {resourceId}");
        try
        {
            string commands = " apt-get update; apt-get install -y curl; curl https://dotnetanalysis.blob.core.windows.net/acascripts/dotnet-cpu-analyzer.sh -o /tmp/dotnet-cpu-analyzer.sh; chmod +x /tmp/dotnet-cpu-analyzer.sh; sh /tmp/dotnet-cpu-analyzer.sh";
            return await InvokeExecCommand(resourceId, commands);
        }

        catch (Exception ex)
        {
            _logger.LogInternalError($"[GetContainerMemoryAnalysisForDotnet] Error executing command: {ex.Message} for {resourceId}");
            throw;
        }
    }

    public async Task<bool> IsDotnetBased(string resourceId)
    {
        _logger.LogInternalInformation($"[IsDotnetBased] Checking if .NET Based for resourceId: {resourceId}");
        try
        {
            string commands = " apt-get update; apt-get install -y curl; curl https://dotnetanalysis.blob.core.windows.net/acascripts/dotnet-detect.sh -o /tmp/dotnet-detect.sh; chmod +x /tmp/dotnet-detect.sh; sh /tmp/dotnet-detect.sh";
            var result = await InvokeExecCommand(resourceId, commands);
            return result.Any();
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
