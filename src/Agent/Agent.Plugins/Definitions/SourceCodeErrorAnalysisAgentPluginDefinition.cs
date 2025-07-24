using System.ComponentModel;
using Agent.Plugins.Interface;

namespace Agent.Plugins.Definitions;

[AgentToolPlugin]
public class SourceCodeErrorAnalysisAgentPluginDefinition
{
    private readonly ISourceCodeAnalysisPlugin _sourceCodeAnalysisPlugin;

    public SourceCodeErrorAnalysisAgentPluginDefinition(ISourceCodeAnalysisPlugin sourceCodeAnalysisPlugin)
    {
        _sourceCodeAnalysisPlugin = sourceCodeAnalysisPlugin;
    }

    [Description("Performs comprehensive semantic code analysis to correlate Azure resource errors with specific source code locations. Extracts and categorizes error traces, call stacks, and runtime exceptions, then conducts semantic search across connected repositories to identify root cause files, methods, and code patterns. Returns structured mapping of errors to probable source code locations with confidence scoring, file paths, line numbers, and actionable insights. Utilizes advanced pattern correlation and contextual analysis to rank source files by probability of being the root cause. Only returns results when a clear correlation between the error and source code is found through semantic analysis.")]
    public async Task<string> CorrelateErrorsWithSourceCode(
        [Description("The exact Azure resource ID in format '/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/{resourceProviderNamespace}/{resourceType}/{resourceName}' that is experiencing the error")]
        string resourceId,
        [Description("Specific error details including error codes, exception messages, stack traces, or precise symptoms from Azure logs/monitoring (e.g., 'NullReferenceException in OrderService.ProcessPayment method', 'SQL timeout on SELECT query in UserRepository.GetUserById', 'OutOfMemoryException in background job processing')")]
        string errorQuery)
    {
        return await _sourceCodeAnalysisPlugin.QueryRepositoryBasedOnError(resourceId, errorQuery);
    }
}
