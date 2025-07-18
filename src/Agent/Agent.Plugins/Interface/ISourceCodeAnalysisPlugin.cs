namespace Agent.Plugins.Interface;

public interface ISourceCodeAnalysisPlugin
{
    Task<IReadOnlyList<SemanticSearchResult>> GetSemanticSearchResult(string resourceId, string query);
    Task<string> QueryRepositoryBasedOnError(string resourceId, string errorDescription);
}
