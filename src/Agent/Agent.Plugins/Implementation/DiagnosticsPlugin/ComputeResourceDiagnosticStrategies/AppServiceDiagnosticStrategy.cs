using System.Text;
using System.Text.RegularExpressions;
using Agent.Core.Helpers;
using Agent.Logging;
using Agent.Plugins.Definitions;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Agent.Plugins.Implementation.DiagnosticsPlugin.ComputeResourceDiagnosticStrategies;

internal class AppServiceDiagnosticStrategy : ComputeResourceDiagnosticStrategyBase
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
        string memoryDumpFile = Path.GetFileName(Path.GetTempFileName() + ".dmp");
        KuduManager kuduManager = await KuduManager.Initialize(resourceId, _armHelper);
        int pid = await _armHelper.GetDefaultProcessIdForWebAppAsync(resourceId, kuduManager.OS, kuduManager.KuduHostName);

        if (kuduManager.OS == "Linux")
        {
            // Step 1. Command to collect the dump on Linux.
            string path = "/tmp/" + memoryDumpFile;
            string command = $"curl http://0.0.0.0:8181/api/processes/{pid}/dump?type=full -o {path}";
            string commandResult = await _armHelper.ExecuteKuduCommandAsync(kuduManager.KuduHostName, command, "/tmp/");

            // Step 2: Analyze the dump.
            await kuduManager.ExecuteCommandAsync("curl -X GET https://dotnetanalysis.blob.core.windows.net/lin64/DotnetAnalyzer -o dotnetanalyzer", "/tmp/");
            await kuduManager.ExecuteCommandAsync($"chmod +x ./dotnetanalyzer", "/tmp/");
            string result = await kuduManager.ExecuteCommandAsync($"./dotnetanalyzer analyze-memory {path}", "/tmp/");

            // Step 3: Delete the dump file after analysis to save space.
            try
            {
                string _ = await kuduManager.ExecuteCommandAsync($"rm {path}", "/tmp/");
            }

            catch (Exception)
            {
                _logger.LogInternalError($"Failed to delete dump: {memoryDumpFile}");
            }

            return result;
        }

        else // Windows.
        {
            string path = "C://local//" + memoryDumpFile;

            // Curl traceCommand on the machine to collect the dump.
            // Step 1: Take a full memory dump.
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

    public sealed class KuduCommandResult 
    {
        public string Output { get; set; }
        public string Error { get; set; }
        public int ExitCode { get; set; }
    }

    internal async Task<string> AnalyzeCpuAsync(string resourceId, ComputeResourceInfo info, AnalysisType type, string additionalProperties)
    {
        KuduManager kuduManager = await KuduManager.Initialize(resourceId, _armHelper);
        int pid = await _armHelper.GetDefaultProcessIdForWebAppAsync(resourceId, kuduManager.OS, kuduManager.KuduHostName);

        // Local helper to simplify ExecuteKuduCommandAsync calls
        async Task<string> ExecKudu(string command, string workingDir = "/home")
            => await _armHelper.ExecuteKuduCommandAsync(kuduManager.KuduHostName, command, workingDir);

        if (kuduManager.OS == "Linux")
        {
            string traceFile = Path.GetFileName(Path.GetTempFileName() + ".nettrace");

            // Step 1: Take trace for 30 seconds.
            string traceCommand = $"curl http://0.0.0.0:8181/api/processes/{pid}/profile/start?durationSeconds=30 -o {traceFile}";
            string traceCommandResult = await ExecKudu(traceCommand);

            // Step 2: Download Dotnet and analyze the trace.
            string runAnalysisCommand = await ExecKudu("curl -X GET https://dotnetanalysis.blob.core.windows.net/webappscripts/dotnet-cpu.sh -o dotnet-cpu.sh");
            runAnalysisCommand = await ExecKudu("chmod u+x dotnet-cpu.sh");
            runAnalysisCommand = await ExecKudu($"./dotnet-cpu.sh {traceFile}");
            var result = System.Text.Json.JsonSerializer.Deserialize<KuduCommandResult>(runAnalysisCommand);
            string pattern = @">>STARTING ANALYSIS<<([\s\S]*?)>>COMPLETED ANALYSIS<<";
            Match match = Regex.Match(result.Output, pattern);
            string analysisResult = "";
            if (match.Success)
            {
                analysisResult = match.Groups[1].Value.Trim();
            }

            else
            {
                _logger.LogInternalError($"No ANALYSIS BLOCK found in the output of the CPU analysis command for: {resourceId}.");
            }

            // Step 3: Delete the dump file after analysis to save space.
            try
            {
                string _ = await ExecKudu($"rm {traceFile}");
            }

            catch
            {
                _logger.LogInternalError($"Failed to delete dump: {traceFile} for {resourceId}");
            }

            return analysisResult;
        }

        else
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
}
