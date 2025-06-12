using Agent.Core.Helpers;
using Agent.Logging;
using Agent.Plugins.Interface;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Agent.Plugins.Implementation.DiagnosticsPlugin.ComputeResourceDiagnosticStrategies;

internal class FunctionAppsDiagnosticStrategy : ComputeResourceDiagnosticStrategyBase
{
    private readonly ArmHelper _armHelper;
    private readonly ILogger<DiagnosticsPlugin> _logger;
    private AppServiceDiagnosticStrategy _appServiceDiagnosticStrategy; 

    public FunctionAppsDiagnosticStrategy(ILogger<DiagnosticsPlugin> logger, ArmHelper armHelper)
        : base(logger)
    {
        _logger = logger;
        _armHelper = armHelper;
        _appServiceDiagnosticStrategy = new AppServiceDiagnosticStrategy(logger, armHelper);
        _analysisHandlers = new Dictionary<AnalysisType, Func<string, ComputeResourceInfo, AnalysisType, string, Task<string>>>
        {
            { AnalysisType.Cpu, AnalyzeCpuAsync },
            { AnalysisType.Memory, AnalyzeMemoryAsync },
        };
    }

    private async Task<bool> IsPremiumSKU(string resourceId)
    {
        var armResource = await _armHelper.GetArmResourceAsJsonAsync(resourceId);
        var jsonObject = JObject.Parse(armResource);
        if (jsonObject == null || !jsonObject.ContainsKey("properties"))
        {
            _logger.LogInternalError($"Failed to parse ARM resource for {resourceId} or 'properties' not found.");
            return false;
        }

        if (jsonObject["properties"] == null || !jsonObject["properties"].HasValues)
        {
            _logger.LogInternalError($"'sku' not found in the ARM resource properties for {resourceId}.");
            return false;
        }

        var sku = jsonObject["properties"]?["sku"]?.Value<string>() ?? null;
        return (sku != null &&
                sku.Contains("Premium", StringComparison.OrdinalIgnoreCase));
    }

    private async Task<string> AnalyzeMemoryAsync(string resourceId, ComputeResourceInfo info, AnalysisType type, string additionalArguments)
    {
        if (info.OsType == OSType.Linux)
        {
            string errorMessage = $"Memory analysis is only supported for Windows Function Apps on Premium SKU | Resource ID: {resourceId}.";
            _logger.LogInternalError(errorMessage);
            throw new ArgumentException(errorMessage);
        }

        if (!await IsPremiumSKU(resourceId))
        {
            string errorMessage = $"Memory analysis is only supported for Function Apps on Premium SKU for {resourceId}.";
            _logger.LogInternalError(errorMessage);
            throw new ArgumentException(errorMessage);
        }

        return await _appServiceDiagnosticStrategy.AnalyzeMemoryAsync(resourceId, info, type, additionalArguments);
    }

    private async Task<string> AnalyzeCpuAsync(string resourceId, ComputeResourceInfo info, AnalysisType type, string additionalArguments)
    {
        if (info.OsType == OSType.Linux)
        {
            string errorMessage = $"CPU analysis is only supported for Windows Function Apps on Premium SKU | Resource ID: {resourceId}.";
            _logger.LogInternalError(errorMessage);
            throw new ArgumentException(errorMessage);
        }

        if (!await IsPremiumSKU(resourceId))
        {
            string errorMessage = $"CPU analysis is only supported for Function Apps on Premium SKU | Resource ID: {resourceId}.";
            _logger.LogInternalError(errorMessage);
            throw new ArgumentException(errorMessage);
        }

        return await _appServiceDiagnosticStrategy.AnalyzeCpuAsync(resourceId, info, type, additionalArguments);
    }

    public override bool CanHandle(ComputeResourceInfo resourceInfo)
        => resourceInfo.ResourceType == ComputeResourceType.FunctionApp;
}
