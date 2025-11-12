using System.ComponentModel;
using System.Text.Json;
using Agent.Core.Models;
using Agent.Plugins;
using Agent.Plugins.Kusto;
using Agent.Plugins.KustoPlugin;
using Microsoft.Extensions.DependencyInjection;

[AgentToolPlugin(Category = ToolCategories.LogQuery)]
public class DynamicKqlToolsPlugin
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Dictionary<string, (string cluster, string database, string query, string description)> _queries = new();
    private KustoPlugin? _kustoPlugin;

    public Guid? ThreadId { get; set; }

    public DynamicKqlToolsPlugin(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public void RegisterQuery(string name, string cluster, string database, string query, string description)
    {
        _queries[name] = (cluster, database, query, description);
    }

    [Description("Executes a pre-configured KQL query by name with optional parameters")]
    public async Task<string> ExecuteDynamicQueryByName(
        [Description("The name of the query to execute")] string queryName,
        [Description("JSON string of parameters, e.g. {\"fromDate\":\"2024-01-01\",\"toDate\":\"2024-01-02\"}")] string parametersJson = "{}")
    {
        if (!_queries.TryGetValue(queryName, out var queryInfo))
            return $"Query '{queryName}' not found. Available queries: {string.Join(", ", _queries.Keys)}";

        // Extract required parameter names from the query
        var expectedParams = ExtractParametersFromQuery(queryInfo.query);

        // Parse incoming parameters
        var providedParams = new Dictionary<string, string>();
        if (!string.IsNullOrWhiteSpace(parametersJson) && parametersJson != "{}")
        {
            try
            {
                var jsonDoc = JsonDocument.Parse(parametersJson);
                foreach (var prop in jsonDoc.RootElement.EnumerateObject())
                    providedParams[prop.Name] = prop.Value.GetString() ?? "";
            }
            catch (JsonException)
            {
                return $"Invalid JSON format for parameters. Expected format: {{\"paramName\":\"value\"}}";
            }
        }

        // Map provided params to expected params (case-insensitive)
        var mappedParams = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var expected in expectedParams)
        {
            // Find by case-insensitive match
            var match = providedParams
                .FirstOrDefault(kvp => string.Equals(kvp.Key, expected, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(match.Key))
                mappedParams[expected] = match.Value;
        }

        // Detect missing params
        var missingParams = expectedParams.Where(p => !mappedParams.ContainsKey(p)).ToList();
        if (missingParams.Any())
        {
            return $"Missing required parameters: {string.Join(", ", missingParams)}. " +
                   $"Expected parameters: {string.Join(", ", expectedParams)}. " +
                   $"You provided: {string.Join(", ", providedParams.Keys)}";
        }

        // Substitute only mapped params
        var finalQuery = queryInfo.query;
        foreach (var param in mappedParams)
            finalQuery = finalQuery.Replace($"##{param.Key}##", param.Value);

        // Lazy load the KustoPlugin
        _kustoPlugin ??= _serviceProvider.GetRequiredService<KustoPlugin>();

        // Execute using the pre-configured cluster, database, and formatted query
        var result = await _kustoPlugin.ExecuteClusterKustoQueryInternal(queryInfo.cluster, queryInfo.database, finalQuery);
        if (result == null || string.IsNullOrEmpty(result.Result) || result.Result.Contains("Kusto query execution failed") || result.Result.Contains("failed") || result.Result.Contains("An error occurred while executing"))
        {
            return $"Query '{queryName}' either did not execute successfully or  returned no results.";
        }
        return result.Result;
    }


    [Description("Lists all available registered KQL queries")]
    public Task<List<string>> ListAvailableQueries()
    {
        return Task.FromResult(_queries.Keys.ToList());
    }

    [Description("Gets information about a specific query including its parameters")]
    public Task<string> GetQueryInfo(string queryName)
    {
        if (!_queries.TryGetValue(queryName, out var queryInfo))
        {
            return Task.FromResult($"Query '{queryName}' not found.");
        }

        // Extract parameters from the query
        var parameters = ExtractParametersFromQuery(queryInfo.query);
        var paramInfo = parameters.Any() ? $"Parameters: {string.Join(", ", parameters)}" : "No parameters";

        return Task.FromResult($"Query: {queryName}\nDescription: {queryInfo.description}\nCluster: {queryInfo.cluster}\nDatabase: {queryInfo.database}\n{paramInfo}");
    }

    private List<string> ExtractParametersFromQuery(string query)
    {
        var parameters = new List<string>();

        // Extract ##paramName## parameters
        var matches = System.Text.RegularExpressions.Regex.Matches(query, @"##(\w+)##");
        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            if (!parameters.Contains(match.Groups[1].Value))
            {
                parameters.Add(match.Groups[1].Value);
            }
        }

        return parameters;
    }
}
