using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using Agent.Core.Helpers;
using Agent.Logging;
using Agent.Plugins.Definitions;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client.Platforms.Features.DesktopOs.Kerberos;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Agent.Plugins.Implementation.DiagnosticsPlugin.ComputeResourceDiagnosticStrategies;

internal sealed class AppServiceDiagnosticStrategy : ComputeResourceDiagnosticStrategyBase
{
    private readonly ArmHelper _armHelper;

    public AppServiceDiagnosticStrategy(ILogger<DiagnosticsPlugin> logger, ArmHelper armHelper)
        : base(logger)
    {
        _armHelper = armHelper;
        _analysisHandlers = new Dictionary<AnalysisType, Func<string, ComputeResourceInfo, AnalysisType, string, Task<string>>>
        {
            { AnalysisType.Memory, AnalyzeMemoryAsync },
            { AnalysisType.Cpu, AnalyzeCpuAsync },
        };
    }

    public override bool CanHandle(ComputeResourceInfo resourceInfo)
        => resourceInfo.ResourceType == ComputeResourceType.AppService;

    internal async Task<string> AnalyzeMemoryAsync(string resourceId, ComputeResourceInfo computeResourceInfo, AnalysisType analysisType, string additionalProperties)
    {
        // Step 1: Take a full memory dump.
        string memoryDumpFile = Path.GetFileName(Path.GetTempFileName() + ".dmp");

        KuduManager kuduManager = await KuduManager.Initialize(resourceId, _armHelper);

        if (kuduManager.OS == "Linux")
        {
            throw new NotImplementedException("Currently this behavior isn't implemented for Linux");
        }

        else // Windows.
        {
            string path = "C://local//" + memoryDumpFile;

            // Curl command on the machine to collect the dump.
            int pid = await _armHelper.GetDefaultProcessIdForWebAppAsync(resourceId, kuduManager.OS, kuduManager.KuduHostName);
            string command = $"C://devtools//sysinternals//procdump.exe -ma {pid} -accepteula C://local//{memoryDumpFile}";
            string commandResult = await _armHelper.ExecuteKuduCommandAsync(kuduManager.KuduHostName, command, "C://local//");

            // Step 2: Analyze the dump.
            if (kuduManager.Is32Bit)
            {
                await kuduManager.ExecuteCommandAsync("curl -X GET https://dotnetanalysis.blob.core.windows.net/win32/DotnetAnalyzer.exe -o DotnetAnalyzer.exe", "C://local//");
            }

            else
            {
                await kuduManager.ExecuteCommandAsync("curl -X GET https://dotnetanalysis.blob.core.windows.net/win64/DotnetAnalyzer.exe -o DotnetAnalyzer.exe", "C://local//");
            }

            // Run the dotnet analyzer on the dump file with the appropriate commands. 
            string result = await kuduManager.ExecuteCommandAsync($"DotnetAnalyzer.exe analyze-memory C://local//{memoryDumpFile}", "C://local//");

            // Delete dump after analysis to save space.
            try
            {
                string _ = await kuduManager.ExecuteCommandAsync($"del C://local//{memoryDumpFile}", "C://local");
            }

            catch (Exception)
            {
                _logger.LogInternalError($"Failed to delete dump: {memoryDumpFile}");
            }

            return result;
        }
    }

    internal async Task<string> AnalyzeCpuAsync(string resourceId, ComputeResourceInfo info, AnalysisType type, string additionalProperties)
    {
        string cpuStackReport = await _armHelper.ProfileAndGetCPUReport(resourceId);
        var jsonReaderSettings = new JsonLoadSettings
        {
            CommentHandling = CommentHandling.Ignore,
            LineInfoHandling = LineInfoHandling.Ignore
        };

        // Parse the JSON with increased depth limit
        using (var stringReader = new StringReader(cpuStackReport))
        using (var jsonReader = new JsonTextReader(stringReader))
        {
            jsonReader.MaxDepth = null;
            var serializer = new Newtonsoft.Json.JsonSerializer
            {
                MaxDepth = null // Unlimited depth
            };

            var rootNode = serializer.Deserialize<FunctionNode>(jsonReader);

            if (rootNode != null)
            {
                List<FunctionNode> allNodes = new List<FunctionNode>();
                CPUFunctionAnalyzer.TraverseTree(rootNode, allNodes);

                StringBuilder sb = new StringBuilder();
                // Get top inclusive methods
                var topInclusiveMethods = CPUFunctionAnalyzer.GetTopInclusiveMethods(allNodes, 10);
                sb.AppendLine("Top Inclusive Methods:");
                foreach (var method in topInclusiveMethods)
                {
                    sb.AppendLine(CPUFunctionAnalyzer.PrintSummary(method));
                }

                // Get top exclusive methods
                var topExclusiveMethods = CPUFunctionAnalyzer.GetTopExclusiveMethods(allNodes, 10);
                sb.AppendLine("Top Exclusive Methods:");
                foreach (var method in topExclusiveMethods)
                {
                    sb.AppendLine(CPUFunctionAnalyzer.PrintSummary(method));
                }

                // Get user methods
                sb.AppendLine("\nUser Methods:");
                var userMethods = CPUFunctionAnalyzer.GetUserMethods(allNodes).OrderByDescending(m => m.InclusiveMetricPercent).Take(10);
                foreach (var method in userMethods)
                {
                    sb.AppendLine(CPUFunctionAnalyzer.PrintSummary(method));
                }

                return sb.ToString();
            }

            else
            {
                string errorMessage = $"Failed to parse CPU stack report. RootNode is null or not found in the CPU stack data for {resourceId}.";
                _logger.LogInternalError(errorMessage);
                throw new ArgumentException(errorMessage);
            }
        }
    }
}
