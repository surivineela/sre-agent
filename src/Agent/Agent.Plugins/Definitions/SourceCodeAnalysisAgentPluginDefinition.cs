using System.ComponentModel;
using Agent.Plugins.Interface;

namespace Agent.Plugins.Definitions;

[AgentToolPlugin]
public class SourceCodeAnalysisAgentPluginDefinition
{
    private readonly ISourceCodeAnalysisPlugin _sourceCodeAnalysisPlugin;

    public SourceCodeAnalysisAgentPluginDefinition(ISourceCodeAnalysisPlugin sourceCodeAnalysisPlugin)
    {
        _sourceCodeAnalysisPlugin = sourceCodeAnalysisPlugin;
    }
    [Description("Responds to generic questions related to source code connected to an Azure resource. Provides insights, guidance, and potential solutions based on the context of the question. Utilizes semantic analysis and contextual understanding to generate meaningful responses. Returns structured answers or actionable recommendations when applicable.")]
    public async Task<string> QuerySourceBySemanticSearch(
        [Description("The exact Azure resource ID in format '/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/{resourceProviderNamespace}/{resourceType}/{resourceName}' for which the question is pertinent to.")]
        string resourceId,
        [Description("The context or question related to source code analysis or Azure resource errors (e.g., 'How do I debug a NullReferenceException?', 'What are common causes of SQL timeouts?', 'How can I identify the root cause of an OutOfMemoryException?')")]
        string question)
    {
        return await _sourceCodeAnalysisPlugin.QueryRepository(resourceId, question);
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
