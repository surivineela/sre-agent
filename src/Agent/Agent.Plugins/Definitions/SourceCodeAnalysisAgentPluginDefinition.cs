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
    [Description("Responds to generic questions related to source code connected to an Azure resource. Provides insights, guidance, and potential solutions based on the context of the question. Run a natural language search for relevant code or documentation comments from the user's repository. Returns relevant code snippets from the user's repository if it is large, or the full contents of the workspace if it is small. Provides actionable recommendations when applicable.")]
    public async Task<string> QuerySourceBySemanticSearch(
        [Description("The exact Azure resource ID in format '/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/{resourceProviderNamespace}/{resourceType}/{resourceName}' for which the question is pertinent to.")]
        string resourceId,
        [Description("The query to search the codebase for. Should contain all relevant context. Should ideally be text that might appear in the codebase, such as function names, variable names, or comments (e.g., 'How do I debug a NullReferenceException?', 'What are common causes of SQL timeouts?', 'How can I identify the root cause of an OutOfMemoryException?', 'Give me thorough details about startup').")] 
        string query)
    {
        return await _sourceCodeAnalysisPlugin.QueryRepository(resourceId, query);
    }

    [Description("Performs comprehensive semantic code analysis to correlate Azure resource errors with specific source code locations. Extracts and categorizes error traces, call stacks, and runtime exceptions, then conducts semantic search across connected repositories to identify root cause files, methods, and code patterns. Returns structured mapping of errors to probable source code locations with confidence scoring, file paths, line numbers, and actionable insights. Utilizes advanced pattern correlation and contextual analysis to rank source files by probability of being the root cause. Only returns results when a clear correlation between the error and source code is found through semantic analysis.")]
    public async Task<string> CorrelateErrorsWithSourceCode(
        [Description("The exact Azure resource ID in format '/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/{resourceProviderNamespace}/{resourceType}/{resourceName}' that is experiencing the error")]
        string resourceId,
        [Description("Specific error details including error codes, exception messages, stack traces, or precise symptoms from Azure logs/monitoring in the form of a query with all relevant context. (e.g., 'NullReferenceException in OrderService.ProcessPayment method', 'SQL timeout on SELECT query in UserRepository.GetUserById', 'OutOfMemoryException in background job processing')")]
        string errorQuery)
    {
        return await _sourceCodeAnalysisPlugin.QueryRepositoryBasedOnError(resourceId, errorQuery);
    }
}
