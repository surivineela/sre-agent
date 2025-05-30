using Agent.Core.Helpers;
using Agent.Logging;
using Agent.Plugins.Definitions;
using Microsoft.Extensions.Logging;

namespace Agent.Plugins.Implementation.DiagnosticsPlugin.ComputeResourceDiagnosticStrategies;

internal sealed class AppServiceDiagnosticStrategy : ComputeResourceDiagnosticStrategyBase 
{
    private readonly ArmHelper _armHelper;

    public AppServiceDiagnosticStrategy(ILogger<DiagnosticsPlugin> logger, ArmHelper armHelper)
        : base(logger)
    {
        _armHelper = armHelper;
        _analysisHandlers = new Dictionary<AnalysisType, Func<string, ComputeResourceInfo, AnalysisType, Task<string>>>
        {
            { AnalysisType.Memory, AnalyzeMemoryAsync },
        };
    }

    public override bool CanHandle(ComputeResourceInfo resourceInfo)
        => resourceInfo.ResourceType == ComputeResourceType.AppService;

    internal async Task<string> AnalyzeMemoryAsync(string resourceId, ComputeResourceInfo computeResourceInfo, AnalysisType analysisType)
    {
        // Step 1: Take a full memory dump.
        string memoryDumpFile = Path.GetFileName(Path.GetTempFileName() + ".dmp");
        KuduManager kuduManager = await KuduManager.Initialize(resourceId, _armHelper);
        if (kuduManager.OS == "Linux")
        {
            throw new NotImplementedException("Currently this behavior isn't implemented for Linux");
        }

        // Curl command on the machine to collect the dump.
        int pid = await _armHelper.GetDefaultProcessIdForWebAppAsync(resourceId, kuduManager.OS, kuduManager.KuduHostName);
        string command = $"C://devtools//sysinternals//procdump.exe -ma {pid} -accepteula C://home//{memoryDumpFile}";
        string commandResult = await _armHelper.ExecuteKuduCommandAsync(kuduManager.KuduHostName, command, "C://home//");

        // Step 2: Analyze the dump.
        if (kuduManager.OS == "Windows")
        {
            if (kuduManager.Is32Bit)
            {
                await kuduManager.ExecuteCommandAsync("curl -X GET https://dotnetanalysis.blob.core.windows.net/win32/DotnetAnalyzer.exe -o DotnetAnalyzer.exe", "C://home//");
            }

            else
            {
                await kuduManager.ExecuteCommandAsync("curl -X GET https://dotnetanalysis.blob.core.windows.net/win64/DotnetAnalyzer.exe -o DotnetAnalyzer.exe", "C://home//");
            }

            // Run the dotnet analyzer on the dump file with the appropriate commands. 
            string result = await kuduManager.ExecuteCommandAsync($"DotnetAnalyzer.exe analyze-memory C://home//{memoryDumpFile}", "C://home//");

            // Delete dump after analysis to save space.
            try
            {
                string _ = await kuduManager.ExecuteCommandAsync($"del C://home//{memoryDumpFile}", "C://home");
            }

            catch (Exception)
            {
                _logger.LogInternalError($"Failed to delete dump: {memoryDumpFile}");
            }

            return result;
        }

        else // TODO: Add the Linux case.
        {
            throw new NotImplementedException("Not implemented for Linux yet.");
        }
    }
}
