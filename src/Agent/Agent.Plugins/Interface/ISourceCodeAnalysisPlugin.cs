namespace Agent.Plugins.Interface;

public interface ISourceCodeAnalysisPlugin
{
    Task<IReadOnlyList<SemanticSearchResult>> GetSemanticSearchResult(string resourceId, string query);
    Task<bool> IsRepositoryIndexed(string repositoryUrl);
    Task<string> ForceRepositoryIndexing(string repositoryUrl);
    Task<string> QueryRepositoryBasedOnError(string resourceId, string errorDescription);
    Task<string> QueryRepository(string resourceId, string query);
}
