namespace Agent.Plugins.Interface;

/// <summary>
/// Represents a single code match within a search result
/// </summary>
public interface ICodeMatch
{
    int LineNumber { get; }
    int ColumnNumber { get; }
    string CodeSnippet { get; }
    string MatchType { get; }
}

/// <summary>
/// Represents a search result from Azure DevOps code search
/// </summary>
public interface ICodeSearchResult
{
    string FileName { get; }
    string FilePath { get; }
    string Repository { get; }
    string Project { get; }
    string Branch { get; }
    IReadOnlyList<ICodeMatch> Matches { get; }
    int MatchCount { get; }
}

/// <summary>
/// Implementation of code match
/// </summary>
public class CodeMatchImpl : ICodeMatch
{
    public int LineNumber { get; init; }
    public int ColumnNumber { get; init; }
    public string CodeSnippet { get; init; } = string.Empty;
    public string MatchType { get; init; } = string.Empty;

    public override string ToString() => $"Line {LineNumber}: {CodeSnippet}";
}

/// <summary>
/// Implementation of code search result
/// </summary>
public class CodeSearchResultImpl : ICodeSearchResult
{
    private readonly List<ICodeMatch> _matches = new();

    public string FileName { get; init; } = string.Empty;
    public string FilePath { get; init; } = string.Empty;
    public string Repository { get; init; } = string.Empty;
    public string Project { get; init; } = string.Empty;
    public string Branch { get; init; } = string.Empty;
    public IReadOnlyList<ICodeMatch> Matches => _matches.AsReadOnly();
    public int MatchCount => _matches.Count;

    public void AddMatch(ICodeMatch match) => _matches.Add(match);

    public override string ToString() => $"{Repository}/{FileName} ({MatchCount} matches, Branch: {Branch}) - {string.Join(", ", Matches)}";
}

/// <summary>
/// Configuration options for Azure DevOps search
/// </summary>
public class SearchOptions
{
    public int MaxResults { get; init; } = 10;
    public string? ProjectFilter { get; set; }
    public bool IncludeSnippets { get; init; } = true;
    public bool Verbose { get; init; } = false;
    public bool SearchAllBranches { get; init; } = true;
    public bool LimitToMainBranches { get; init; } = false;
    public string[]? FileExtensions { get; init; }
    public string[]? Repositories { get; init; }
}

/// <summary>
/// Search statistics and metadata
/// </summary>
public class SearchMetadata
{
    public int TotalResults { get; init; }
    public int InfoCode { get; init; }
    public string[]? Repositories { get; init; }
    public string[]? Branches { get; init; }
    public string[]? FileTypes { get; init; }
    public TimeSpan SearchDuration { get; init; }
    public bool IsSuccess => InfoCode == 0;
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Complete search response with results and metadata
/// </summary>
public class SearchResponse
{
    public IReadOnlyList<ICodeSearchResult> Results { get; init; } = Array.Empty<ICodeSearchResult>();
    public SearchMetadata Metadata { get; init; } = new();
    public bool IsSuccess => Metadata.IsSuccess;
    public int Count => Results.Count;
    public override string ToString() => $"SearchResponse: {Count} results: {string.Join(" ;", Results)}, Success: {IsSuccess}, Duration: {Metadata.SearchDuration.TotalSeconds}s";
}

/// <summary>
/// Main interface for Azure DevOps code search service
/// </summary>
public interface IAzureDevOpsSourceCodeSearch
{
    /// <summary>
    /// Performs a comprehensive search with full options
    /// </summary>
    Task<SearchResponse> SearchAsync(string searchTerm, string repoUrl, SearchOptions? options = null);
    Task<string> GetFileContentAsync(string organization, string project, string repositoryId, string filePath);
}
